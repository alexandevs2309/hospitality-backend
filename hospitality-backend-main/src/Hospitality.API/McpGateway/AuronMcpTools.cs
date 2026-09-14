using System.ComponentModel;
using System.Globalization;
using System.Text;
using Hospitality.Application.Common.Helpers;
using Hospitality.Application.Common.Interfaces;
using Hospitality.Application.Hotels.Queries;
using Hospitality.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using ModelContextProtocol.Server;

namespace Hospitality.API.McpGateway;

/// <summary>
/// MCP Gateway — capa de agentes.
/// Herramientas que un asistente IA (opencode, Claude, etc.) puede invocar
/// en lenguaje natural para consultar el hotel del operador autenticado,
/// usando los mismos servicios del backend. El alcance de datos se limita
/// al hotel del token (ICurrentUserService.HotelId).
/// </summary>
[McpServerToolType]
public static class AuronMcpTools
{
    private static Guid? ResolveHotel(ICurrentUserService user)
        => user.HotelId;

    [McpServerTool, Description(
        "Información del hotel del usuario autenticado: nombre, moneda, dirección, " +
        "teléfono y categorías de habitación con precio base y capacidad.")]
    public static async Task<string> get_hotel_info(
        IApplicationDbContext context,
        ICurrentUserService user)
    {
        var hotelId = ResolveHotel(user);
        if (!hotelId.HasValue)
        {
            return "No se pudo determinar el hotel del usuario.";
        }

        var hotel = await context.Hotels.FindAsync(hotelId.Value);
        if (hotel == null)
        {
            return "Hotel no encontrado.";
        }

        var roomTypes = await context.RoomTypes
            .Where(rt => rt.HotelId == hotelId.Value)
            .Include(rt => rt.RatePlan)
            .OrderBy(rt => rt.BasePrice)
            .ToListAsync();

        var sb = new StringBuilder();
        sb.AppendLine($"Hotel: {hotel.Name} ({hotel.StarRating}★)");
        sb.AppendLine($"Dirección: {hotel.Address}");
        sb.AppendLine($"Teléfono: {hotel.PhoneNumber}");
        sb.AppendLine($"Moneda: {hotel.Currency}");
        sb.AppendLine($"Habitaciones: {hotel.TotalRooms}");
        sb.AppendLine("Categorías:");
        foreach (var rt in roomTypes)
        {
            var plan = rt.RatePlan != null ? $" (plan '{rt.RatePlan.Name}', x{rt.RatePlan.Multiplier})" : string.Empty;
            sb.AppendLine($"  - {rt.Name}: {FormatMoney(rt.BasePrice, hotel.Currency)}{plan}, capacidad {rt.Capacity}");
        }
        return sb.ToString();
    }

