using FluentValidation;

namespace Hospitality.Application.Hotels.Commands.Validators;

public class CreateHotelCommandValidator : AbstractValidator<CreateHotelCommand>
{
    public CreateHotelCommandValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("El nombre del hotel es obligatorio.")
            .MaximumLength(200).WithMessage("El nombre no puede superar 200 caracteres.");

        RuleFor(x => x.Description)
            .MaximumLength(1000).WithMessage("La descripción no puede superar 1000 caracteres.");

        RuleFor(x => x.Address)
            .NotEmpty().WithMessage("La dirección es obligatoria.")
            .MaximumLength(300).WithMessage("La dirección no puede superar 300 caracteres.");

        RuleFor(x => x.PhoneNumber)
            .MaximumLength(30).WithMessage("El teléfono no puede superar 30 caracteres.");

        RuleFor(x => x.Email)
            .EmailAddress().WithMessage("El correo electrónico no es válido.")
            .MaximumLength(256).WithMessage("El correo electrónico no puede superar 256 caracteres.");

        RuleFor(x => x.Website)
            .MaximumLength(300).WithMessage("El sitio web no puede superar 300 caracteres.");

        RuleFor(x => x.StarRating)
            .InclusiveBetween(1, 5).WithMessage("La calificación debe estar entre 1 y 5 estrellas.");

        RuleFor(x => x.TotalRooms)
            .GreaterThanOrEqualTo(0).WithMessage("El total de habitaciones no puede ser negativo.");

        RuleFor(x => x.City)
            .NotEmpty().WithMessage("La ciudad es obligatoria.")
            .MaximumLength(150).WithMessage("La ciudad no puede superar 150 caracteres.");

        RuleFor(x => x.Country)
            .NotEmpty().WithMessage("El país es obligatorio.")
            .MaximumLength(150).WithMessage("El país no puede superar 150 caracteres.");

        RuleFor(x => x.TimeZone)
            .NotEmpty().WithMessage("La zona horaria es obligatoria.")
            .MaximumLength(100).WithMessage("La zona horaria no puede superar 100 caracteres.");
    }
}

public class UpdateHotelCommandValidator : AbstractValidator<UpdateHotelCommand>
{
    public UpdateHotelCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty().WithMessage("El ID del hotel es obligatorio.");

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("El nombre del hotel es obligatorio.")
            .MaximumLength(200).WithMessage("El nombre no puede superar 200 caracteres.");

        RuleFor(x => x.Description)
            .MaximumLength(1000).WithMessage("La descripción no puede superar 1000 caracteres.");

        RuleFor(x => x.Address)
            .NotEmpty().WithMessage("La dirección es obligatoria.")
            .MaximumLength(300).WithMessage("La dirección no puede superar 300 caracteres.");

        RuleFor(x => x.PhoneNumber)
            .MaximumLength(30).WithMessage("El teléfono no puede superar 30 caracteres.");

        RuleFor(x => x.Email)
            .EmailAddress().WithMessage("El correo electrónico no es válido.")
            .MaximumLength(256).WithMessage("El correo electrónico no puede superar 256 caracteres.");

        RuleFor(x => x.Website)
            .MaximumLength(300).WithMessage("El sitio web no puede superar 300 caracteres.");

        RuleFor(x => x.StarRating)
            .InclusiveBetween(1, 5).WithMessage("La calificación debe estar entre 1 y 5 estrellas.");

        RuleFor(x => x.TotalRooms)
            .GreaterThanOrEqualTo(0).WithMessage("El total de habitaciones no puede ser negativo.");

        RuleFor(x => x.City)
            .NotEmpty().WithMessage("La ciudad es obligatoria.")
            .MaximumLength(150).WithMessage("La ciudad no puede superar 150 caracteres.");

        RuleFor(x => x.Country)
            .NotEmpty().WithMessage("El país es obligatorio.")
            .MaximumLength(150).WithMessage("El país no puede superar 150 caracteres.");

        RuleFor(x => x.TimeZone)
            .NotEmpty().WithMessage("La zona horaria es obligatoria.")
            .MaximumLength(100).WithMessage("La zona horaria no puede superar 100 caracteres.");
    }
}