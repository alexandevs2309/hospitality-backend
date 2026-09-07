using Hospitality.Application.Common.Interfaces;
using Hospitality.Application.Hotels.Queries;
using Hospitality.Domain.Entities;
using Hospitality.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Hospitality.Application.Hotels.Services;

public class DashboardService : IDashboardService
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<DashboardService> _logger;

    public DashboardService(
        IApplicationDbContext context,
        ICurrentUserService currentUserService,
        ILogger<DashboardService> logger)
    {
        _context = context;
        _currentUserService = currentUserService;
        _logger = logger;
    }

    public async Task<Dictionary<string, decimal>> GetHotelMetricsAsync(Guid? hotelId = null)
    {
        var id = await ResolveHotelIdAsync(hotelId);
        if (!id.HasValue)
        {
            return new Dictionary<string, decimal>();
        }

        var today = DateTime.UtcNow.Date;

        var totalRoomsProperty = await _context.ActiveHotels()
            .Where(h => h.Id == id.Value)
            .Select(h => h.TotalRooms)
            .FirstOrDefaultAsync();

        var totalRooms = await _context.ActiveRooms()
            .CountAsync(r => r.HotelId == id.Value);
        if (totalRoomsProperty > 0)
        {
            totalRooms = totalRoomsProperty;
        }

        var occupiedRooms = await _context.ActiveRooms()
            .CountAsync(r => r.HotelId == id.Value && r.Status == RoomStatus.Occupied);
        var availableRooms = await _context.ActiveRooms()
            .CountAsync(r => r.HotelId == id.Value &&
                             r.Status == RoomStatus.Available && !r.IsMaintenanceRequired);
        var maintenanceRooms = await _context.ActiveRooms()
            .CountAsync(r => r.HotelId == id.Value &&
                             (r.Status == RoomStatus.Maintenance || r.IsMaintenanceRequired));

        var checkInsToday = await _context.Reservations
            .CountAsync(r => r.HotelId == id.Value &&
                             r.Status == ReservationStatus.Confirmed &&
                             r.CheckInDate.Date == today);
        var checkOutsToday = await _context.Reservations
            .CountAsync(r => r.HotelId == id.Value &&
                             r.Status == ReservationStatus.CheckedOut &&
                             r.CheckedOutAt.HasValue &&
                             r.CheckedOutAt.Value.Date == today);

        var monthStart = new DateTime(today.Year, today.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var nextMonthStart = monthStart.AddMonths(1);
        var revenueToday = await _context.Reservations
            .Where(r => r.HotelId == id.Value &&
                        r.Status == ReservationStatus.CheckedOut &&
                        r.CheckedOutAt.HasValue &&
                        r.CheckedOutAt.Value.Date == today)
            .SumAsync(r => (decimal?)r.AmountPaid) ?? 0m;
        var revenueMonth = await _context.Reservations
            .Where(r => r.HotelId == id.Value &&
                        r.Status == ReservationStatus.CheckedOut &&
                        r.CheckedOutAt.HasValue &&
                        r.CheckedOutAt.Value >= monthStart &&
                        r.CheckedOutAt.Value < nextMonthStart)
            .SumAsync(r => (decimal?)r.AmountPaid) ?? 0m;

        var occupancyRate = totalRooms > 0
            ? Math.Round((decimal)occupiedRooms / totalRooms * 100, 2)
            : 0m;

        return new Dictionary<string, decimal>
        {
            ["TotalRooms"] = totalRooms,
            ["OccupiedRooms"] = occupiedRooms,
            ["AvailableRooms"] = availableRooms,
            ["MaintenanceRooms"] = maintenanceRooms,
            ["OccupancyRate"] = occupancyRate,
            ["TodayRevenue"] = revenueToday,
            ["MonthlyRevenue"] = revenueMonth,
            ["CheckInsToday"] = checkInsToday,
            ["CheckOutsToday"] = checkOutsToday
        };
    }

    public async Task<Dictionary<string, decimal>> GetLiveHotelMetricsAsync(Guid? hotelId = null)
    {
        return await GetHotelMetricsAsync(hotelId);
    }

    public async Task<List<BookingRowDto>> GetTodayBookingsAsync(Guid? hotelId = null, int limit = 10)
    {
        var id = await ResolveHotelIdAsync(hotelId);
        if (!id.HasValue)
        {
            return new List<BookingRowDto>();
        }

        var today = DateTime.UtcNow.Date;
        var bookings = await _context.Reservations
            .Include(r => r.Hotel)
            .Include(r => r.Room)
            .Include(r => r.Guest)
            .Where(r => r.HotelId == id.Value)
            .Where(r => r.CheckInDate.Date == today ||
                        r.CheckOutDate.Date == today ||
                        (r.CheckInDate <= today && r.CheckOutDate >= today))
            .OrderBy(r => r.CheckInDate)
            .Take(Math.Clamp(limit, 1, 50))
            .ToListAsync();

        return bookings.Select(r => new BookingRowDto
        {
            Guest = r.Guest.FullName,
            Room = r.Room.RoomNumber,
            CheckIn = r.CheckInDate.ToString("MMM dd"),
            CheckOut = r.CheckOutDate.ToString("MMM dd"),
            Status = r.Status.ToString(),
            Amount = r.TotalAmount
        }).ToList();
    }

    public async Task<HousekeepingStatusDto> GetHousekeepingStatusAsync(Guid? hotelId = null)
    {
        var id = await ResolveHotelIdAsync(hotelId);
        if (!id.HasValue)
        {
            return new HousekeepingStatusDto();
        }

        var rooms = await _context.ActiveRooms()
            .Where(r => r.HotelId == id.Value)
            .ToListAsync();

        var statuses = await _context.HousekeepingStatuses
            .Include(h => h.Room)
            .Where(h => h.Room.HotelId == id.Value)
            .OrderByDescending(h => h.CreatedAt)
            .ToListAsync();

        var pending = statuses
            .Where(h => h.Status == HousekeepingStatusType.InProgress || h.Status == HousekeepingStatusType.Inspection || h.Status == HousekeepingStatusType.Dirty)
            .Select(h => new HousekeepingRoomDto
            {
                Room = h.Room.RoomNumber,
                Status = h.Status.ToString(),
                Housekeeper = h.HousekeeperName ?? string.Empty,
                Priority = h.Priority
            })
            .ToList();

        return new HousekeepingStatusDto
        {
            Clean = rooms.Count(r => r.IsClean && !r.IsMaintenanceRequired),
            Pending = rooms.Count(r => !r.IsClean),
            Inspection = statuses.Count(h => h.Status == HousekeepingStatusType.Inspection),
            Maintenance = rooms.Count(r => r.IsMaintenanceRequired),
            Rooms = pending
        };
    }

    public async Task<List<MaintenanceTicketDto>> GetMaintenanceTicketsAsync(Guid? hotelId = null, string? status = "open", int limit = 10)
    {
        var id = await ResolveHotelIdAsync(hotelId);
        if (!id.HasValue)
        {
            return new List<MaintenanceTicketDto>();
        }

        var query = _context.MaintenanceTickets
            .Include(t => t.Room)
            .Where(t => t.Room.HotelId == id.Value);

        query = status?.ToLowerInvariant() switch
        {
            "open" => query.Where(t => t.Status == MaintenanceTicketStatus.Open),
            "inprogress" => query.Where(t => t.Status == MaintenanceTicketStatus.InProgress),
            "resolved" => query.Where(t => t.Status == MaintenanceTicketStatus.Resolved),
            "closed" => query.Where(t => t.Status == MaintenanceTicketStatus.Closed),
            _ => query.Where(t => t.Status == MaintenanceTicketStatus.Open || t.Status == MaintenanceTicketStatus.InProgress || t.Status == MaintenanceTicketStatus.OnHold)
        };

        var tickets = await query
            .OrderByDescending(t => t.CreatedAt)
            .Take(Math.Clamp(limit, 1, 50))
            .ToListAsync();

        return tickets.Select(t => new MaintenanceTicketDto
        {
            Room = t.Room.RoomNumber,
            Issue = t.Title,
            Priority = t.Priority.ToString(),
            Assignee = t.AssignedTo ?? string.Empty,
            Sla = t.DueDate.HasValue ? t.DueDate.Value.ToString("MMM dd HH:mm") : string.Empty,
            Status = t.Status.ToString()
        }).ToList();
    }

    public async Task<List<ChartPointDto>> GetOccupancyTrendAsync(Guid? hotelId = null, string period = "week")
    {
        var id = await ResolveHotelIdAsync(hotelId);
        if (!id.HasValue)
        {
            return new List<ChartPointDto>();
        }

        var reservations = await _context.Reservations
            .Where(r => r.HotelId == id.Value &&
                        r.Status != ReservationStatus.Cancelled &&
                        r.Status != ReservationStatus.NoShow)
            .Select(r => new { r.CheckInDate, r.CheckOutDate })
            .ToListAsync();

        var totalRooms = await _context.ActiveHotels()
            .Where(h => h.Id == id.Value)
            .Select(h => h.TotalRooms)
            .FirstOrDefaultAsync();

        return period switch
        {
            "month" => BuildDailyTrend(reservations.Select(r => (r.CheckInDate, r.CheckOutDate)).ToList(), totalRooms, 30),
            "year" => BuildMonthlyTrend(reservations.Select(r => (r.CheckInDate, r.CheckOutDate)).ToList(), totalRooms),
            _ => BuildDailyTrend(reservations.Select(r => (r.CheckInDate, r.CheckOutDate)).ToList(), totalRooms, 7)
        };
    }

    public async Task<List<ChartPointDto>> GetRevenueTrendAsync(Guid? hotelId = null, string period = "year")
    {
        var id = await ResolveHotelIdAsync(hotelId);
        if (!id.HasValue)
        {
            return new List<ChartPointDto>();
        }

        var reservations = await _context.Reservations
            .Where(r => r.HotelId == id.Value &&
                        r.Status == ReservationStatus.CheckedOut &&
                        r.CheckedOutAt.HasValue)
            .Select(r => new { r.CheckedOutAt, r.AmountPaid })
            .ToListAsync();

        var today = DateTime.UtcNow.Date;
        var weekStart = today.AddDays(-6);

        return period switch
        {
            "week" => reservations
                .Where(r => r.CheckedOutAt!.Value.Date >= weekStart)
                .GroupBy(r => r.CheckedOutAt!.Value.Date)
                .OrderBy(g => g.Key)
                .Select(g => new ChartPointDto
                {
                    Label = g.Key.ToString("ddd"),
                    Value = g.Sum(r => r.AmountPaid)
                })
                .ToList(),
            "month" => reservations
                .Where(r => r.CheckedOutAt!.Value.Date >= today.AddMonths(-1))
                .GroupBy(r => new DateTime(r.CheckedOutAt!.Value.Year, r.CheckedOutAt.Value.Month, 1))
                .OrderBy(g => g.Key)
                .Select(g => new ChartPointDto
                {
                    Label = g.Key.ToString("MMM"),
                    Value = g.Sum(r => r.AmountPaid)
                })
                .ToList(),
            _ => reservations
                .GroupBy(r => new DateTime(r.CheckedOutAt!.Value.Year, r.CheckedOutAt.Value.Month, 1))
                .OrderBy(g => g.Key)
                .Select(g => new ChartPointDto
                {
                    Label = g.Key.ToString("MMM yy"),
                    Value = g.Sum(r => r.AmountPaid)
                })
                .ToList()
        };
    }

    public async Task<Dictionary<string, decimal>> GetDashboardKpisAsync(Guid? hotelId = null)
    {
        var id = await ResolveHotelIdAsync(hotelId);
        if (!id.HasValue)
        {
            return new Dictionary<string, decimal>();
        }

        var today = DateTime.UtcNow.Date;

        var totalRooms = await _context.ActiveHotels()
            .Where(h => h.Id == id.Value)
            .Select(h => h.TotalRooms)
            .FirstOrDefaultAsync();

        var occupiedRooms = await _context.Reservations
            .CountAsync(r => r.HotelId == id.Value &&
                             r.CheckInDate <= today &&
                             r.CheckOutDate > today &&
                             r.Status != ReservationStatus.Cancelled &&
                             r.Status != ReservationStatus.NoShow);

        // Reservas actuales (hospedaje vigente hoy).
        var currentReservations = _context.Reservations
            .Where(r => r.HotelId == id.Value &&
                        r.CheckInDate <= today &&
                        r.CheckOutDate > today &&
                        r.Status != ReservationStatus.Cancelled &&
                        r.Status != ReservationStatus.NoShow)
            .Select(r => r.RoomRate);
        var roomRateList = await currentReservations.ToListAsync();
        var avgDailyRate = roomRateList.Count > 0
            ? Math.Round(roomRateList.Average(), 2)
            : 0m;

        var revenue = await _context.Reservations
            .Where(r => r.HotelId == id.Value && r.Status == ReservationStatus.CheckedOut)
            .SumAsync(r => (decimal?)r.AmountPaid) ?? 0m;
        var revPar = totalRooms > 0 ? Math.Round(revenue / totalRooms, 2) : 0m;

        var pendingBookings = await _context.Reservations
            .CountAsync(r =>
                r.HotelId == id.Value &&
                (r.Status == ReservationStatus.Pending || r.Status == ReservationStatus.Confirmed));

        var openTickets = await _context.MaintenanceTickets
            .Include(t => t.Room)
            .Where(t => t.Room.HotelId == id.Value &&
                        (t.Status == MaintenanceTicketStatus.Open ||
                         t.Status == MaintenanceTicketStatus.InProgress))
            .CountAsync();

        var checkInsToday = await _context.Reservations
            .CountAsync(r => r.HotelId == id.Value &&
                             r.CheckInDate.Date == today &&
                             r.Status == ReservationStatus.Confirmed);
        var checkOutsToday = await _context.Reservations
            .CountAsync(r => r.HotelId == id.Value &&
                             r.CheckedOutAt.HasValue &&
                             r.CheckedOutAt.Value.Date == today);
        var totalBookings = await _context.Reservations
            .CountAsync(r => r.HotelId == id.Value);

        var occupancyRate = totalRooms > 0
            ? Math.Round((decimal)occupiedRooms / totalRooms * 100, 2)
            : 0m;

        return new Dictionary<string, decimal>
        {
            ["OccupancyRate"] = occupancyRate,
            ["AverageDailyRate"] = avgDailyRate,
            ["RevenuePerAvailableRoom"] = revPar,
            ["TotalRevenue"] = revenue,
            ["CheckInsToday"] = checkInsToday,
            ["CheckOutsToday"] = checkOutsToday,
            ["PendingBookings"] = pendingBookings,
            ["OpenTickets"] = openTickets,
            ["TotalBookings"] = totalBookings
        };
    }

    public async Task<DashboardWidgetsDto> GetDashboardWidgetsAsync(Guid? hotelId = null)
    {
        var id = await ResolveHotelIdAsync(hotelId);

        // Ejecutamos en serie: EF Core no permite consultas concurrentes sobre el mismo DbContext.
        var metrics = await GetHotelMetricsAsync(id);
        var todayBookings = await GetTodayBookingsAsync(id);
        var housekeeping = await GetHousekeepingStatusAsync(id);
        var tickets = await GetMaintenanceTicketsAsync(id);
        var occupancyTrend = await GetOccupancyTrendAsync(id, "week");
        var revenueTrend = await GetRevenueTrendAsync(id, "year");

        return new DashboardWidgetsDto
        {
            Metrics = metrics,
            TodayBookings = todayBookings,
            HousekeepingStatus = housekeeping,
            MaintenanceTickets = tickets,
            OccupancyTrend = occupancyTrend,
            RevenueTrend = revenueTrend
        };
    }

    private async Task<Guid?> ResolveHotelIdAsync(Guid? hotelId)
    {
        if (hotelId.HasValue && hotelId.Value != Guid.Empty)
        {
            return hotelId.Value;
        }

        if (_currentUserService.HotelId.HasValue)
        {
            return _currentUserService.HotelId.Value;
        }

        var firstHotel = await _context.ActiveHotels()
            .OrderBy(h => h.CreatedAt)
            .FirstOrDefaultAsync();
        return firstHotel?.Id;
    }

    private static List<ChartPointDto> BuildDailyTrend(IReadOnlyCollection<(DateTime CheckInDate, DateTime CheckOutDate)> reservations, int totalRooms, int days)
    {
        var result = new List<ChartPointDto>();
        var start = DateTime.UtcNow.Date.AddDays(-(days - 1));
        for (var i = 0; i < days; i++)
        {
            var day = start.AddDays(i);
            var occupied = reservations.Count(r => r.CheckInDate.Date <= day && r.CheckOutDate.Date > day);
            var occupancyRate = totalRooms > 0 ? Math.Round((decimal)occupied / totalRooms * 100, 2) : 0m;
            result.Add(new ChartPointDto
            {
                Label = day.ToString("dd MMM"),
                Value = occupancyRate
            });
        }
        return result;
    }

    private static List<ChartPointDto> BuildMonthlyTrend(IReadOnlyCollection<(DateTime CheckInDate, DateTime CheckOutDate)> reservations, int totalRooms)
    {
        var result = new List<ChartPointDto>();
        var start = DateTime.UtcNow.Date.AddMonths(-11);
        for (var i = 0; i < 12; i++)
        {
            var month = start.AddMonths(i);
            var monthStart = new DateTime(month.Year, month.Month, 1);
            var monthEnd = monthStart.AddMonths(1);
            var roomsNight = totalRooms > 0 ? (decimal)totalRooms * monthEnd.Subtract(monthStart).Days : 0m;
            var occupiedNights = reservations
                .Where(r => r.CheckInDate < monthEnd && r.CheckOutDate > monthStart)
                .Sum(r => Math.Max(0,
                    (r.CheckOutDate < monthEnd ? r.CheckOutDate : monthEnd)
                    .Subtract(r.CheckInDate > monthStart ? r.CheckInDate : monthStart).Days));
            var occupancyRate = roomsNight > 0 ? Math.Round(occupiedNights / roomsNight * 100, 2) : 0m;
            result.Add(new ChartPointDto
            {
                Label = month.ToString("MMM yy"),
                Value = occupancyRate
            });
        }
        return result;
    }
}
