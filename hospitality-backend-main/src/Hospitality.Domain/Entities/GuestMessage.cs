namespace Hospitality.Domain.Entities;

/// <summary>
/// Mensaje generado por el motor de automatización y enviado al huésped a
/// través de un canal (WhatsApp/SMS/Email). ChannelReference guarda el ID
/// devuelto por el proveedor (mock: waid_/smsid_/emailid_).
/// </summary>
public class GuestMessage : BaseEntity
{
    public Guid HotelId { get; set; }
    public Hotel Hotel { get; set; } = null!;

    public Guid? RuleId { get; set; }
    public AutomationRule? Rule { get; set; }

    public Guid? ReservationId { get; set; }
    public Reservation? Reservation { get; set; }

    public string Recipient { get; set; } = string.Empty;
    public string Channel { get; set; } = "WhatsApp";
    public string Subject { get; set; } = string.Empty;
    public string? Body { get; set; }
    public bool IsSent { get; set; }
    public DateTime? SentAt { get; set; }
    public string? ChannelReference { get; set; }
    public string? Error { get; set; }
}