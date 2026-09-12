using Hospitality.Domain.Enums;

namespace Hospitality.Domain.Entities;

/// <summary>
/// Plan de tarifas (Fase 1B): temporada con multiplicador de precio, estancia
/// mínima y política de cancelación. Se asigna a los RoomType del hotel; la
/// tarifa diaria efectiva = (override del calendario ?? BasePrice) × Multiplier.
/// </summary>
public class RatePlan : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public decimal Multiplier { get; set; } = 1m;
    public int MinStay { get; set; } = 1;
    public RefundabilityType Refundability { get; set; } = RefundabilityType.Flexible;
    public int CancellationDeadlineHours { get; set; } = 24;
    public bool IsDefault { get; set; }

    // Foreign keys
    public Guid HotelId { get; set; }

    // Navigation properties
    public Hotel Hotel { get; set; } = null!;
    public ICollection<RoomType> RoomTypes { get; set; } = new List<RoomType>();

    public bool IsNonRefundable => Refundability == RefundabilityType.NonRefundable;
}