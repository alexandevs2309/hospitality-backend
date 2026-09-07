using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Hospitality.Application.Auth.Commands;
using Hospitality.Application.Common.DTOs;
using Hospitality.Application.Common.Interfaces;
using Hospitality.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;

namespace Hospitality.Application.Auth.Services;

public class AuthService : IAuthService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly IConfiguration _configuration;
    private readonly ILogger<AuthService> _logger;

    public AuthService(
        UserManager<ApplicationUser> userManager,
        IApplicationDbContext context,
        ICurrentUserService currentUserService,
        IConfiguration configuration,
        ILogger<AuthService> logger)
    {
        _userManager = userManager;
        _context = context;
        _currentUserService = currentUserService;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<AuthResponse?> LoginAsync(LoginCommand command)
    {
        var user = await _userManager.FindByEmailAsync(command.Email);
        if (user is null || !user.IsActive || user.IsDeleted)
        {
            return null;
        }

        if (await _userManager.IsLockedOutAsync(user))
        {
            _logger.LogInformation("Cuenta bloqueada temporalmente por intentos fallidos: {Email}", command.Email);
            return null;
        }

        var passwordValid = await _userManager.CheckPasswordAsync(user, command.Password);
        if (!passwordValid)
        {
            // Incrementa el contador de intentos fallidos (configurado en 5 intentos / 15 minutos de bloqueo).
            await _userManager.AccessFailedAsync(user);
            _logger.LogInformation("Intento de login fallido para {Email}", command.Email);
            return null;
        }

        await _userManager.ResetAccessFailedCountAsync(user);
        user.LastLoginAt = DateTime.UtcNow;
        user.UpdatedAt = DateTime.UtcNow;
        await _userManager.UpdateAsync(user);
        _logger.LogInformation("Login exitoso para el usuario {Email}", command.Email);

        return await CreateAuthResponseAsync(user);
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
            Language = "es",
            IsActive = true,
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
        return result.Succeeded
            ? AuthResult.Success()
            : AuthResult.Failure(result.Errors.Select(e => e.Description).ToArray());
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
        return result.Succeeded
            ? AuthResult.Success()
            : AuthResult.Failure(result.Errors.Select(e => e.Description).ToArray());
    }

    public async Task<AuthResponse?> RefreshTokenAsync(RefreshTokenCommand command)
    {
        var user = await _context.Users
            .FirstOrDefaultAsync(u => !u.IsDeleted &&
                                      u.RefreshToken == command.RefreshToken &&
                                      u.RefreshTokenExpiry.HasValue &&
                                      u.RefreshTokenExpiry > DateTime.UtcNow);
        if (user is null)
        {
            return null;
        }

        return await CreateAuthResponseAsync(user);
    }

    public async Task LogoutAsync()
    {
        var user = await GetCurrentUserOrThrowAsync();
        user.RefreshToken = null;
        user.RefreshTokenExpiry = null;
        user.UpdatedAt = DateTime.UtcNow;
        await _userManager.UpdateAsync(user);
        _logger.LogInformation("Sesión cerrada para {UserId}", user.Id);
    }

    public async Task<bool> CheckEmailAvailabilityAsync(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return false;
        }

        return !await _context.Users.AnyAsync(u => u.Email == email.Trim());
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

    private async Task<AuthResponse> CreateAuthResponseAsync(ApplicationUser user)
    {
        var roles = await _userManager.GetRolesAsync(user);
        var expiryMinutes = GetTokenExpiryMinutes();
        var tokenExpiresAt = DateTime.UtcNow.AddMinutes(expiryMinutes);

        return new AuthResponse
        {
            Token = GenerateJwtToken(user, roles, expiryMinutes),
            RefreshToken = await IssueRefreshTokenAsync(user),
            ExpiresAt = tokenExpiresAt,
            User = MapUser(user, roles)
        };
    }

    private string GenerateJwtToken(ApplicationUser user, IList<string> roles, int expiryMinutes)
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
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        if (user.HotelId.HasValue)
        {
            claims.Add(new Claim("hotel_id", user.HotelId.Value.ToString()));
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

    private async Task<string> IssueRefreshTokenAsync(ApplicationUser user)
    {
        var refreshToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
        user.RefreshToken = refreshToken;
        user.RefreshTokenExpiry = DateTime.UtcNow.AddDays(GetRefreshTokenExpiryDays());
        await _userManager.UpdateAsync(user);
        return refreshToken;
    }

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
            IsActive = user.IsActive
        };
    }
}