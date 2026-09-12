namespace Hospitality.Domain.Entities;

/// <summary>
/// Canal de ventas (Fase 1C: onboarding). Diferencia de dónde llega una reserva:
/// OTA (Booking, Expedia…), Agencia, Teléfono, Walk-in, Sitio web. La comisión
/// se usa más adelante para descontar el cobro del canal en el folio.
/// </summary>
public class Channel : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string ChannelType { get; set; } = "Ota"; // Ota, Agency, Phone, WalkIn, Site
    public decimal CommissionRate { get; set; }
    public bool IsActive { get; set; } = true;
    public string? CredentialsJson { get; set; }

    public Guid HotelId { get; set; }
    public Hotel Hotel { get; set; } = null!;

    public ICollection<ChannelMapping> Mappings { get; set; } = new List<ChannelMapping>();
}