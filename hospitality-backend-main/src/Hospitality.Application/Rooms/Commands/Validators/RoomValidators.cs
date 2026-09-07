using FluentValidation;
using Hospitality.Domain.Enums;

namespace Hospitality.Application.Rooms.Commands.Validators;

public class CreateRoomCommandValidator : AbstractValidator<CreateRoomCommand>
{
    public CreateRoomCommandValidator()
    {
        RuleFor(x => x.RoomNumber)
            .NotEmpty().WithMessage("El número de habitación es obligatorio.")
            .MaximumLength(20).WithMessage("El número de habitación no puede superar 20 caracteres.");

        RuleFor(x => x.Floor)
            .GreaterThanOrEqualTo(0).WithMessage("La planta no puede ser negativa.")
            .LessThanOrEqualTo(200).WithMessage("La planta no puede superar 200.");

        RuleFor(x => x.Description)
            .MaximumLength(500).WithMessage("La descripción no puede superar 500 caracteres.");

        RuleFor(x => x.RoomTypeId)
            .NotEmpty().WithMessage("El tipo de habitación es obligatorio.");

        RuleFor(x => x.HotelId)
            .NotEmpty().WithMessage("El ID del hotel es obligatorio.");
    }
}

public class UpdateRoomCommandValidator : AbstractValidator<UpdateRoomCommand>
{
    public UpdateRoomCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty().WithMessage("El ID de la habitación es obligatorio.");

        RuleFor(x => x.RoomNumber)
            .NotEmpty().WithMessage("El número de habitación es obligatorio.")
            .MaximumLength(20).WithMessage("El número de habitación no puede superar 20 caracteres.");

        RuleFor(x => x.Floor)
            .GreaterThanOrEqualTo(0).WithMessage("La planta no puede ser negativa.")
            .LessThanOrEqualTo(200).WithMessage("La planta no puede superar 200.");

        RuleFor(x => x.Description)
            .MaximumLength(500).WithMessage("La descripción no puede superar 500 caracteres.");

        RuleFor(x => x.RoomTypeId)
            .NotEmpty().WithMessage("El tipo de habitación es obligatorio.");

        RuleFor(x => x.HotelId)
            .NotEmpty().WithMessage("El ID del hotel es obligatorio.");
    }
}

public class UpdateRoomStatusCommandValidator : AbstractValidator<UpdateRoomStatusCommand>
{
    public UpdateRoomStatusCommandValidator()
    {
        RuleFor(x => x.Status)
            .NotEmpty().WithMessage("El estado es obligatorio.")
            .Must(value => Enum.TryParse<RoomStatus>(value, true, out _))
            .WithMessage("El estado de la habitación no es válido.");

        RuleFor(x => x.Notes)
            .MaximumLength(500).WithMessage("Las notas no pueden superar 500 caracteres.");
    }
}

public class RequestMaintenanceCommandValidator : AbstractValidator<RequestMaintenanceCommand>
{
    public RequestMaintenanceCommandValidator()
    {
        RuleFor(x => x.Description)
            .NotEmpty().WithMessage("La descripción del mantenimiento es obligatoria.")
            .MaximumLength(500).WithMessage("La descripción no puede superar 500 caracteres.");

        RuleFor(x => x.Priority)
            .NotEmpty().WithMessage("La prioridad es obligatoria.")
            .Must(value => Enum.TryParse<MaintenancePriority>(value, true, out _))
            .WithMessage("La prioridad no es válida.")
            .MaximumLength(20).WithMessage("La prioridad no puede superar 20 caracteres.");
    }
}