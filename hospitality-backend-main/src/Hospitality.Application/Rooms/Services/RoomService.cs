using Hospitality.Application.Common.DTOs;
using Hospitality.Application.Common.Interfaces;
using Hospitality.Application.Rooms.Commands;
using Hospitality.Domain.Entities;
using Hospitality.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Hospitality.Application.Rooms.Services;

public class RoomService : IRoomService
{
    private readonly IApplicationDbContext _context;
    private readonly ILogger<RoomService> _logger;

    public RoomService(IApplicationDbContext context, ILogger<RoomService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<PaginatedResult<RoomDto>> GetRoomsAsync(PaginatedQuery query, Guid? hotelScope = null)
    {
        var dbQuery = QueryWithDetails()
            .Where(r => !r.IsDeleted);

        if (hotelScope.HasValue)
        {
            dbQuery = dbQuery.Where(r => r.HotelId == hotelScope.Value);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim().ToLower();
            dbQuery = dbQuery.Where(r =>
                r.RoomNumber.ToLower().Contains(search) ||
                (r.RoomType != null && r.RoomType.Name.ToLower().Contains(search)));
        }

        var totalCount = await dbQuery.CountAsync();
        var rooms = await dbQuery
            .OrderBy(r => r.Floor).ThenBy(r => r.RoomNumber)
            .Skip((query.PageNumber - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync();

        return new PaginatedResult<RoomDto>(
            rooms.Select(MapToDto).ToList(),
            totalCount,
            query.PageNumber,
            query.PageSize);
    }

    public async Task<RoomDto?> GetRoomByIdAsync(Guid id)
    {
        var room = await QueryWithDetails()
            .FirstOrDefaultAsync(r => r.Id == id && !r.IsDeleted);
        return room == null ? null : MapToDto(room);
    }

    public async Task<List<RoomDto>> GetRoomsByHotelAsync(Guid hotelId)
    {
        var rooms = await QueryWithDetails()
            .Where(r => r.HotelId == hotelId && !r.IsDeleted)
            .OrderBy(r => r.Floor).ThenBy(r => r.RoomNumber)
            .ToListAsync();
        return rooms.Select(MapToDto).ToList();
    }

    public async Task<List<RoomDto>> GetAvailableRoomsAsync(
        Guid hotelId, DateTime startDate, DateTime endDate, Guid? roomTypeId = null)
    {
        if (startDate >= endDate)
        {
            throw new ArgumentException("La fecha de check-in debe ser anterior a la fecha de check-out.");
        }

        // Normalizar a UTC: las columnas son timestamp with time zone y Npgsql
        // rechaza DateTime con Kind=Unspecified.
        var start = DateTime.SpecifyKind(startDate, DateTimeKind.Utc);
        var end = DateTime.SpecifyKind(endDate, DateTimeKind.Utc);

        var query = QueryWithDetails()
            .Where(r => r.HotelId == hotelId && !r.IsDeleted &&
                        r.Status == RoomStatus.Available && !r.IsMaintenanceRequired);

        if (roomTypeId.HasValue)
        {
            query = query.Where(r => r.RoomTypeId == roomTypeId.Value);
        }

        var rooms = await query.ToListAsync();

        var occupiedRoomIds = await _context.Reservations
            .Where(r => r.HotelId == hotelId &&
                        r.Status != ReservationStatus.Cancelled &&
                        r.Status != ReservationStatus.CheckedOut &&
                        r.CheckInDate < end &&
                        r.CheckOutDate > start)
            .Select(r => r.RoomId)
            .ToListAsync();

        var available = rooms
            .Where(r => !occupiedRoomIds.Contains(r.Id))
            .ToList();
        return available.Select(MapToDto).ToList();
    }

    public async Task<RoomDto> CreateRoomAsync(CreateRoomCommand command)
    {
        if (!await _context.ActiveHotels().AnyAsync(h => h.Id == command.HotelId))
        {
            throw new KeyNotFoundException($"Hotel con ID {command.HotelId} no encontrado.");
        }

        if (!await _context.RoomTypes.AnyAsync(rt => rt.Id == command.RoomTypeId))
        {
            throw new KeyNotFoundException($"Tipo de habitación con ID {command.RoomTypeId} no encontrado.");
        }

        var room = new Room
        {
            RoomNumber = command.RoomNumber,
            Floor = command.Floor,
            Status = RoomStatus.Available,
            IsClean = true,
            IsMaintenanceRequired = false,
            Notes = command.Description,
            HotelId = command.HotelId,
            RoomTypeId = command.RoomTypeId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.Rooms.Add(room);
        await _context.SaveChangesAsync();
        _logger.LogInformation("Habitación creada con ID {RoomId}", room.Id);
        return MapToDto(room);
    }

    public async Task<RoomDto> UpdateRoomAsync(UpdateRoomCommand command)
    {
        var room = await QueryWithDetails()
            .FirstOrDefaultAsync(r => r.Id == command.Id && !r.IsDeleted)
            ?? throw new KeyNotFoundException($"Habitación con ID {command.Id} no encontrada.");

        room.RoomNumber = command.RoomNumber;
        room.Floor = command.Floor;
        room.Notes = command.Description;
        room.RoomTypeId = command.RoomTypeId;
        room.HotelId = command.HotelId;
        room.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        return MapToDto(await QueryWithDetails().FirstAsync(r => r.Id == room.Id));
    }

    public async Task<RoomDto> UpdateRoomStatusAsync(Guid id, UpdateRoomStatusCommand command)
    {
        var room = await QueryWithDetails()
            .FirstOrDefaultAsync(r => r.Id == id && !r.IsDeleted)
            ?? throw new KeyNotFoundException($"Habitación con ID {id} no encontrada.");

        if (Enum.TryParse<RoomStatus>(command.Status, true, out var status))
        {
            room.Status = status;
        }

        room.IsClean = command.IsClean;
        room.IsMaintenanceRequired = command.IsMaintenanceRequired;
        room.Notes = command.Notes;
        room.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        return MapToDto(room);
    }

    public async Task<RoomDto> MarkRoomAsDirtyAsync(Guid roomId, string? reason = null)
    {
        var room = await QueryWithDetails()
            .FirstOrDefaultAsync(r => r.Id == roomId && !r.IsDeleted)
            ?? throw new KeyNotFoundException($"Habitación con ID {roomId} no encontrada.");

        room.MarkAsDirty();
        room.Notes = reason ?? room.Notes;
        room.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        return MapToDto(room);
    }

    public async Task<RoomDto> MarkRoomAsCleanAsync(Guid roomId)
    {
        var room = await QueryWithDetails()
            .FirstOrDefaultAsync(r => r.Id == roomId && !r.IsDeleted)
            ?? throw new KeyNotFoundException($"Habitación con ID {roomId} no encontrada.");

        room.MarkAsClean();
        room.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        return MapToDto(room);
    }

    public async Task<RoomDto> RequestMaintenanceAsync(Guid roomId, RequestMaintenanceCommand command)
    {
        var room = await QueryWithDetails()
            .FirstOrDefaultAsync(r => r.Id == roomId && !r.IsDeleted)
            ?? throw new KeyNotFoundException($"Habitación con ID {roomId} no encontrada.");

        room.RequestMaintenance();
        room.UpdatedAt = DateTime.UtcNow;

        var ticket = new MaintenanceTicket
        {
            Title = command.Description,
            Description = command.Description,
            Priority = Enum.TryParse<MaintenancePriority>(command.Priority, true, out var priority)
                ? priority
                : MaintenancePriority.Medium,
            Status = MaintenanceTicketStatus.Open,
            RoomId = room.Id,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        ticket.CalculateDueDateBasedOnPriority();
        _context.MaintenanceTickets.Add(ticket);

        await _context.SaveChangesAsync();
        return MapToDto(room);
    }

    public async Task<RoomDto> CompleteMaintenanceAsync(Guid roomId)
    {
        var room = await QueryWithDetails()
            .FirstOrDefaultAsync(r => r.Id == roomId && !r.IsDeleted)
            ?? throw new KeyNotFoundException($"Habitación con ID {roomId} no encontrada.");

        room.CompleteMaintenance();
        room.UpdatedAt = DateTime.UtcNow;

        var openTickets = await _context.MaintenanceTickets
            .Where(t => t.RoomId == roomId &&
                        t.Status != MaintenanceTicketStatus.Closed &&
                        t.Status != MaintenanceTicketStatus.Cancelled)
            .ToListAsync();

        foreach (var ticket in openTickets)
        {
            ticket.Status = MaintenanceTicketStatus.Resolved;
            ticket.CompletedAt = DateTime.UtcNow;
            ticket.ResolutionNotes = "Mantenimiento completado desde la consola de habitaciones.";
            ticket.UpdatedAt = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync();
        return MapToDto(room);
    }

    public async Task<List<RoomHistoryDto>> GetRoomHistoryAsync(Guid roomId, DateTime? from = null, DateTime? to = null)
    {
        if (!await _context.ActiveRooms().AnyAsync(r => r.Id == roomId))
        {
            throw new KeyNotFoundException($"Habitación con ID {roomId} no encontrada.");
        }

        var query = _context.Reservations
            .Include(r => r.Guest)
            .Where(r => r.RoomId == roomId);

        if (from.HasValue)
        {
            var fromUtc = DateTime.SpecifyKind(from.Value, DateTimeKind.Utc);
            query = query.Where(r => r.CheckOutDate >= fromUtc || r.CheckInDate >= fromUtc);
        }

        if (to.HasValue)
        {
            var toUtc = DateTime.SpecifyKind(to.Value, DateTimeKind.Utc);
            query = query.Where(r => r.CheckInDate <= toUtc);
        }

        var reservations = await query
            .OrderByDescending(r => r.CheckOutDate)
            .ToListAsync();

        return reservations.Select(r => new RoomHistoryDto
        {
            Date = r.CheckOutDate,
            Status = r.Status.ToString(),
            GuestName = r.Guest.FullName,
            Price = r.TotalAmount
        }).ToList();
    }

    public async Task<RoomStatsDto> GetRoomStatsAsync(Guid hotelId)
    {
        if (!await _context.ActiveHotels().AnyAsync(h => h.Id == hotelId))
        {
            throw new KeyNotFoundException($"Hotel con ID {hotelId} no encontrado.");
        }

        var reservations = await _context.Reservations
            .Where(r => r.HotelId == hotelId &&
                        r.Status != ReservationStatus.Cancelled &&
                        r.Status != ReservationStatus.NoShow)
            .ToListAsync();

        var closed = reservations.Where(r => r.Status == ReservationStatus.CheckedOut).ToList();

        var totalNights = closed.Sum(r => r.NumberOfNights);
        var totalRevenue = closed.Sum(r => r.AmountPaid);
        var averageDailyRate = totalNights > 0 ? Math.Round(totalRevenue / totalNights, 2) : 0m;

        var totalRooms = await _context.ActiveHotels()
            .Where(h => h.Id == hotelId)
            .Select(h => h.TotalRooms)
            .FirstOrDefaultAsync();
        var occupancyRate = totalRooms > 0
            ? Math.Round((decimal)reservations.Count(r => r.CheckInDate <= DateTime.UtcNow && r.CheckOutDate >= DateTime.UtcNow) / totalRooms * 100, 2)
            : 0m;

        return new RoomStatsDto
        {
            HotelId = hotelId,
            AverageDailyRate = averageDailyRate,
            TotalNights = totalNights,
            TotalRevenue = totalRevenue,
            OccupancyRate = occupancyRate
        };
    }

    public async Task<List<RoomDto>> SearchRoomsAsync(
        Guid hotelId, string? roomNumber, string? status, int? floor, Guid? roomTypeId)
    {
        var query = QueryWithDetails()
            .Where(r => r.HotelId == hotelId);

        if (!string.IsNullOrWhiteSpace(roomNumber))
        {
            query = query.Where(r => r.RoomNumber.Contains(roomNumber.Trim()));
        }

        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<RoomStatus>(status, true, out var statusValue))
        {
            query = query.Where(r => r.Status == statusValue);
        }

        if (floor.HasValue)
        {
            query = query.Where(r => r.Floor == floor.Value);
        }

        if (roomTypeId.HasValue)
        {
            query = query.Where(r => r.RoomTypeId == roomTypeId.Value);
        }

        var rooms = await query
            .OrderBy(r => r.Floor).ThenBy(r => r.RoomNumber)
            .ToListAsync();
        return rooms.Select(MapToDto).ToList();
    }

    private IQueryable<Room> QueryWithDetails()
    {
        return _context.ActiveRooms()
            .Include(r => r.RoomType)
            .Include(r => r.Hotel);
    }

    private static RoomDto MapToDto(Room room)
    {
        return new RoomDto
        {
            Id = room.Id,
            RoomNumber = room.RoomNumber,
            Floor = room.Floor,
            Description = room.Notes,
            Price = room.RoomType?.BasePrice ?? 0m,
            MaxOccupancy = room.RoomType?.Capacity ?? 0,
            Status = room.Status.ToString(),
            IsClean = room.IsClean,
            IsMaintenanceRequired = room.IsMaintenanceRequired,
            RoomTypeId = room.RoomTypeId,
            RoomTypeName = room.RoomType?.Name ?? string.Empty,
            HotelId = room.HotelId,
            HotelName = room.Hotel?.Name ?? string.Empty,
            CreatedAt = room.CreatedAt,
            UpdatedAt = room.UpdatedAt,
            MaintenanceNotes = room.Notes
        };
    }
}