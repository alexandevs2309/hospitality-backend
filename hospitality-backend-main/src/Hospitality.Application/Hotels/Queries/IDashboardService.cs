namespace Hospitality.Application.Hotels.Queries;

public interface IDashboardService
{
    Task<Dictionary<string, decimal>> GetHotelMetricsAsync(Guid? hotelId = null);
    Task<Dictionary<string, decimal>> GetLiveHotelMetricsAsync(Guid? hotelId = null);
    Task<List<BookingRowDto>> GetTodayBookingsAsync(Guid? hotelId = null, int limit = 10);
    Task<HousekeepingStatusDto> GetHousekeepingStatusAsync(Guid? hotelId = null);
    Task<List<MaintenanceTicketDto>> GetMaintenanceTicketsAsync(Guid? hotelId = null, string? status = "open", int limit = 10);
    Task<List<ChartPointDto>> GetOccupancyTrendAsync(Guid? hotelId = null, string period = "week");
    Task<List<ChartPointDto>> GetRevenueTrendAsync(Guid? hotelId = null, string period = "year");
    Task<Dictionary<string, decimal>> GetDashboardKpisAsync(Guid? hotelId = null);
    Task<DashboardWidgetsDto> GetDashboardWidgetsAsync(Guid? hotelId = null);
}

public class BookingRowDto
{
    public string Guest { get; set; } = string.Empty;
    public string Room { get; set; } = string.Empty;
    public string CheckIn { get; set; } = string.Empty;
    public string CheckOut { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public decimal Amount { get; set; }
}

public class HousekeepingStatusDto
{
    public int Clean { get; set; }
    public int Pending { get; set; }
    public int Inspection { get; set; }
    public int Maintenance { get; set; }
    public List<HousekeepingRoomDto> Rooms { get; set; } = new List<HousekeepingRoomDto>();
}

public class HousekeepingRoomDto
{
    public string Room { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Housekeeper { get; set; } = string.Empty;
    public string Priority { get; set; } = string.Empty;
}

public class MaintenanceTicketDto
{
    public Guid TicketId { get; set; }
    public Guid RoomId { get; set; }
    public string Room { get; set; } = string.Empty;
    public string Issue { get; set; } = string.Empty;
    public string Priority { get; set; } = string.Empty;
    public string Assignee { get; set; } = string.Empty;
    public string Sla { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

public class ChartPointDto
{
    public string Label { get; set; } = string.Empty;
    public decimal Value { get; set; }
}

public class DashboardWidgetsDto
{
    public Dictionary<string, decimal> Metrics { get; set; } = new Dictionary<string, decimal>();
    public List<BookingRowDto> TodayBookings { get; set; } = new List<BookingRowDto>();
    public HousekeepingStatusDto HousekeepingStatus { get; set; } = new HousekeepingStatusDto();
    public List<MaintenanceTicketDto> MaintenanceTickets { get; set; } = new List<MaintenanceTicketDto>();
    public List<ChartPointDto> OccupancyTrend { get; set; } = new List<ChartPointDto>();
    public List<ChartPointDto> RevenueTrend { get; set; } = new List<ChartPointDto>();
}