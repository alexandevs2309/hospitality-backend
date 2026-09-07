using Hospitality.Application.Common.DTOs;

namespace Hospitality.Application.Auth.Commands;

public class AuthResponse
{
    public string Token { get; set; } = string.Empty;
    public string RefreshToken { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public UserDto User { get; set; } = null!;
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
}

public interface IAuthService
{
    Task<AuthResponse?> LoginAsync(LoginCommand command);
    Task<AuthResponse> RegisterAsync(RegisterCommand command);
    Task<UserDto> GetCurrentUserAsync();
    Task<UserDto> UpdateCurrentUserAsync(UpdateUserCommand command);
    Task<AuthResult> ChangePasswordAsync(ChangePasswordCommand command);
    Task<bool> ForgotPasswordAsync(ForgotPasswordCommand command);
    Task<AuthResult> ResetPasswordAsync(ResetPasswordCommand command);
    Task<AuthResponse?> RefreshTokenAsync(RefreshTokenCommand command);
    Task LogoutAsync();
    Task<bool> CheckEmailAvailabilityAsync(string email);
}