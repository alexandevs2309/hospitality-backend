using FluentValidation;

namespace Hospitality.Application.Auth.Commands.Validators;

public class LoginCommandValidator : AbstractValidator<LoginCommand>
{
    public LoginCommandValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("El correo electrónico es obligatorio.")
            .EmailAddress().WithMessage("El correo electrónico no es válido.")
            .MaximumLength(256).WithMessage("El correo electrónico no puede superar 256 caracteres.");

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("La contraseña es obligatoria.")
            .MaximumLength(128).WithMessage("La contraseña no puede superar 128 caracteres.");
    }
}

public class RegisterCommandValidator : AbstractValidator<RegisterCommand>
{
    public RegisterCommandValidator()
    {
        RuleFor(x => x.FirstName)
            .NotEmpty().WithMessage("El nombre es obligatorio.")
            .MaximumLength(100).WithMessage("El nombre no puede superar 100 caracteres.");

        RuleFor(x => x.LastName)
            .NotEmpty().WithMessage("El apellido es obligatorio.")
            .MaximumLength(100).WithMessage("El apellido no puede superar 100 caracteres.");

        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("El correo electrónico es obligatorio.")
            .EmailAddress().WithMessage("El correo electrónico no es válido.")
            .MaximumLength(256).WithMessage("El correo electrónico no puede superar 256 caracteres.");

        RuleFor(x => x.PhoneNumber)
            .MaximumLength(30).WithMessage("El teléfono no puede superar 30 caracteres.");

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("La contraseña es obligatoria.")
            .MinimumLength(8).WithMessage("La contraseña debe tener al menos 8 caracteres.")
            .MaximumLength(128).WithMessage("La contraseña no puede superar 128 caracteres.");

        RuleFor(x => x.ConfirmPassword)
            .NotEmpty().WithMessage("Debes confirmar la contraseña.")
            .Equal(x => x.Password).WithMessage("Las contraseñas no coinciden.");

        RuleFor(x => x.Department)
            .MaximumLength(100).WithMessage("El departamento no puede superar 100 caracteres.");

        RuleFor(x => x.Position)
            .MaximumLength(100).WithMessage("El cargo no puede superar 100 caracteres.");

        RuleFor(x => x.Language)
            .MaximumLength(10).WithMessage("El idioma no es válido.");

        RuleFor(x => x.TimeZone)
            .MaximumLength(100).WithMessage("La zona horaria no puede superar 100 caracteres.");
    }
}

public class UpdateUserCommandValidator : AbstractValidator<UpdateUserCommand>
{
    public UpdateUserCommandValidator()
    {
        RuleFor(x => x.FirstName)
            .NotEmpty().WithMessage("El nombre es obligatorio.")
            .MaximumLength(100).WithMessage("El nombre no puede superar 100 caracteres.");

        RuleFor(x => x.LastName)
            .NotEmpty().WithMessage("El apellido es obligatorio.")
            .MaximumLength(100).WithMessage("El apellido no puede superar 100 caracteres.");

        RuleFor(x => x.PhoneNumber)
            .MaximumLength(30).WithMessage("El teléfono no puede superar 30 caracteres.");

        RuleFor(x => x.ProfilePicture)
            .MaximumLength(500).WithMessage("La URL de la foto de perfil no puede superar 500 caracteres.");
    }
}

public class ChangePasswordCommandValidator : AbstractValidator<ChangePasswordCommand>
{
    public ChangePasswordCommandValidator()
    {
        RuleFor(x => x.CurrentPassword)
            .NotEmpty().WithMessage("La contraseña actual es obligatoria.")
            .MaximumLength(128).WithMessage("La contraseña no puede superar 128 caracteres.");

        RuleFor(x => x.NewPassword)
            .NotEmpty().WithMessage("La nueva contraseña es obligatoria.")
            .MinimumLength(8).WithMessage("La nueva contraseña debe tener al menos 8 caracteres.")
            .MaximumLength(128).WithMessage("La nueva contraseña no puede superar 128 caracteres.");

        RuleFor(x => x.ConfirmPassword)
            .NotEmpty().WithMessage("Debes confirmar la nueva contraseña.")
            .Equal(x => x.NewPassword).WithMessage("Las contraseñas no coinciden.");
    }
}

public class ForgotPasswordCommandValidator : AbstractValidator<ForgotPasswordCommand>
{
    public ForgotPasswordCommandValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("El correo electrónico es obligatorio.")
            .EmailAddress().WithMessage("El correo electrónico no es válido.")
            .MaximumLength(256).WithMessage("El correo electrónico no puede superar 256 caracteres.");
    }
}

public class ResetPasswordCommandValidator : AbstractValidator<ResetPasswordCommand>
{
    public ResetPasswordCommandValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("El correo electrónico es obligatorio.")
            .EmailAddress().WithMessage("El correo electrónico no es válido.")
            .MaximumLength(256).WithMessage("El correo electrónico no puede superar 256 caracteres.");

        RuleFor(x => x.Token)
            .NotEmpty().WithMessage("El token de restablecimiento es obligatorio.");

        RuleFor(x => x.NewPassword)
            .NotEmpty().WithMessage("La nueva contraseña es obligatoria.")
            .MinimumLength(8).WithMessage("La nueva contraseña debe tener al menos 8 caracteres.")
            .MaximumLength(128).WithMessage("La nueva contraseña no puede superar 128 caracteres.");

        RuleFor(x => x.ConfirmPassword)
            .NotEmpty().WithMessage("Debes confirmar la nueva contraseña.")
            .Equal(x => x.NewPassword).WithMessage("Las contraseñas no coinciden.");
    }
}

public class RefreshTokenCommandValidator : AbstractValidator<RefreshTokenCommand>
{
    public RefreshTokenCommandValidator()
    {
        RuleFor(x => x.RefreshToken)
            .NotEmpty().WithMessage("El token de refresco es obligatorio.")
            .MaximumLength(512).WithMessage("El token de refresco no puede superar 512 caracteres.");
    }
}

public class TwoFactorLoginCommandValidator : AbstractValidator<TwoFactorLoginCommand>
{
    public TwoFactorLoginCommandValidator()
    {
        RuleFor(x => x.TwoFactorToken)
            .NotEmpty().WithMessage("La sesión de segundo factor es obligatoria.");

        RuleFor(x => x.Code)
            .NotEmpty().WithMessage("El código es obligatorio.")
            .MaximumLength(12).WithMessage("El código no puede superar 12 caracteres.");
    }
}

public class TwoFactorVerifyCommandValidator : AbstractValidator<TwoFactorVerifyCommand>
{
    public TwoFactorVerifyCommandValidator()
    {
        RuleFor(x => x.Code)
            .NotEmpty().WithMessage("El código de verificación es obligatorio.")
            .MaximumLength(12).WithMessage("El código no puede superar 12 caracteres.");
    }
}

public class TwoFactorDisableCommandValidator : AbstractValidator<TwoFactorDisableCommand>
{
    public TwoFactorDisableCommandValidator()
    {
        RuleFor(x => x.Code)
            .NotEmpty().WithMessage("El código de verificación es obligatorio.")
            .MaximumLength(12).WithMessage("El código no puede superar 12 caracteres.");
    }
}

public class RevokeSessionCommandValidator : AbstractValidator<RevokeSessionCommand>
{
    public RevokeSessionCommandValidator()
    {
        RuleFor(x => x.SessionId)
            .NotEmpty().WithMessage("La sesión es obligatoria.");
    }
}