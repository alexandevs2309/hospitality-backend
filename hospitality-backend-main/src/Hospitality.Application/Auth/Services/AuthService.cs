using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Hospitality.Application.Audit;
using Hospitality.Application.Auth.Commands;
using Hospitality.Application.Common.DTOs;
using Hospitality.Application.Common.Interfaces;
using Hospitality.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using QRCoder;

namespace Hospitality.Application.Auth.Services;

public class AuthService : IAuthService
{
    private const string TwoFactorPurposeClaim = "2fa";
    private const string AuthenticatorProvider = "Authenticator";
    private static readonly string[] Base32Alphabet =
        "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567".Select(c => c.ToString()).ToArray();

    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly IConfiguration _configuration;
    private readonly ILogger<AuthService> _logger;
    private readonly IAuditService _audit;

    public AuthService(
        UserManager<ApplicationUser> userManager,
        IApplicationDbContext context,
        ICurrentUserService currentUserService,
        IConfiguration configuration,
        ILogger<AuthService> logger,
        IAuditService audit)
    {
        _userManager = userManager;
        _context = context;
        _currentUserService = currentUserService;
        _configuration = configuration;
        _logger = logger;
        _audit = audit;
    }

    public async Task<AuthResponse?> LoginAsync(LoginCommand command)
    {
        var user = await _userManager.FindByEmailAsync(command.Email);
        if (user is null || !user.IsActive || user.IsDeleted)
        {
            await _audit.LogAsync("login_failed", "auth", userName: command.Email);
            return null;
        }

        if (await _userManager.IsLockedOutAsync(user))
        {
            _logger.LogInformation("Cuenta bloqueada temporalmente por intentos fallidos: {Email}", command.Email);
            await _audit.LogAsync("login_locked", "auth", userName: command.Email, userId: user.Id);
            return null;
        }

        var passwordValid = await _userManager.CheckPasswordAsync(user, command.Password);
        if (!passwordValid)
        {
            // Incrementa el contador de intentos fallidos (configurado en 5 intentos / 15 minutos de bloqueo).
            await _userManager.AccessFailedAsync(user);
            _logger.LogInformation("Intento de login fallido para {Email}", command.Email);
            await _audit.LogAsync("login_failed", "auth", userName: command.Email, userId: user.Id);
            return null;
        }

        await _userManager.ResetAccessFailedCountAsync(user);

        // Si el usuario tiene 2FA activado, la contraseña solo acredita el primer paso.
        if (user.TwoFactorEnabled)
        {
            var roles = await _userManager.GetRolesAsync(user);
            _logger.LogInformation("Usuario {Email} acreditó contraseña; requiere segundo factor", command.Email);
            return new AuthResponse
            {
                RequiresTwoFactor = true,
                TwoFactorToken = GenerateTwoFactorLoginToken(user),
                User = MapUser(user, roles)
            };
        }

        user.LastLoginAt = DateTime.UtcNow;
        user.UpdatedAt = DateTime.UtcNow;
        await _userManager.UpdateAsync(user);
        _logger.LogInformation("Login exitoso para el usuario {Email}", command.Email);
        await _audit.LogAsync("login", "auth", userName: user.Email, userId: user.Id);

        return await CreateAuthResponseAsync(user);
    }

