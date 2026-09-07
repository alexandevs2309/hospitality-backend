using FluentValidation;

namespace Hospitality.Application.Common.DTOs.Validators;

public class PaginatedQueryValidator : AbstractValidator<PaginatedQuery>
{
    public PaginatedQueryValidator()
    {
        RuleFor(x => x.PageNumber)
            .GreaterThan(0).WithMessage("El número de página debe ser mayor que 0.");

        RuleFor(x => x.PageSize)
            .GreaterThan(0).WithMessage("El tamaño de página debe ser mayor que 0.")
            .LessThanOrEqualTo(100).WithMessage("El tamaño de página no puede superar 100.");

        RuleFor(x => x.Search)
            .MaximumLength(200).WithMessage("La búsqueda no puede superar 200 caracteres.");

        RuleFor(x => x.SortBy)
            .MaximumLength(50).WithMessage("El campo de ordenamiento no puede superar 50 caracteres.");

        RuleFor(x => x.SortDirection)
            .Must(direction => string.IsNullOrWhiteSpace(direction) ||
                               direction.Equals("asc", StringComparison.OrdinalIgnoreCase) ||
                               direction.Equals("desc", StringComparison.OrdinalIgnoreCase))
            .WithMessage("La dirección de ordenamiento debe ser 'asc' o 'desc'.");
    }
}