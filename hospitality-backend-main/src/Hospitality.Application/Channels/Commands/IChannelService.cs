namespace Hospitality.Application.Channels.Commands;

public class ChannelDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string ChannelType { get; set; } = "Ota";
    public decimal CommissionRate { get; set; }
    public bool IsActive { get; set; }
    public string? CredentialsJson { get; set; }
    public string ChannelTypeLabel => ChannelType switch
    {
        "Agency" => "Agencia",
        "Phone" => "Teléfono",
        "WalkIn" => "Walk-in",
        "Site" => "Sitio web",
        _ => "OTA"
    };
}

public class UpsertChannelCommand
{
    public Guid HotelId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string ChannelType { get; set; } = "Ota";
    public decimal CommissionRate { get; set; }
    public bool IsActive { get; set; } = true;
    public string? CredentialsJson { get; set; }
}

public interface IChannelService
{
    Task<IReadOnlyList<ChannelDto>> GetChannelsAsync(Guid hotelId);
    Task<ChannelDto> CreateChannelAsync(UpsertChannelCommand command);
    Task<ChannelDto> UpdateChannelAsync(Guid id, UpsertChannelCommand command);
    Task DeleteChannelAsync(Guid id);
}