using Hospitality.Application.ChannelManager.Adapters;

namespace Hospitality.Application.ChannelManager.Commands;

public class ChannelMappingDto
{
    public Guid Id { get; set; }
    public Guid ChannelId { get; set; }
    public string ChannelName { get; set; } = string.Empty;
    public Guid RoomTypeId { get; set; }
    public string RoomTypeName { get; set; } = string.Empty;
    public string ChannelRoomCode { get; set; } = string.Empty;
    public string? ChannelRatePlanCode { get; set; }
    public bool IsActive { get; set; }
}

public class UpsertChannelMappingCommand
{
    public Guid ChannelId { get; set; }
    public Guid RoomTypeId { get; set; }
    public string ChannelRoomCode { get; set; } = string.Empty;
    public string? ChannelRatePlanCode { get; set; }
    public bool IsActive { get; set; } = true;
}

public class ChannelCredentialsDto
{
    public Guid ChannelId { get; set; }
    public string CredentialsJson { get; set; } = string.Empty;
}

public class PushResultDto
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public int ItemsPushed { get; set; }
}

public class BookingPullDto
{
    public string ExternalBookingId { get; set; } = string.Empty;
    public string ChannelRoomCode { get; set; } = string.Empty;
    public string ChannelRatePlanCode { get; set; } = string.Empty;
    public DateTime CheckIn { get; set; }
    public DateTime CheckOut { get; set; }
    public int Adults { get; set; }
    public int Children { get; set; }
    public string GuestName { get; set; } = string.Empty;
    public string GuestEmail { get; set; } = string.Empty;
    public string GuestPhone { get; set; } = string.Empty;
    public decimal TotalPrice { get; set; }
    public string Currency { get; set; } = string.Empty;
    public Dictionary<string, object> RawData { get; set; } = new();
}

public interface IChannelManagerService
{
    Task<bool> TestConnectionAsync(Guid channelId);
    Task<Dictionary<string, ChannelRoomTypeMap>> FetchRoomTypeMapsAsync(Guid channelId);
    Task<PushResultDto> PushAvailabilityAsync(Guid channelId, DateTime from, DateTime to);
    Task<PushResultDto> PushRatesAsync(Guid channelId, DateTime from, DateTime to);
    Task<List<BookingPullDto>> PullBookingsAsync(Guid channelId, DateTime from, DateTime to);
    Task<PushResultDto> CreateMappingsAsync(Guid channelId);
    Task<List<ChannelMappingDto>> GetMappingsAsync(Guid channelId);
    Task<ChannelMappingDto> UpsertMappingAsync(UpsertChannelMappingCommand command);
    Task DeleteMappingAsync(Guid mappingId);
    Task<ChannelCredentialsDto> GetCredentialsAsync(Guid channelId);
    Task UpdateCredentialsAsync(ChannelCredentialsDto command);
}