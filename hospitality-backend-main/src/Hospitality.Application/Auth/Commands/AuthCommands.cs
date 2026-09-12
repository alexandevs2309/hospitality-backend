namespace Hospitality.Application.Auth.Commands;

public class LoginCommand
{
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public bool RememberMe { get; set; }
}

public class RegisterCommand
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string ConfirmPassword { get; set; } = string.Empty;
    public string Department { get; set; } = string.Empty;
    public string Position { get; set; } = string.Empty;
    public string Language { get; set; } = "es";
    public string TimeZone { get; set; } = "UTC";
}

public class UpdateUserCommand
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public string? ProfilePicture { get; set; }
    public string Language { get; set; } = "en";
    public string TimeZone { get; set; } = "UTC";
    public bool EmailNotifications { get; set; } = true;
    public bool SmsNotifications { get; set; } = false;
}

public class ChangePasswordCommand
{
    public string CurrentPassword { get; set; } = string.Empty;
    public string NewPassword { get; set; } = string.Empty;
    public string ConfirmPassword { get; set; } = string.Empty;
}

public class ForgotPasswordCommand
{
    public string Email { get; set; } = string.Empty;
}

public class ResetPasswordCommand
{
    public string Email { get; set; } = string.Empty;
    public string Token { get; set; } = string.Empty;
    public string NewPassword { get; set; } = string.Empty;
    public string ConfirmPassword { get; set; } = string.Empty;
}

public class RefreshTokenCommand
{
    public string Token { get; set; } = string.Empty;
    public string RefreshToken { get; set; } = string.Empty;
}

public class TwoFactorLoginCommand
{
    public string TwoFactorToken { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
}

public class TwoFactorVerifyCommand
{
    public string Code { get; set; } = string.Empty;
}

public class TwoFactorDisableCommand
{
    public string Code { get; set; } = string.Empty;
}

public class TwoFactorRecoveryCodesCommand
{
    public string Code { get; set; } = string.Empty;
}

public class RevokeSessionCommand
{
    public Guid SessionId { get; set; }
}