    public async Task<AuthResponse?> LoginWithTwoFactorAsync(TwoFactorLoginCommand command)
    {
        var user = await ResolveTwoFactorLoginTokenAsync(command.TwoFactorToken);
        if (user is null || !user.IsActive || user.IsDeleted)
        {
            await _audit.LogAsync("login_failed", "auth", details: "Segundo factor: token de sesión inválido o expirado");
            return null;
        }

        if (await _userManager.IsLockedOutAsync(user))
        {
            await _audit.LogAsync("login_locked", "auth", userName: user.Email, userId: user.Id);
            return null;
        }

        var code = command.Code.Trim().Replace(" ", string.Empty);
        var validTotp = await _userManager.VerifyTwoFactorTokenAsync(user, AuthenticatorProvider, code);
        var usedRecoveryCode = false;

        if (!validTotp)
        {
            usedRecoveryCode = await UseRecoveryCodeAsync(user, code);
            if (usedRecoveryCode)
            {
                await _audit.LogAsync("recovery_code_used", "auth", userName: user.Email, userId: user.Id);
            }
        }

        if (!validTotp && !usedRecoveryCode)
        {
            await _userManager.AccessFailedAsync(user);
            _logger.LogWarning("Segundo factor rechazado para {Email}", user.Email);
            await _audit.LogAsync("login_failed", "auth", userName: user.Email, userId: user.Id);
            return null;
        }

        await _userManager.ResetAccessFailedCountAsync(user);
        user.LastLoginAt = DateTime.UtcNow;
        user.UpdatedAt = DateTime.UtcNow;
        await _userManager.UpdateAsync(user);
        _logger.LogInformation("Login con 2FA exitoso para {Email}", user.Email);
        await _audit.LogAsync("login_2fa", "auth", userName: user.Email, userId: user.Id);

        var response = await CreateAuthResponseAsync(user);
        response.RequiresTwoFactor = false;
        return response;
    }

