using Hospitality.Application.ChannelManager.Adapters;
using System.Text.Json.Serialization;

namespace Hospitality.Application.ChannelManager.Adapters;

/// <summary>
/// ⚠️ ADAPTER DE DESARROLLO / SIMULADO (mock) — NO es integración real con
/// Booking.com. Todos los métodos son deterministas y locales (Task.Delay como
/// latencia simulada; PullBookings devuelve reservas de prueba). NO hace
/// ninguna llamada HTTP a Booking.com.
///
/// Sirve como scaffolding de la interfaz <see cref="IChannelAdapter"/> y para
/// demo/onboarding con datos de prueba. La integración real reemplazaría el
/// cuerpo de estos métodos por llamadas a la Booking.com Connectivity/OTA API
/// (con credenciales reales), manteniendo el mismo contrato. NO fundir este
/// archivo con lógica de producción real de OTA.
/// </summary>
public class BookingComAdapter : IChannelAdapter
{
    public string ChannelType => "Ota";
    public string DisplayName => "Booking.com";

    public async Task<bool> TestConnectionAsync(string credentialsJson)
    {
        await Task.Delay(100);
        var options = new System.Text.Json.JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };
        var creds = System.Text.Json.JsonSerializer.Deserialize<BookingComCredentials>(credentialsJson, options);
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
        var options = new System.Text.Json.JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };
        var creds = System.Text.Json.JsonSerializer.Deserialize<BookingComCredentials>(credentialsJson, options);
        if (creds == null || string.IsNullOrEmpty(creds.ApiKey))
        {
            return new List<BookingPullResult>();
        }

        var start = DateTime.SpecifyKind(from.Date, DateTimeKind.Utc);
        var end = DateTime.SpecifyKind(to.Date, DateTimeKind.Utc);
        var results = new List<BookingPullResult>();

        // OTA simulada: reservas deterministas dentro del rango solicitado.
        var b1In = start.AddDays(3);
        if (b1In < end)
        {
            results.Add(new BookingPullResult(
                ExternalBookingId: $"BKG{b1In:yyyyMMdd}01",
                ChannelRoomCode: "DBL_STD",
                ChannelRatePlanCode: "RATE_FLEX",
                CheckIn: b1In,
                CheckOut: b1In.AddDays(2),
                Adults: 2,
                Children: 0,
                GuestName: "María Fernández",
                GuestEmail: $"mfernandez{b1In.Day:00}@ota-sim.test",
                GuestPhone: "+18095550101",
                TotalPrice: 208.80m,
                Currency: "MXN",
                RawData: new Dictionary<string, object>
                {
                    ["platform"] = "booking.com",
                    ["gateway"] = "mock"
                })
            );
        }

        var b2In = start.AddDays(6);
        if (b2In < end)
        {
            results.Add(new BookingPullResult(
                ExternalBookingId: $"BKG{b2In:yyyyMMdd}02",
                ChannelRoomCode: "DBL_STD",
                ChannelRatePlanCode: "RATE_NONREF",
                CheckIn: b2In,
                CheckOut: b2In.AddDays(1),
                Adults: 1,
                Children: 0,
                GuestName: "Carlos Pérez",
                GuestEmail: $"cperez{b2In.Day:00}@ota-sim.test",
                GuestPhone: "+18095550202",
                TotalPrice: 104.40m,
                Currency: "MXN",
                RawData: new Dictionary<string, object>
                {
                    ["platform"] = "booking.com",
                    ["gateway"] = "mock",
                    ["nonRefundable"] = true
                })
            );
        }

        return results;
    }

    public async Task<bool> AcknowledgeBookingAsync(string credentialsJson, string externalBookingId, bool confirmed)
    {
        await Task.Delay(100);
        return true;
    }

    private record BookingComCredentials(string ApiKey, string PropertyId);
}