using Hospitality.Application.Common;
using Hospitality.Application.Common.DTOs;
using Hospitality.Application.Common.Interfaces;
using Hospitality.Application.Reservations.Commands;
using Hospitality.Domain.Entities;
using Hospitality.Domain.Enums;
using Hospitality.Domain.Exceptions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Hospitality.Application.Reservations.Services;

public class ReservationService : IReservationService
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<ReservationService> _logger;

    public ReservationService(IApplicationDbContext context, ICurrentUserService currentUserService, ILogger<ReservationService> logger)
    {
        _context = context;
        _currentUserService = currentUserService;
        _logger = logger;
    }

    public async Task<PaginatedResult<ReservationDto>> GetReservationsAsync(
        PaginatedQuery query,
        Guid? hotelScope = null,
        string? status = null,
        DateTime? from = null,
        DateTime? to = null,
        string? search = null)
    {
        var dbQuery = QueryWithDetails();

        dbQuery = dbQuery.Where(r => (_context.ActiveHotels().Any(h => h.Id == r.HotelId)));

        if (hotelScope.HasValue)
        {
            dbQuery = dbQuery.Where(r => r.HotelId == hotelScope.Value);
        }

        if (!string.IsNullOrWhiteSpace(status) &&
            Enum.TryParse<ReservationStatus>(status, true, out var parsedStatus))
        {
            dbQuery = dbQuery.Where(r => r.Status == parsedStatus);
        }

        if (from.HasValue)
        {
            var fromDate = from.Value.Date == DateTime.MinValue ? from.Value : DateTime.SpecifyKind(from.Value.Date, DateTimeKind.Utc);
            dbQuery = dbQuery.Where(r => r.CheckInDate.Date >= fromDate.Date || r.CheckOutDate.Date >= fromDate.Date);
        }

        if (to.HasValue)
        {
            var toDate = DateTime.SpecifyKind(to.Value.Date, DateTimeKind.Utc);
            dbQuery = dbQuery.Where(r => r.CheckInDate.Date <= toDate.Date);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            dbQuery = dbQuery.Where(r =>
                r.ReservationNumber.ToLower().Contains(term) ||
                (r.Guest.FirstName + " " + r.Guest.LastName).ToLower().Contains(term) ||
                r.Guest.Email.ToLower().Contains(term) ||
                r.Room.RoomNumber.ToLower().Contains(term) ||
                (r.BookingReference != null && r.BookingReference.ToLower().Contains(term)));
        }

        dbQuery = dbQuery.OrderByDescending(r => r.CreatedAt);

        var totalCount = await dbQuery.CountAsync();
        var items = await dbQuery
            .Skip((query.PageNumber - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync();

        return new PaginatedResult<ReservationDto>(
            items.Select(MapToDto).ToList(),
            totalCount,
            query.PageNumber,
            query.PageSize);
    }

    public async Task<ReservationDto?> GetReservationByIdAsync(Guid id)
    {
        return await QueryWithDetails()
            .Where(r => r.Id == id)
            .Select(r => MapToDto(r))
            .FirstOrDefaultAsync();
    }

    public async Task<List<ReservationDto>> GetReservationsByGuestAsync(Guid guestId, Guid? hotelScope = null)
    {
        var query = QueryWithDetails().Where(r => r.GuestId == guestId);
        if (hotelScope.HasValue)
        {
            query = query.Where(r => r.HotelId == hotelScope.Value);
        }
        var items = await query.OrderByDescending(r => r.CreatedAt).ToListAsync();
        return items.Select(MapToDto).ToList();
    }

    public async Task<ReservationDto> CreateReservationAsync(CreateReservationCommand command)
    {
        var hotel = await _context.ActiveHotels().FirstOrDefaultAsync(h => h.Id == command.HotelId)
            ?? throw new KeyNotFoundException($"Hotel con ID {command.HotelId} no encontrado.");

        var room = await _context.Rooms
            .Include(r => r.RoomType).ThenInclude(rt => rt.RatePlan)
            .FirstOrDefaultAsync(r => r.Id == command.RoomId && !r.IsDeleted && r.HotelId == command.HotelId)
            ?? throw new KeyNotFoundException($"Habitación con ID {command.RoomId} no encontrada.");

        var checkIn = DateTime.SpecifyKind(command.CheckInDate, DateTimeKind.Utc);
        var checkOut = DateTime.SpecifyKind(command.CheckOutDate, DateTimeKind.Utc);

        if (checkOut.Date <= checkIn.Date)
        {
            throw new ValidationException("La reserva debe cubrir al menos una noche.");
        }

        await EnsureRoomFreeAsync(room.Id, checkIn, checkOut);

        var guest = await ResolveGuestAsync(command);

        var ratePlan = room.RoomType.RatePlan;
        var nights = Math.Max(0, (int)(checkOut.Date - checkIn.Date).Days);
        if (ratePlan is { MinStay: > 1 } && nights < ratePlan.MinStay)
        {
            throw new ValidationException($"El plan «{ratePlan.Name}» exige una estancia mínima de {ratePlan.MinStay} noche(s).");
        }

        var multiplier = ratePlan?.Multiplier ?? 1m;
        var roomRate = command.RoomRate.HasValue && command.RoomRate.Value > 0
            ? command.RoomRate.Value
            : Math.Round((await ResolveCalendarRateAsync(room.RoomType, checkIn, checkOut)) * multiplier, 2);

        var reservation = new Reservation
        {
            ReservationNumber = GenerateReservationNumber(),
            CheckInDate = checkIn,
            CheckOutDate = checkOut,
            NumberOfGuests = Math.Max(1, command.NumberOfGuests),
            HasExtraBed = command.HasExtraBed,
            SpecialRequests = command.SpecialRequests,
            Status = ReservationStatus.Confirmed,
            Source = string.IsNullOrWhiteSpace(command.Source) ? "Directo" : command.Source,
            BookingReference = command.BookingReference,
            RoomRate = roomRate,
            ExtraBedRate = command.HasExtraBed ? Math.Round(roomRate * 0.15m, 2) : 0,
            TaxRate = hotel.TaxRate ?? 0,
            HotelId = hotel.Id,
            RoomId = room.Id,
            GuestId = guest.Id,
            RatePlanId = ratePlan?.Id,
            PenaltyAmount = 0,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        reservation.CalculateTotal();

        _context.Reservations.Add(reservation);

        _context.DomainEvents.Add(await DomainEventLog.NewAsync(_context, "reservation", reservation.Id, "ReservationCreated",
            new
            {
                reservation.HotelId,
                reservation.RoomId,
                reservation.GuestId,
                reservation.CheckInDate,
                reservation.CheckOutDate,
                reservation.NumberOfNights,
                reservation.RoomRate,
                reservation.ExtraBedRate,
                reservation.TaxRate,
                reservation.TotalAmount,
                reservation.Source,
                reservation.BookingReference
            },
            actorUserId: _currentUserService.UserId));

        await _context.SaveChangesAsync();

        _logger.LogInformation("Reserva {Number} creada para habitación {Room} hotel {Hotel}",
            reservation.ReservationNumber, room.RoomNumber, hotel.Name);

        return await GetReservationByIdAsync(reservation.Id) ?? MapToDto(reservation);
    }

    /// <summary>
    /// Calcula la tarifa nocturna efectiva promediando el calendario de tarifas
    /// (RoomRate override ?? BasePrice) sobre las noches de la reserva.
    /// </summary>
    private async Task<decimal> ResolveCalendarRateAsync(RoomType roomType, DateTime checkIn, DateTime checkOut)
    {
        var from = DateOnly.FromDateTime(checkIn.Date);
        var to = DateOnly.FromDateTime(checkOut.Date.AddDays(-1));
        if (to < from)
        {
            return roomType.BasePrice;
        }

        var rates = await _context.RoomRates
            .Where(rr => rr.RoomTypeId == roomType.Id && rr.Date >= from && rr.Date <= to)
            .ToDictionaryAsync(rr => rr.Date, rr => rr.Price);

        var nights = to.DayNumber - from.DayNumber + 1;
        var total = 0m;
        for (var day = from; day <= to; day = day.AddDays(1))
        {
            total += rates.TryGetValue(day, out var price) ? price : roomType.BasePrice;
        }

        var average = Math.Round(total / nights, 2);
        return average > 0 ? average : roomType.BasePrice;
    }

    public async Task<ReservationDto> ConfirmReservationAsync(Guid id)
    {
        var reservation = await GetForMutationAsync(id);
        reservation.Confirm();
        reservation.UpdatedAt = DateTime.UtcNow;
        _context.DomainEvents.Add(await DomainEventLog.NewAsync(_context, "reservation", reservation.Id, "ReservationConfirmed",
            new { reservation.ReservationNumber }, actorUserId: _currentUserService.UserId));
        await _context.SaveChangesAsync();
        return MapToDto(reservation);
    }

    public async Task<ReservationDto> CheckInReservationAsync(Guid id)
    {
        var reservation = await GetForMutationAsync(id);
        if (reservation.Status != ReservationStatus.Confirmed)
        {
            throw new ValidationException("Solo se puede hacer check-in a una reserva confirmada.");
        }

        if (reservation.Room.Status == RoomStatus.Occupied)
        {
            throw new ValidationException("La habitación ya está ocupada.");
        }

        reservation.CheckIn();
        reservation.UpdatedAt = DateTime.UtcNow;
        _context.DomainEvents.Add(await DomainEventLog.NewAsync(_context, "reservation", reservation.Id, "ReservationCheckedIn",
            new { reservation.ReservationNumber, reservation.Room.RoomNumber }, actorUserId: _currentUserService.UserId));
        await _context.SaveChangesAsync();

        _logger.LogInformation("Check-in de reserva {Number} en habitación {Room}",
            reservation.ReservationNumber, reservation.Room.RoomNumber);

        return MapToDto(reservation);
    }

    public async Task<ReservationDto> CheckOutReservationAsync(Guid id)
    {
        var reservation = await GetForMutationAsync(id);
        if (reservation.Status != ReservationStatus.CheckedIn)
        {
            throw new ValidationException("Solo se puede hacer check-out a una reserva con check-in realizado.");
        }

        reservation.CheckOut();
        reservation.UpdatedAt = DateTime.UtcNow;
        _context.DomainEvents.Add(await DomainEventLog.NewAsync(_context, "reservation", reservation.Id, "ReservationCheckedOut",
            new { reservation.ReservationNumber }, actorUserId: _currentUserService.UserId));
        await _context.SaveChangesAsync();

        _logger.LogInformation("Check-out de reserva {Number}", reservation.ReservationNumber);

        return MapToDto(reservation);
    }

    public async Task<ReservationDto> CancelReservationAsync(Guid id, string? reason)
    {
        var reservation = await GetForMutationAsync(id);

        var wasCheckedIn = reservation.Status == ReservationStatus.CheckedIn;
        reservation.Cancel(reason);
        reservation.UpdatedAt = DateTime.UtcNow;

        var penalty = ResolveCancellationPenalty(reservation);
        reservation.PenaltyAmount = penalty;
        var refundAmount = Math.Max(0, reservation.AmountPaid - penalty);

        // Al cancelar una reserva activa, la habitación vuelve a estar libre (si no tiene otra reserva vigente).
        if (wasCheckedIn && reservation.Room.Status == RoomStatus.Occupied)
        {
            reservation.Room.Status = RoomStatus.Available;
            reservation.Room.IsClean = false;
            reservation.Room.UpdatedAt = DateTime.UtcNow;
        }

        _context.DomainEvents.Add(await DomainEventLog.NewAsync(_context, "reservation", reservation.Id, "ReservationCancelled",
            new
            {
                reservation.ReservationNumber,
                Reason = reason,
                PenaltyAmount = penalty,
                RefundAmount = refundAmount,
                RatePlanId = reservation.RatePlanId
            }, actorUserId: _currentUserService.UserId));

        await _context.SaveChangesAsync();

        _logger.LogInformation("Reserva {Number} cancelada", reservation.ReservationNumber);

        return MapToDto(reservation);
    }

    private async Task<Reservation> GetForMutationAsync(Guid id)
    {
        var reservation = await QueryWithDetails()
            .FirstOrDefaultAsync(r => r.Id == id)
            ?? throw new KeyNotFoundException($"Reservación con ID {id} no encontrada.");
        return reservation;
    }

    private IQueryable<Reservation> QueryWithDetails()
    {
        return _context.Reservations
            .Include(r => r.Hotel)
            .Include(r => r.Room).ThenInclude(room => room.RoomType)
            .Include(r => r.Guest)
            .Include(r => r.RatePlan)
            .Where(r => !r.Room.IsDeleted);
    }

    /// <summary>
    /// Penalización por cancelación según el plan de tarifas de la reserva.
    /// Sin plan, se mantiene la política legada por ventanas de días.
    /// </summary>
    private static decimal ResolveCancellationPenalty(Reservation reservation)
    {
        var plan = reservation.RatePlan;
        if (plan == null)
        {
            return reservation.CalculateCancellationFee();
        }

        var charges = Math.Round(reservation.RoomRate * reservation.NumberOfNights, 2);
        var hoursToCheckIn = (reservation.CheckInDate - DateTime.UtcNow).TotalHours;
        return plan.Refundability switch
        {
            RefundabilityType.NonRefundable => charges,
            RefundabilityType.Moderate when hoursToCheckIn < plan.CancellationDeadlineHours => Math.Round(charges * 0.5m, 2),
            _ => 0m
        };
    }

    private async Task EnsureRoomFreeAsync(Guid roomId, DateTime checkIn, DateTime checkOut)
    {
        var overlapExists = await _context.Reservations.AnyAsync(r =>
            r.RoomId == roomId &&
            r.Status != ReservationStatus.Cancelled &&
            r.Status != ReservationStatus.CheckedOut &&
            checkIn < r.CheckOutDate &&
            checkOut > r.CheckInDate);

        if (overlapExists)
        {
            throw new ValidationException("La habitación ya tiene una reserva para ese rango de fechas.");
        }
    }

    private async Task<Guest> ResolveGuestAsync(CreateReservationCommand command)
    {
        if (!string.IsNullOrWhiteSpace(command.GuestEmail))
        {
            var byEmail = await _context.Guests
                .FirstOrDefaultAsync(g => !g.IsDeleted && g.Email.ToLower() == command.GuestEmail.Trim().ToLower());
            if (byEmail != null)
            {
                UpdateGuestFields(byEmail, command);
                return byEmail;
            }
        }

        if (!string.IsNullOrWhiteSpace(command.GuestDocumentNumber))
        {
            var byDoc = await _context.Guests
                .FirstOrDefaultAsync(g => !g.IsDeleted && g.DocumentNumber == command.GuestDocumentNumber.Trim());
            if (byDoc != null)
            {
                UpdateGuestFields(byDoc, command);
                return byDoc;
            }
        }

        var newGuest = new Guest
        {
            FirstName = command.GuestFirstName.Trim(),
            LastName = command.GuestLastName.Trim(),
            Email = command.GuestEmail?.Trim() ?? string.Empty,
            PhoneNumber = command.GuestPhone?.Trim() ?? string.Empty,
            DocumentType = string.IsNullOrWhiteSpace(command.GuestDocumentType) ? "Pasaporte" : command.GuestDocumentType,
            DocumentNumber = command.GuestDocumentNumber?.Trim() ?? string.Empty,
            Nationality = command.GuestNationality?.Trim() ?? string.Empty,
            City = command.GuestCity?.Trim(),
            Country = command.GuestCountry?.Trim(),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.Guests.Add(newGuest);
        await _context.SaveChangesAsync();
        return newGuest;
    }

    private static void UpdateGuestFields(Guest guest, CreateReservationCommand command)
    {
        if (!string.IsNullOrWhiteSpace(command.GuestFirstName)) guest.FirstName = command.GuestFirstName.Trim();
        if (!string.IsNullOrWhiteSpace(command.GuestLastName)) guest.LastName = command.GuestLastName.Trim();
        if (!string.IsNullOrWhiteSpace(command.GuestPhone)) guest.PhoneNumber = command.GuestPhone.Trim();
        if (!string.IsNullOrWhiteSpace(command.GuestDocumentNumber)) guest.DocumentNumber = command.GuestDocumentNumber.Trim();
        if (!string.IsNullOrWhiteSpace(command.GuestDocumentType)) guest.DocumentType = command.GuestDocumentType;
        if (!string.IsNullOrWhiteSpace(command.GuestNationality)) guest.Nationality = command.GuestNationality.Trim();
        if (!string.IsNullOrWhiteSpace(command.GuestCity)) guest.City = command.GuestCity.Trim();
        if (!string.IsNullOrWhiteSpace(command.GuestCountry)) guest.Country = command.GuestCountry.Trim();
        guest.UpdatedAt = DateTime.UtcNow;
    }

    private static string GenerateReservationNumber()
    {
        return $"RSV-{DateTime.UtcNow:yyyyMMdd}-{Random.Shared.Next(1000, 9999)}";
    }

    private static ReservationDto MapToDto(Reservation r)
    {
        return new ReservationDto
        {
            Id = r.Id,
            ReservationNumber = r.ReservationNumber,
            CheckInDate = r.CheckInDate,
            CheckOutDate = r.CheckOutDate,
            NumberOfNights = r.NumberOfNights,
            NumberOfGuests = r.NumberOfGuests,
            HasExtraBed = r.HasExtraBed,
            SpecialRequests = r.SpecialRequests,
            Status = r.Status.ToString(),
            CheckedInAt = r.CheckedInAt,
            CheckedOutAt = r.CheckedOutAt,
            CancelledAt = r.CancelledAt,
            CancellationReason = r.CancellationReason,
            Source = r.Source,
            BookingReference = r.BookingReference,
            RoomRate = r.RoomRate,
            TotalAmount = r.TotalAmount,
            AmountPaid = r.AmountPaid,
            BalanceDue = r.BalanceDue,
            IsFullyPaid = r.IsFullyPaid,
            TaxRate = r.TaxRate,
            HotelId = r.HotelId,
            HotelName = r.Hotel?.Name ?? string.Empty,
            RoomId = r.RoomId,
            RoomNumber = r.Room?.RoomNumber ?? string.Empty,
            RoomTypeName = r.Room?.RoomType?.Name ?? string.Empty,
            GuestId = r.GuestId,
            GuestName = r.Guest != null ? $"{r.Guest.FirstName} {r.Guest.LastName}".Trim() : string.Empty,
            GuestEmail = r.Guest?.Email ?? string.Empty,
            RatePlanId = r.RatePlanId,
            RatePlanName = r.RatePlan != null ? r.RatePlan.Name : string.Empty,
            PenaltyAmount = r.PenaltyAmount,
            GuestPhone = r.Guest?.PhoneNumber ?? string.Empty,
            CreatedAt = r.CreatedAt
        };
    }
}