    public async Task<AuthResponse> RegisterAsync(RegisterCommand command)
    {
        if (command.Password != command.ConfirmPassword)
        {
            throw new ArgumentException("Las contraseñas no coinciden.");
        }

        if (await _userManager.FindByEmailAsync(command.Email) is not null)
        {
            throw new ArgumentException("El correo electrónico ya está registrado.");
        }

        var user = new ApplicationUser
        {
            UserName = command.Email,
            Email = command.Email,
            EmailConfirmed = false,
            FirstName = command.FirstName,
            LastName = command.LastName,
            PhoneNumber = command.PhoneNumber,
            Department = string.IsNullOrWhiteSpace(command.Department) ? null : command.Department,
            Position = string.IsNullOrWhiteSpace(command.Position) ? null : command.Position,
            Language = string.IsNullOrWhiteSpace(command.Language) ? "es" : command.Language,
            TimeZone = string.IsNullOrWhiteSpace(command.TimeZone) ? "UTC" : command.TimeZone,
            IsActive = true,
            MustChangePassword = false,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var result = await _userManager.CreateAsync(user, command.Password);
        if (!result.Succeeded)
        {
            throw new ArgumentException(string.Join(" ", result.Errors.Select(e => e.Description)));
        }

        await _userManager.AddToRoleAsync(user, "User");
        _logger.LogInformation("Usuario registrado con ID {UserId}", user.Id);
        await _audit.LogAsync("register", "auth", userName: user.Email, userId: user.Id);

        return await CreateAuthResponseAsync(user);
    }

    public async Task<UserDto> GetCurrentUserAsync()
    {
        var user = await GetCurrentUserOrThrowAsync();
        var roles = await _userManager.GetRolesAsync(user);
        return MapUser(user, roles);
    }

    public async Task<UserDto> UpdateCurrentUserAsync(UpdateUserCommand command)
    {
        var user = await GetCurrentUserOrThrowAsync();

        user.FirstName = command.FirstName;
        user.LastName = command.LastName;
        user.PhoneNumber = command.PhoneNumber;
        user.ProfilePicture = command.ProfilePicture;
        user.Language = command.Language;
        user.TimeZone = command.TimeZone;
        user.EmailNotifications = command.EmailNotifications;
        user.SmsNotifications = command.SmsNotifications;
        user.UpdatedAt = DateTime.UtcNow;

        await _userManager.UpdateAsync(user);
        _logger.LogInformation("Usuario {UserId} actualizado", user.Id);

        var roles = await _userManager.GetRolesAsync(user);
        return MapUser(user, roles);
    }

    public async Task<AuthResult> ChangePasswordAsync(ChangePasswordCommand command)
    {
        if (command.NewPassword != command.ConfirmPassword)
        {
            return AuthResult.Failure("Las contraseñas no coinciden.");
        }

        var user = await GetCurrentUserOrThrowAsync();
        var result = await _userManager.ChangePasswordAsync(user, command.CurrentPassword, command.NewPassword);
        if (!result.Succeeded)
        {
            return AuthResult.Failure(result.Errors.Select(e => e.Description).ToArray());
        }

        user.MustChangePassword = false;
        await _userManager.UpdateAsync(user);
        await _audit.LogAsync("password_changed", "auth", userName: user.Email, userId: user.Id);
        return AuthResult.Success();
    }

    public async Task<bool> ForgotPasswordAsync(ForgotPasswordCommand command)
    {
        var user = await _userManager.FindByEmailAsync(command.Email);
        if (user is null)
        {
            return true;
        }

        var token = await _userManager.GeneratePasswordResetTokenAsync(user);
        // El token de restablecimiento NUNCA debe registrarse en logs.
        // Debe entregarse al usuario por un canal seguro (email/SMS).
        // TODO: integrar servicio de notificaciones (email/SMS) para enviar el enlace.
        _logger.LogInformation("Solicitud de restablecimiento de contraseña recibida para {Email}. Token de restablecimiento generado.", command.Email);
        return true;
    }

    public async Task<AuthResult> ResetPasswordAsync(ResetPasswordCommand command)
    {
        if (command.NewPassword != command.ConfirmPassword)
        {
            return AuthResult.Failure("Las contraseñas no coinciden.");
        }

        var user = await _userManager.FindByEmailAsync(command.Email);
        if (user is null)
        {
            return AuthResult.Failure("No se encontró un usuario con ese correo electrónico.");
        }

        var result = await _userManager.ResetPasswordAsync(user, command.Token, command.NewPassword);
        if (result.Succeeded)
        {
            await _audit.LogAsync("password_reset", "auth", userName: user.Email, userId: user.Id);
            return AuthResult.Success();
        }
        return AuthResult.Failure(result.Errors.Select(e => e.Description).ToArray());
    }

    public async Task<AuthResponse?> RefreshTokenAsync(RefreshTokenCommand command)
    {
        var hash = HashToken(command.RefreshToken);
        var session = await _context.UserSessions
            .FirstOrDefaultAsync(s => s.RefreshTokenHash == hash);

        if (session is null)
        {
            return null;
        }

        var user = await _userManager.FindByIdAsync(session.UserId);
        if (user is null || user.IsDeleted || !user.IsActive)
        {
            return null;
        }

        var now = DateTime.UtcNow;
        if (session.RevokedAt is not null || session.ExpiresAt <= now)
        {
            return null;
        }

        // Rotación del token de refresco: el antiguo queda inservible.
        var rawToken = NewRefreshToken();
        session.RefreshTokenHash = HashToken(rawToken);
        session.LastUsedAt = now;
        session.ExpiresAt = now.AddDays(GetRefreshTokenExpiryDays());
        session.IpAddress = _currentUserService.IpAddress;
        session.UserAgent = _currentUserService.DeviceInfo.UserAgent;
        session.DeviceName = _currentUserService.DeviceInfo.Name;
        await _context.SaveChangesAsync();

        var roles = await _userManager.GetRolesAsync(user);
        return await BuildAuthResponseAsync(user, roles, session, rawToken);
    }

    public async Task LogoutAsync()
    {
        var user = await GetCurrentUserOrThrowAsync();

        var sessionId = _currentUserService.SessionId;
        if (sessionId.HasValue)
        {
            var session = await _context.UserSessions
                .FirstOrDefaultAsync(s => s.Id == sessionId.Value && s.UserId == user.Id);
            if (session is not null && session.RevokedAt is null)
            {
                session.RevokedAt = DateTime.UtcNow;
                session.RevokedByUserId = user.Id;
                await _context.SaveChangesAsync();
            }
        }

        _logger.LogInformation("Sesión cerrada para {UserId}", user.Id);
        await _audit.LogAsync("logout", "auth", userName: user.Email, userId: user.Id);
    }

    public async Task<bool> CheckEmailAvailabilityAsync(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return false;
        }

        return !await _context.Users.AnyAsync(u => u.Email == email.Trim());
    }

