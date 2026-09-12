using Hospitality.Application.Common.DTOs;

namespace Hospitality.Application.Auth.Commands;

public class AuthResponse
{
    public string Token { get; set; } = string.Empty;
    public string RefreshToken { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public UserDto User { get; set; } = null!;
    public bool RequiresTwoFactor { get; set; }
    public string? TwoFactorToken { get; set; }
}

public class TwoFactorSetupDto
{
    public string SharedKey { get; set; } = string.Empty;
    public string QrCodeSvg { get; set; } = string.Empty;
    public string KeyUri { get; set; } = string.Empty;
}

public class TwoFactorVerifyResult
{
    public bool Succeeded { get; set; }
    public string[]? RecoveryCodes { get; set; }
}

public class SessionDto
{
    public Guid Id { get; set; }
    public string DeviceName { get; set; } = string.Empty;
    public string? IpAddress { get; set; }
    public bool IsCurrent { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime LastUsedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
}

public class UserDto
{
    public Guid Id { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public string? ProfilePicture { get; set; }
    public string Department { get; set; } = string.Empty;
    public string Position { get; set; } = string.Empty;
    public string[] Roles { get; set; } = Array.Empty<string>();
    public Guid? HotelId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? LastLogin { get; set; }
    public bool IsActive { get; set; }
    public bool MustChangePassword { get; set; }
    public bool TwoFactorEnabled { get; set; }
}

public interface IAuthService
{
    Task<AuthResponse?> LoginAsync(LoginCommand command);
    Task<AuthResponse?> LoginWithTwoFactorAsync(TwoFactorLoginCommand command);
    Task<AuthResponse> RegisterAsync(RegisterCommand command);
    Task<UserDto> GetCurrentUserAsync();
    Task<UserDto> UpdateCurrentUserAsync(UpdateUserCommand command);
    Task<AuthResult> ChangePasswordAsync(ChangePasswordCommand command);
    Task<bool> ForgotPasswordAsync(ForgotPasswordCommand command);
    Task<AuthResult> ResetPasswordAsync(ResetPasswordCommand command);
    Task<AuthResponse?> RefreshTokenAsync(RefreshTokenCommand command);
    Task LogoutAsync();
    Task<bool> CheckEmailAvailabilityAsync(string email);
    Task<TwoFactorSetupDto> GetTwoFactorSetupAsync();
    Task<TwoFactorVerifyResult> VerifyTwoFactorAsync(TwoFactorVerifyCommand command);
    Task<TwoFactorVerifyResult> RegenerateRecoveryCodesAsync(TwoFactorRecoveryCodesCommand command);
    Task<bool> DisableTwoFactorAsync(TwoFactorDisableCommand command);
    Task<IReadOnlyList<SessionDto>> GetSessionsAsync();
    Task RevokeSessionAsync(RevokeSessionCommand command);
}