using Hospitality.Application.ChannelManager.Adapters;

namespace Hospitality.Application.ChannelManager.Adapters;

/// <summary>
/// ⚠️ ADAPTER DE DESARROLLO / SIMULADO (mock) — NO es integración real con
/// Expedia. Todos los métodos son locales y deterministas (Task.Delay como
/// latencia simulada; PullBookings devuelve lista vacía de prueba). NO hace
/// ninguna llamada HTTP a Expedia.
///
/// Ver README.md del módulo ChannelManager. La integración real sustituiría el
/// cuerpo por llamadas a la Expedia/EGENCIA API con credenciales reales,
/// manteniendo el contrato de <see cref="IChannelAdapter"/>.
/// </summary>
public class ExpediaAdapter : IChannelAdapter
{
    public string ChannelType => "Ota";
    public string DisplayName => "Expedia";

    public async Task<bool> TestConnectionAsync(string credentialsJson)
    {
        await Task.Delay(100);
        var options = new System.Text.Json.JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };
        var creds = System.Text.Json.JsonSerializer.Deserialize<ExpediaCredentials>(credentialsJson, options);
        return !string.IsNullOrEmpty(creds?.ApiKey) && !string.IsNullOrEmpty(creds?.HotelId);
    }

    public async Task<Dictionary<string, ChannelRoomTypeMap>> FetchRoomTypeMapsAsync(string credentialsJson)
    {
        await Task.Delay(200);
        return new Dictionary<string, ChannelRoomTypeMap>
        {
            ["EXP_DBL"] = new("EXP_DBL", "PLAN_STD"),
            ["EXP_SGL"] = new("EXP_SGL", "PLAN_STD"),
            ["EXP_STE"] = new("EXP_STE", "PLAN_PREMIUM")
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

    private record ExpediaCredentials(string ApiKey, string HotelId);
}