namespace Hospitality.Domain.Entities;

/// <summary>
/// Regla de automatización (Fase 3): cuando ocurre el evento de reserva
/// TriggerEvent, envía un mensaje al huésped por el canal Channel usando el
/// template Template (con placeholders {Huesped}, {Folio}, {Llegada}, …).
/// </summary>
public class AutomationRule : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string TriggerEvent { get; set; } = string.Empty; // ReservationCreated/Confirmed/CheckedIn/CheckedOut/Cancelled
    public string Channel { get; set; } = "WhatsApp";        // WhatsApp, Sms, Email
    public string Template { get; set; } = string.Empty;
    public bool IsEnabled { get; set; } = true;

    public Guid HotelId { get; set; }
    public Hotel Hotel { get; set; } = null!;
}