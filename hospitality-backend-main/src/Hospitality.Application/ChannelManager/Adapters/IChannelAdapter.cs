namespace Hospitality.Application.ChannelManager.Adapters;

public record ChannelRoomTypeMap(string ChannelRoomCode, string ChannelRatePlanCode);

public record AvailabilityUpdate(
    string ChannelRoomCode,
    string ChannelRatePlanCode,
    DateTime Date,
    int AvailableCount,
    decimal? PriceOverride = null,
    decimal? MinStayOverride = null,
    bool ClosedToArrival = false,
    bool ClosedToDeparture = false);

public record RateUpdate(
    string ChannelRoomCode,
    string ChannelRatePlanCode,
    DateTime StartDate,
    DateTime EndDate,
    decimal BasePrice,
    decimal? MinStay = null,
    string? Currency = null);

public record BookingPullResult(
    string ExternalBookingId,
    string ChannelRoomCode,
    string ChannelRatePlanCode,
    DateTime CheckIn,
    DateTime CheckOut,
    int Adults,
    int Children,
    string GuestName,
    string GuestEmail,
    string GuestPhone,
    decimal TotalPrice,
    string Currency,
    Dictionary<string, object> RawData);

public interface IChannelAdapter
{
    string ChannelType { get; }
    string DisplayName { get; }

    Task<bool> TestConnectionAsync(string credentialsJson);
    Task<Dictionary<string, ChannelRoomTypeMap>> FetchRoomTypeMapsAsync(string credentialsJson);
    Task PushAvailabilityAsync(string credentialsJson, IEnumerable<AvailabilityUpdate> updates);
    Task PushRatesAsync(string credentialsJson, IEnumerable<RateUpdate> updates);
    Task<List<BookingPullResult>> PullBookingsAsync(string credentialsJson, DateTime from, DateTime to);
    Task<bool> AcknowledgeBookingAsync(string credentialsJson, string externalBookingId, bool confirmed);
}