    public async Task<TwoFactorSetupDto> GetTwoFactorSetupAsync()
    {
        var user = await GetCurrentUserOrThrowAsync();
        if (user.TwoFactorEnabled)
        {
            throw new InvalidOperationException("La verificación en dos pasos ya está activada.");
        }

        await _userManager.ResetAuthenticatorKeyAsync(user);
        await _userManager.UpdateAsync(user);

        var sharedKey = await _userManager.GetAuthenticatorKeyAsync(user);
        if (string.IsNullOrEmpty(sharedKey))
        {
            throw new InvalidOperationException("No se pudo generar la clave de autenticador.");
        }

        var issuer = _configuration["TwoFactor:Issuer"] ?? "HospitalityOS";
        var account = $"{issuer}:{user.Email}";
        var keyUri = $"otpauth://totp/{Uri.EscapeDataString(account)}?secret={sharedKey}&issuer={issuer}&algorithm=SHA1&digits=6&period=30";

        var svg = string.Empty;
        using (var generator = new QRCodeGenerator())
        {
            var qrData = generator.CreateQrCode(keyUri, QRCodeGenerator.ECCLevel.M);
            using var qr = new SvgQRCode(qrData);
            svg = qr.GetGraphic(8);
        }

        return new TwoFactorSetupDto { SharedKey = sharedKey, QrCodeSvg = svg, KeyUri = keyUri };
    }

    public async Task<TwoFactorVerifyResult> VerifyTwoFactorAsync(TwoFactorVerifyCommand command)
    {
        var user = await GetCurrentUserOrThrowAsync();
        var code = command.Code.Trim().Replace(" ", string.Empty);

        if (await _userManager.VerifyTwoFactorTokenAsync(user, AuthenticatorProvider, code) is false)
        {
            return new TwoFactorVerifyResult { Succeeded = false };
        }

        user.TwoFactorEnabled = true;
        var recoveryCodes = GenerateRecoveryCodes();
        user.RecoveryCodes = string.Join(':', recoveryCodes.Select(HashToken));
        await _userManager.UpdateAsync(user);

        _logger.LogInformation("2FA activado para {Email}", user.Email);
        await _audit.LogAsync("two_factor_enabled", "auth", userName: user.Email, userId: user.Id);

        return new TwoFactorVerifyResult { Succeeded = true, RecoveryCodes = recoveryCodes };
    }

    public async Task<TwoFactorVerifyResult> RegenerateRecoveryCodesAsync(TwoFactorRecoveryCodesCommand command)
    {
        var user = await GetCurrentUserOrThrowAsync();

        if (!user.TwoFactorEnabled)
        {
            return new TwoFactorVerifyResult { Succeeded = false };
        }

        var code = command.Code.Trim().Replace(" ", string.Empty);
        if (await _userManager.VerifyTwoFactorTokenAsync(user, AuthenticatorProvider, code) is false)
        {
            return new TwoFactorVerifyResult { Succeeded = false };
        }

        var recoveryCodes = GenerateRecoveryCodes();
        user.RecoveryCodes = string.Join(':', recoveryCodes.Select(HashToken));
        await _userManager.UpdateAsync(user);

        _logger.LogInformation("Códigos de recuperación regenerados para {Email}", user.Email);
        await _audit.LogAsync("two_factor_recovery_regenerated", "auth", userName: user.Email, userId: user.Id);

        return new TwoFactorVerifyResult { Succeeded = true, RecoveryCodes = recoveryCodes };
    }

    public async Task<bool> DisableTwoFactorAsync(TwoFactorDisableCommand command)
    {
        var user = await GetCurrentUserOrThrowAsync();
        var code = command.Code.Trim().Replace(" ", string.Empty);

        if (await _userManager.VerifyTwoFactorTokenAsync(user, AuthenticatorProvider, code) is false)
        {
            return false;
        }

        user.TwoFactorEnabled = false;
        user.RecoveryCodes = null;
        await _userManager.UpdateAsync(user);

        _logger.LogInformation("2FA desactivado para {Email}", user.Email);
        await _audit.LogAsync("two_factor_disabled", "auth", userName: user.Email, userId: user.Id);
        return true;
    }

