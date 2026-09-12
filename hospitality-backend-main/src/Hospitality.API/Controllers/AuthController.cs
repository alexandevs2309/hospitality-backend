using Hospitality.Application.Auth.Commands;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Hospitality.API.Controllers;

[ApiController]
[Route("api/v1/auth")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;

    public AuthController(IAuthService authService)
    {
        _authService = authService;
    }

    /// <summary>
    /// Inicia sesión en el sistema
    /// </summary>
    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting("login")]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<AuthResponse>> Login([FromBody] LoginCommand command)
    {
        var response = await _authService.LoginAsync(command);
        if (response == null)
        {
            return Unauthorized("Credenciales inválidas");
        }
        return Ok(response);
    }

    /// <summary>
    /// Registra un nuevo usuario
    /// </summary>
    [HttpPost("register")]
    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<AuthResponse>> Register([FromBody] RegisterCommand command)
    {
        var response = await _authService.RegisterAsync(command);
        return CreatedAtAction(nameof(GetCurrentUser), new { }, response);
    }

    /// <summary>
    /// Obtiene información del usuario actual
    /// </summary>
    [HttpGet("me")]
    [Authorize]
    [ProducesResponseType(typeof(UserDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<UserDto>> GetCurrentUser()
    {
        var user = await _authService.GetCurrentUserAsync();
        return Ok(user);
    }

    /// <summary>
    /// Actualiza información del usuario actual
    /// </summary>
    [HttpPut("me")]
    [Authorize]
    [ProducesResponseType(typeof(UserDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<UserDto>> UpdateCurrentUser([FromBody] UpdateUserCommand command)
    {
        var user = await _authService.UpdateCurrentUserAsync(command);
        return Ok(user);
    }

    /// <summary>
    /// Cambia contraseña del usuario actual
    /// </summary>
    [HttpPost("change-password")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordCommand command)
    {
        var result = await _authService.ChangePasswordAsync(command);
        if (result.Succeeded)
        {
            return Ok("Contraseña cambiada exitosamente");
        }
        return BadRequest(string.Join(", ", result.Errors.Select(e => e.Description)));
    }

    /// <summary>
    /// Solicita restablecimiento de contraseña
    /// </summary>
    [HttpPost("forgot-password")]
    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordCommand command)
    {
        await _authService.ForgotPasswordAsync(command);
        // Por seguridad, siempre devolvemos OK aunque el correo no exista.
        return Ok("Si el correo existe, se ha enviado un enlace para restablecer la contraseña");
    }

    /// <summary>
    /// Restablece contraseña con token
    /// </summary>
    [HttpPost("reset-password")]
    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordCommand command)
    {
        var result = await _authService.ResetPasswordAsync(command);
        if (result.Succeeded)
        {
            return Ok("Contraseña restablecida exitosamente");
        }
        return BadRequest(string.Join(", ", result.Errors.Select(e => e.Description)));
    }

    /// <summary>
    /// Refresca token JWT
    /// </summary>
    [HttpPost("refresh-token")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<AuthResponse>> RefreshToken([FromBody] RefreshTokenCommand command)
    {
        var response = await _authService.RefreshTokenAsync(command);
        if (response == null)
        {
            return Unauthorized("Token de refresco inválido o expirado");
        }
        return Ok(response);
    }

    /// <summary>
    /// Cierra sesión (revoca token)
    /// </summary>
    [HttpPost("logout")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> Logout()
    {
        await _authService.LogoutAsync();
        return Ok("Sesión cerrada exitosamente");
    }

    /// <summary>
    /// Verifica si el correo está disponible
    /// </summary>
    [HttpGet("check-email/{email}")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(bool), StatusCodes.Status200OK)]
    public async Task<ActionResult<bool>> CheckEmailAvailability(string email)
    {
        var isAvailable = await _authService.CheckEmailAvailabilityAsync(email);
        return Ok(isAvailable);
    }

    /// <summary>
    /// Verifica validez del token JWT
    /// </summary>
    [HttpGet("validate-token")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult ValidateToken()
    {
        // Si llegamos aquí, el token es válido (Authorize lo valida)
        return Ok(new { valid = true, message = "Token válido" });
    }

    /// <summary>
    /// Segundo paso del inicio de sesión (código TOTP o código de recuperación)
    /// </summary>
    [HttpPost("login/two-factor")]
    [AllowAnonymous]
    [EnableRateLimiting("login")]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<AuthResponse>> LoginTwoFactor([FromBody] TwoFactorLoginCommand command)
    {
        var response = await _authService.LoginWithTwoFactorAsync(command);
        if (response == null)
        {
            return Unauthorized("Código de verificación inválido o expirado");
        }
        return Ok(response);
    }

    /// <summary>
    /// Obtiene los datos para configurar la verificación en dos pasos (TOTP)
    /// </summary>
    [HttpGet("two-factor/setup")]
    [Authorize]
    [ProducesResponseType(typeof(TwoFactorSetupDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<TwoFactorSetupDto>> TwoFactorSetup()
    {
        var setup = await _authService.GetTwoFactorSetupAsync();
        return Ok(setup);
    }

    /// <summary>
    /// Confirma y activa la verificación en dos pasos
    /// </summary>
    [HttpPost("two-factor/verify")]
    [Authorize]
    [ProducesResponseType(typeof(TwoFactorVerifyResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<TwoFactorVerifyResult>> TwoFactorVerify([FromBody] TwoFactorVerifyCommand command)
    {
        var result = await _authService.VerifyTwoFactorAsync(command);
        if (!result.Succeeded)
        {
            return BadRequest("El código de verificación no es válido.");
        }
        return Ok(result);
    }

    /// <summary>
    /// Regenera los códigos de recuperación (exige un código TOTP vigente)
    /// </summary>
    [HttpPost("two-factor/recovery-codes")]
    [Authorize]
    [ProducesResponseType(typeof(TwoFactorVerifyResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<TwoFactorVerifyResult>> RegenerateRecoveryCodes([FromBody] TwoFactorRecoveryCodesCommand command)
    {
        var result = await _authService.RegenerateRecoveryCodesAsync(command);
        if (!result.Succeeded)
        {
            return BadRequest("El código de verificación no es válido.");
        }
        return Ok(result);
    }

    /// <summary>
    /// Desactiva la verificación en dos pasos
    /// </summary>
    [HttpPost("two-factor/disable")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> TwoFactorDisable([FromBody] TwoFactorDisableCommand command)
    {
        var disabled = await _authService.DisableTwoFactorAsync(command);
        if (!disabled)
        {
            return BadRequest("El código de verificación no es válido.");
        }
        return Ok("Verificación en dos pasos desactivada");
    }

    /// <summary>
    /// Lista las sesiones activas del usuario actual
    /// </summary>
    [HttpGet("sessions")]
    [Authorize]
    [ProducesResponseType(typeof(IReadOnlyList<SessionDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<SessionDto>>> GetSessions()
    {
        var sessions = await _authService.GetSessionsAsync();
        return Ok(sessions);
    }

    /// <summary>
    /// Revoca una sesión activa (excepto la actual)
    /// </summary>
    [HttpPost("sessions/revoke")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> RevokeSession([FromBody] RevokeSessionCommand command)
    {
        await _authService.RevokeSessionAsync(command);
        return Ok("Sesión revocada");
    }
}