    [McpServerTool, Description(
        "Unidades libres (disponibilidad) por noche y por categoría de habitación para " +
        "un rango de fechas. Responde preguntas como '¿cuántas habitaciones libres tengo " +
        "este fin de semana?' o '¿hay disponibilidad del 10 al 12 de octubre?'.")]
    public static async Task<string> get_availability(
        IApplicationDbContext context,
        ICurrentUserService user,
        [Description("Fecha inicial en formato YYYY-MM-DD.")] string from,
        [Description("Fecha final incluida en formato YYYY-MM-DD.")] string to,
        [Description("Opcional: nombre de la categoría (p. ej. 'Doble'). Vacío para todas.")] string? roomTypeName = null)
    {
        var hotelId = ResolveHotel(user);
        if (!hotelId.HasValue)
        {
            return "No se pudo determinar el hotel del usuario.";
        }

        if (!TryParseDate(from, out var start) || !TryParseDate(to, out var end) || end < start)
        {
            return "Rango de fechas inválido. Use el formato YYYY-MM-DD y que 'to' sea >= 'from'.";
        }

        var roomTypes = (await context.RoomTypes
            .Where(rt => rt.HotelId == hotelId.Value)
            .OrderBy(rt => rt.BasePrice)
            .ToListAsync())
            .Where(rt => string.IsNullOrEmpty(roomTypeName) || rt.Name.Contains(roomTypeName!, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (!roomTypes.Any())
        {
            return string.IsNullOrEmpty(roomTypeName)
                ? "El hotel no tiene categorías de habitación."
                : $"No se encontró la categoría '{roomTypeName}'.";
        }

        var sb = new StringBuilder();
        sb.AppendLine($"Disponibilidad del {start:yyyy-MM-dd} al {end:yyyy-MM-dd}:");
        foreach (var rt in roomTypes)
        {
            sb.AppendLine($"  {rt.Name}:");
            for (var d = start; d <= end; d = d.AddDays(1))
            {
                var date = DateTime.SpecifyKind(d.Date, DateTimeKind.Utc);
                var free = await AvailabilityHelper.CalculateAvailableRoomsAsync(context, rt.Id, date);
                sb.AppendLine($"    - {date:ddd dd MMM}: {free} {(free == 1 ? "unidad libre" : "unidades libres")}");
            }
        }
        return sb.ToString();
    }

    [McpServerTool, Description(
        "Precio por noche de cada categoría de habitación para cada fecha de un rango." +
        "Responde '¿cuánto cuesta una habitación el sábado?'.")]
    public static async Task<string> get_rates(
        IApplicationDbContext context,
        ICurrentUserService user,
        [Description("Fecha inicial en formato YYYY-MM-DD.")] string from,
        [Description("Fecha final incluida en formato YYYY-MM-DD.")] string to)
    {
        var hotelId = ResolveHotel(user);
        if (!hotelId.HasValue)
        {
            return "No se pudo determinar el hotel del usuario.";
        }

        if (!TryParseDate(from, out var start) || !TryParseDate(to, out var end) || end < start)
        {
            return "Rango de fechas inválido. Use el formato YYYY-MM-DD.";
        }

        var roomTypes = await context.RoomTypes
            .Where(rt => rt.HotelId == hotelId.Value)
            .Include(rt => rt.RatePlan)
            .ToListAsync();

        var currency = (await context.Hotels.FindAsync(hotelId.Value))?.Currency ?? "USD";

        var startDate = DateOnly.FromDateTime(start);
        var endDate = DateOnly.FromDateTime(end);

        var rates = await context.RoomRates
            .Where(rr => rr.HotelId == hotelId.Value && rr.Date >= startDate && rr.Date <= endDate)
            .ToListAsync();

        var sb = new StringBuilder();
        sb.AppendLine($"Tarifas del {start:yyyy-MM-dd} al {end:yyyy-MM-dd} (por noche):");
        foreach (var rt in roomTypes.OrderBy(x => x.BasePrice))
        {
            sb.AppendLine($"  {rt.Name}:");
            for (var d = start; d <= end; d = d.AddDays(1))
            {
                var specific = rates.FirstOrDefault(rr => rr.RoomTypeId == rt.Id && rr.Date == DateOnly.FromDateTime(d));
                var price = specific?.Price ??
                    (rt.RatePlan != null ? rt.BasePrice * rt.RatePlan.Multiplier : rt.BasePrice);
                sb.AppendLine($"    - {d:ddd dd MMM}: {FormatMoney(price, currency)}");
            }
        }
        return sb.ToString();
    }

    [McpServerTool, Description(
        "Lista las reservas del hotel. Estatus opcionales: Pending, Confirmed, CheckedIn, " +
        "CheckedOut, Cancelled, NoShow. Responde '¿qué llegadas tengo mañana?'.")]
    public static async Task<string> list_reservations(
        IApplicationDbContext context,
        ICurrentUserService user,
        [Description("Opcional: filtra por estatus (Confirmed, CheckedIn, CheckedOut, Pending, Cancelled, NoShow).")] string? status = null,
        [Description("Opcional: fecha inicial YYYY-MM-DD para filtrar por llegada.")] string? from = null,
        [Description("Opcional: fecha final YYYY-MM-DD para filtrar por llegada.")] string? to = null)
    {
        var hotelId = ResolveHotel(user);
        if (!hotelId.HasValue)
        {
            return "No se pudo determinar el hotel del usuario.";
        }

        ReservationStatus? statusFilter = null;
        if (!string.IsNullOrEmpty(status))
        {
            if (!Enum.TryParse(status, true, out ReservationStatus parsed))
            {
                return $"Estatus inválido. Use uno de: {string.Join(", ", Enum.GetNames<ReservationStatus>())}.";
            }
            statusFilter = parsed;
        }

        DateTime fromDate = default, toDate = default;
        bool hasFrom = false, hasTo = false;
        if (!string.IsNullOrEmpty(from) && TryParseDate(from, out fromDate))
        {
            hasFrom = true;
        }
        else if (!string.IsNullOrEmpty(from))
        {
            return "Fecha 'from' inválida. Use YYYY-MM-DD.";
        }
        if (!string.IsNullOrEmpty(to) && TryParseDate(to, out toDate))
        {
            hasTo = true;
        }
        else if (!string.IsNullOrEmpty(to))
        {
            return "Fecha 'to' inválida. Use YYYY-MM-DD.";
        }

        var query = context.Reservations
            .Include(r => r.Guest)
            .Include(r => r.Room).ThenInclude(r => r.RoomType)
            .Include(r => r.Hotel)
            .Where(r => r.HotelId == hotelId.Value);

        if (statusFilter.HasValue)
        {
            query = query.Where(r => r.Status == statusFilter.Value);
        }
        if (hasFrom)
        {
            query = query.Where(r => r.CheckInDate >= fromDate);
        }
        if (hasTo)
        {
            query = query.Where(r => r.CheckInDate <= toDate);
        }

        var reservations = await query.OrderBy(r => r.CheckInDate).Take(50).ToListAsync();

        if (!reservations.Any())
        {
            return "No hay reservas con esos criterios.";
        }

        var sb = new StringBuilder();
        foreach (var r in reservations)
        {
            sb.AppendLine($"  {r.ReservationNumber} | {r.Guest.FirstName} {r.Guest.LastName} | " +
                $"habitación {r.Room.RoomNumber} ({r.Room.RoomType?.Name ?? "—"}) | " +
                $"{r.CheckInDate:dd MMM} → {r.CheckOutDate:dd MMM} ({r.NumberOfNights} noches) | " +
                $"{FormatMoney(r.TotalAmount, r.Hotel?.Currency ?? "USD")} | {r.Status} | {r.Source}");
        }
        return sb.ToString();
    }

    [McpServerTool, Description(
        "Indicadores del hotel en los últimos 30 días: Occupancy (%), ADR (tarifa promedio " +
        "por habitación), RevPAR, TotalRevenue y RevenueLast30D. Responde '¿cómo va mi " +
        "ocupación?'.")]
    public static async Task<string> get_kpis(
        ICurrentUserService user,
        IDashboardService dashboard,
        [Description("Opcional: ventana en días (por defecto 30).")] int? days = null)
    {
        var hotelId = ResolveHotel(user);
        if (!hotelId.HasValue)
        {
            return "No se pudo determinar el hotel del usuario.";
        }

        var kpis = await dashboard.GetDashboardKpisAsync(hotelId);
        var sb = new StringBuilder();
        foreach (var kv in kpis)
        {
            sb.AppendLine($"  {kv.Key}: {kv.Value:0.##}");
        }
        return sb.ToString();
    }

    [McpServerTool, Description(
        "Lista los canales de venta del hotel (Booking.com, Expedia, Directo, etc.) con su " +
        "tipo, comisión, estado activo y número de mapeos de habitaciones configurados.")]
    public static async Task<string> list_channels(
        IApplicationDbContext context,
        ICurrentUserService user)
    {
        var hotelId = ResolveHotel(user);
        if (!hotelId.HasValue)
        {
            return "No se pudo determinar el hotel del usuario.";
        }

        var channels = await context.Channels
            .Where(c => c.HotelId == hotelId.Value)
            .Select(c => new { c.Name, c.ChannelType, c.CommissionRate, c.IsActive, Mappings = c.Mappings.Count })
            .ToListAsync();

        if (!channels.Any())
        {
            return "El hotel no tiene canales de venta configurados.";
        }

        var sb = new StringBuilder();
        foreach (var c in channels)
        {
            var tipo = c.ChannelType.ToLowerInvariant() switch
            {
                "ota" => "OTA",
                "agency" => "Agencia",
                "phone" => "Teléfono",
                "walkin" or "walk-in" => "Walk-in",
                "site" or "website" => "Sitio web",
                _ => c.ChannelType
            };
            sb.AppendLine($"  {c.Name} | {tipo} | comisión {c.CommissionRate:0}% | " +
                $"{(c.IsActive ? "activo" : "inactivo")} | {c.Mappings} mapeo(s)");
        }
        return sb.ToString();
    }

    private static bool TryParseDate(string? value, out DateTime date)
    {
        date = default;
        return DateTime.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture,
            DateTimeStyles.None, out date);
    }

    private static string FormatMoney(decimal amount, string? currency)
        => $"{amount.ToString("0.##", CultureInfo.InvariantCulture)} {(string.IsNullOrEmpty(currency) ? "USD" : currency)}";
}