    public async Task<IReadOnlyList<SessionDto>> GetSessionsAsync()
    {
        var user = await GetCurrentUserOrThrowAsync();
        var currentSessionId = _currentUserService.SessionId;
        var now = DateTime.UtcNow;

        var sessions = await _context.UserSessions
            .Where(s => s.UserId == user.Id && s.RevokedAt == null && s.ExpiresAt > now)
            .OrderByDescending(s => s.LastUsedAt)
            .ToListAsync();

        return sessions.Select(s => new SessionDto
        {
            Id = s.Id,
            DeviceName = string.IsNullOrWhiteSpace(s.DeviceName) ? "Navegador" : s.DeviceName,
            IpAddress = s.IpAddress,
            IsCurrent = currentSessionId.HasValue && s.Id == currentSessionId.Value,
            CreatedAt = s.CreatedAt,
            LastUsedAt = s.LastUsedAt,
            ExpiresAt = s.ExpiresAt
        }).ToList();
    }

    public async Task RevokeSessionAsync(RevokeSessionCommand command)
    {
        var user = await GetCurrentUserOrThrowAsync();

        if (_currentUserService.SessionId == command.SessionId)
        {
            throw new InvalidOperationException("No puedes cerrar la sesión actual desde esta pantalla. Usa 'Cerrar sesión'.");
        }

        var session = await _context.UserSessions
            .FirstOrDefaultAsync(s => s.Id == command.SessionId && s.UserId == user.Id);
        if (session is null)
        {
            throw new KeyNotFoundException("La sesión no existe o no pertenece a este usuario.");
        }

        session.RevokedAt = DateTime.UtcNow;
        session.RevokedByUserId = user.Id;
        await _context.SaveChangesAsync();

        await _audit.LogAsync("session_revoked", "auth",
            entity: "UserSession", entityId: session.Id.ToString(),
            details: session.DeviceName, userName: user.Email, userId: user.Id);
    }

    private async Task<ApplicationUser> GetCurrentUserOrThrowAsync()
    {
        var userId = _currentUserService.UserId;
        if (string.IsNullOrEmpty(userId))
        {
            throw new UnauthorizedAccessException("No hay un usuario autenticado.");
        }

        var user = await _userManager.FindByIdAsync(userId);
        if (user is null || user.IsDeleted)
        {
            throw new UnauthorizedAccessException("No hay un usuario autenticado.");
        }
        return user;
    }

    // ── Emisión de tokens ─────────────────────────────────────────

    private async Task<AuthResponse> CreateAuthResponseAsync(ApplicationUser user)
    {
        var roles = await _userManager.GetRolesAsync(user);
        var (session, rawRefreshToken) = await RegisterSessionAsync(user);
        return await BuildAuthResponseAsync(user, roles, session, rawRefreshToken);
    }

    private async Task<AuthResponse> BuildAuthResponseAsync(ApplicationUser user, IList<string> roles, UserSession session, string rawRefreshToken)
    {
        var expiryMinutes = GetTokenExpiryMinutes();
        var tokenExpiresAt = DateTime.UtcNow.AddMinutes(expiryMinutes);

        return new AuthResponse
        {
            Token = await GenerateJwtTokenAsync(user, roles, expiryMinutes, session.Id),
            RefreshToken = rawRefreshToken,
            ExpiresAt = tokenExpiresAt,
            User = MapUser(user, roles)
        };
    }

