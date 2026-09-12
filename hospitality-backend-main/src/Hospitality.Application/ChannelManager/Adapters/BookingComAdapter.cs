using Hospitality.Application.ChannelManager.Adapters;

namespace Hospitality.Application.ChannelManager.Adapters;

public class BookingComAdapter : IChannelAdapter
{
    public string ChannelType => "Ota";
    public string DisplayName => "Booking.com";

    public async Task<bool> TestConnectionAsync(string credentialsJson)
    {
        await Task.Delay(100);
        var creds = System.Text.Json.JsonSerializer.Deserialize<BookingComCredentials>(credentialsJson);
        return !string.IsNullOrEmpty(creds?.ApiKey) && !string.IsNullOrEmpty(creds?.PropertyId);
    }

    public async Task<Dictionary<string, ChannelRoomTypeMap>> FetchRoomTypeMapsAsync(string credentialsJson)
    {
        await Task.Delay(200);
        return new Dictionary<string, ChannelRoomTypeMap>
        {
            ["DBL_STD"] = new("DBL_STD", "RATE_FLEX"),
            ["SGL_STD"] = new("SGL_STD", "RATE_FLEX"),
            ["STE_LUX"] = new("STE_LUX", "RATE_NONREF")
        };
    }

    public async Task PushAvailabilityAsync(string credentialsJson, IEnumerable<AvailabilityUpdate> updates)
    {
        await Task.Delay(50 * updates.Count());
    }

    public async Task PushRatesAsync(string credentialsJson, IEnumerable<RateUpdate> updates)
    {
        await Task.Delay(50 * updates.Count());
    }

    public async Task<List<BookingPullResult>> PullBookingsAsync(string credentialsJson, DateTime from, DateTime to)
    {
        await Task.Delay(300);
        return new List<BookingPullResult>();
    }

    public async Task<bool> AcknowledgeBookingAsync(string credentialsJson, string externalBookingId, bool confirmed)
    {
        await Task.Delay(100);
        return true;
    }

    private record BookingComCredentials(string ApiKey, string PropertyId);
}