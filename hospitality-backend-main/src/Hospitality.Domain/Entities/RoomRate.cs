namespace Hospitality.Domain.Entities;

/// <summary>
/// Tarifa de una noche para un tipo de habitación en una fecha concreta.
/// Motor de tarifas (Fase 1): por defecto se usa RoomType.BasePrice; si existe
/// un RoomRate para (Hotel, RoomType, Date) ese valor overridea la base.
/// </summary>
public class RoomRate : BaseEntity
{
    public Guid HotelId { get; set; }
    public Guid RoomTypeId { get; set; }
    public DateOnly Date { get; set; }
    public decimal Price { get; set; }

    public Hotel Hotel { get; set; } = null!;
    public RoomType RoomType { get; set; } = null!;
}