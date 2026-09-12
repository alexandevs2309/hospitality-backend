namespace Hospitality.Domain.Enums;

/// <summary>
/// Política de cancelación de un plan de tarifas (RatePlan).
/// </summary>
public enum RefundabilityType
{
    /// <summary>Cancelación gratuita hasta la hora límite (24h por defecto), luego cargo moderado.</summary>
    Flexible = 0,
    /// <summary>Cargo del 50% si se cancela dentro de la ventana límite.</summary>
    Moderate = 1,
    /// <summary>No reembolsable: se retiene el cargo de todas las noches.</summary>
    NonRefundable = 2
}