    private async Task<(UserSession Session, string RawToken)> RegisterSessionAsync(ApplicationUser user)
    {
        var now = DateTime.UtcNow;
        var device = _currentUserService.DeviceInfo;
        var expiryDays = GetRefreshTokenExpiryDays();
        var rawToken = NewRefreshToken();

        // Si el cliente aporta un fingerprint estable, reutilizamos su sesión activa (rotación en el mismo dispositivo).
        if (!string.IsNullOrWhiteSpace(device.Fingerprint))
        {
            var existing = await _context.UserSessions
                .FirstOrDefaultAsync(s => s.UserId == user.Id &&
                                          s.DeviceFingerprint == device.Fingerprint &&
                                          s.RevokedAt == null);
            if (existing is not null)
            {
                existing.DeviceName = device.Name;
                existing.UserAgent = device.UserAgent;
                existing.IpAddress = _currentUserService.IpAddress;
                existing.LastUsedAt = now;
                existing.ExpiresAt = now.AddDays(expiryDays);
                existing.RefreshTokenHash = HashToken(rawToken);
                await _context.SaveChangesAsync();
                return (existing, rawToken);
            }
        }

        var session = new UserSession
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            DeviceName = device.Name,
            UserAgent = device.UserAgent,
            DeviceFingerprint = device.Fingerprint,
            IpAddress = _currentUserService.IpAddress,
            RefreshTokenHash = HashToken(rawToken),
            CreatedAt = now,
            LastUsedAt = now,
            ExpiresAt = now.AddDays(expiryDays)
        };

