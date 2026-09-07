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
    [EnableRateLimiting("auth")]
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
}