        _context.UserSessions.Add(session);
        await _context.SaveChangesAsync();
        return (session, rawToken);
    }

    private async Task<string> GenerateJwtTokenAsync(ApplicationUser user, IList<string> roles, int expiryMinutes, Guid sessionId)
    {
        var jwtSettings = _configuration.GetSection("JwtSettings");
        var secret = jwtSettings["Secret"];
        if (string.IsNullOrEmpty(secret))
        {
            throw new InvalidOperationException("JwtSettings:Secret no está configurado.");
        }

        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret)),
            SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email ?? string.Empty),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new("sid", sessionId.ToString())
        };

        // Fase 0: scope de propiedades desde PropertyAssignment (nuevo modelo).
        // Durante la transición, si el usuario no tiene asignaciones activas, se
        // cae al legacy ApplicationUser.HotelId.
        var propertyIds = await _context.PropertyAssignments
            .Where(p => p.UserId == user.Id && p.IsActive && !p.IsDeleted)
            .OrderBy(p => p.CreatedAt)
            .Select(p => p.PropertyId)
            .ToListAsync();

        Guid? effectiveHotelId = propertyIds.Count > 0 ? propertyIds[0] : user.HotelId;
        if (effectiveHotelId.HasValue && effectiveHotelId.Value != Guid.Empty)
        {
            claims.Add(new Claim("hotel_id", effectiveHotelId.Value.ToString()));
        }

        if (propertyIds.Count > 0)
        {
            claims.Add(new Claim("property_ids", string.Join(',', propertyIds)));
        }

        foreach (var role in roles)
        {
            claims.Add(new Claim(ClaimTypes.Role, role));
        }

        var token = new JwtSecurityToken(
            issuer: jwtSettings["Issuer"],
            audience: jwtSettings["Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(expiryMinutes),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private string GenerateTwoFactorLoginToken(ApplicationUser user)
    {
        var jwtSettings = _configuration.GetSection("JwtSettings");
        var secret = jwtSettings["Secret"] ?? string.Empty;
        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret)),
            SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: jwtSettings["Issuer"],
            audience: jwtSettings["Audience"],
            claims: new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
                new Claim("purpose", TwoFactorPurposeClaim),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
            },
            expires: DateTime.UtcNow.AddMinutes(5),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private async Task<ApplicationUser?> ResolveTwoFactorLoginTokenAsync(string tokenValue)
    {
        var jwtSettings = _configuration.GetSection("JwtSettings");
        var secret = jwtSettings["Secret"] ?? string.Empty;

        try
        {
            var handler = new JwtSecurityTokenHandler();
            var principal = handler.ValidateToken(tokenValue, new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = jwtSettings["Issuer"],
                ValidAudience = jwtSettings["Audience"],
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret)),
                ClockSkew = TimeSpan.Zero
            }, out _);

            if (principal.FindFirstValue("purpose") != TwoFactorPurposeClaim)
            {
                return null;
            }

            var userId = principal.FindFirstValue(ClaimTypes.NameIdentifier) ?? principal.FindFirstValue("sub");
            if (string.IsNullOrEmpty(userId))
            {
                return null;
            }

            return await _userManager.FindByIdAsync(userId);
        }
        catch
        {
            return null;
        }
    }

    // ── Códigos de recuperación ──────────────────────────────────

    private static string[] GenerateRecoveryCodes(int count = 10)
    {
        var codes = new string[count];
        for (var i = 0; i < count; i++)
        {
            var bytes = RandomNumberGenerator.GetBytes(8);
            var base32 = ToBase32(bytes);
            codes[i] = $"{base32[..5]}-{base32[5..10]}";
        }
        return codes;
    }

    private async Task<bool> UseRecoveryCodeAsync(ApplicationUser user, string code)
    {
        if (string.IsNullOrWhiteSpace(user.RecoveryCodes))
        {
            return false;
        }

        // Los códigos de recuperación se comparan por hash SHA-256 y se invalidan al usarlos (uso único).
        var normalized = code.Trim().ToUpperInvariant();
        if (normalized.Length != 11 || normalized[5] != '-')
        {
            return false;
        }

        var targetHash = HashToken(normalized);
        var storedHashes = user.RecoveryCodes.Split(':', StringSplitOptions.RemoveEmptyEntries);
        var match = storedHashes.FirstOrDefault(h => h == targetHash);
        if (match is null)
        {
            return false;
        }

        user.RecoveryCodes = string.Join(':', storedHashes.Where(h => h != match));
        await _userManager.UpdateAsync(user);
        return true;
    }

    // ── Utilidades de hash / codificación ────────────────────────

    private static string HashToken(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private static string NewRefreshToken()
    {
        return Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
    }

    private static string ToBase32(byte[] data)
    {
        var sb = new StringBuilder();
        var buffer = 0;
        var bitsLeft = 0;
        foreach (var b in data)
        {
            buffer = (buffer << 8) | b;
            bitsLeft += 8;
            while (bitsLeft >= 5)
            {
                var index = (buffer >> (bitsLeft - 5)) & 31;
                sb.Append(Base32Alphabet[index]);
                bitsLeft -= 5;
            }
        }
        if (bitsLeft > 0)
        {
            var index = (buffer << (5 - bitsLeft)) & 31;
            sb.Append(Base32Alphabet[index]);
        }
        return sb.ToString();
    }

    // ── Configuración y mapeo ─────────────────────────────────────

    private int GetRefreshTokenExpiryDays()
    {
        if (double.TryParse(_configuration["JwtSettings:RefreshTokenExpiryDays"], out var days) &&
            days > 0 && days <= 90)
        {
            return (int)days;
        }

        return 7;
    }

    private int GetTokenExpiryMinutes()
    {
        if (int.TryParse(_configuration["JwtSettings:ExpiryInMinutes"], out var minutes) && minutes > 0)
        {
            return minutes;
        }

        return 60;
    }

    private static UserDto MapUser(ApplicationUser user, IList<string> roles)
    {
        return new UserDto
        {
            Id = Guid.TryParse(user.Id, out var userId) ? userId : Guid.Empty,
            FirstName = user.FirstName,
            LastName = user.LastName,
            Email = user.Email ?? string.Empty,
            PhoneNumber = user.PhoneNumber ?? string.Empty,
            ProfilePicture = user.ProfilePicture,
            Department = user.Department ?? string.Empty,
            Position = user.Position ?? string.Empty,
            Roles = roles.ToArray(),
            HotelId = user.HotelId,
            CreatedAt = user.CreatedAt,
            LastLogin = user.LastLoginAt,
            IsActive = user.IsActive,
            MustChangePassword = user.MustChangePassword,
            TwoFactorEnabled = user.TwoFactorEnabled
        };
    }
}