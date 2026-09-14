using Hospitality.Application.Automation.Commands;
using Hospitality.Application.Common;
using Hospitality.Application.Common.Helpers;
using Hospitality.Application.Common.Interfaces;
using Hospitality.Application.PublicWidget.Commands;
using Hospitality.Domain.Entities;
using Hospitality.Domain.Enums;
using Hospitality.Domain.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace Hospitality.Application.PublicWidget.Services;

public class WidgetService : IWidgetService
{
    private readonly IApplicationDbContext _context;
    private readonly IAutomationService _automationService;

    public WidgetService(IApplicationDbContext context, IAutomationService automationService)
    {
        _context = context;
        _automationService = automationService;
    }

    private static decimal ResolvePricePerNight(RoomType roomType)
    {
        return roomType.RatePlan != null && roomType.RatePlan.Multiplier > 0
            ? roomType.BasePrice * roomType.RatePlan.Multiplier
            : roomType.BasePrice;
    }

    public async Task<WidgetConfigDto> GetConfigAsync(Guid hotelId)
    {
        var hotel = await _context.Hotels
            .Include(h => h.RoomTypes)
            .ThenInclude(rt => rt.RatePlan)
            .FirstOrDefaultAsync(h => h.Id == hotelId && !h.IsDeleted);

        if (hotel == null)
        {
            return new WidgetConfigDto { HotelId = hotelId };
        }

        return new WidgetConfigDto
        {
            HotelId = hotel.Id,
            HotelName = hotel.Name,
            Currency = hotel.Currency ?? "USD",
            RoomTypes = hotel.RoomTypes
                .OrderBy(rt => rt.BasePrice)
                .Select(rt => new WidgetRoomTypeDto
                {
                    RoomTypeId = rt.Id,
                    Name = rt.Name,
                    Description = rt.Description,
                    Capacity = rt.Capacity,
                    BasePrice = rt.BasePrice,
                    PricePerNight = ResolvePricePerNight(rt),
                    Amenities = rt.Amenities,
                    ImageUrl = rt.ImageUrl
                })
                .ToList()
        };
    }

    public async Task<WidgetAvailabilityDto> GetAvailabilityAsync(Guid hotelId, DateTime from, DateTime to)
    {
        var config = await GetConfigAsync(hotelId);
        var fromUtc = DateTime.SpecifyKind(from.Date, DateTimeKind.Utc);
        var toUtc = DateTime.SpecifyKind(to.Date, DateTimeKind.Utc);

        var result = new WidgetAvailabilityDto
        {
            HotelId = hotelId,
            From = fromUtc,
            To = toUtc,
            Currency = config.Currency,
            RoomTypes = new List<WidgetRoomAvailabilityDto>()
        };

        var nights = Math.Max(0, (toUtc - fromUtc).Days);
        if (nights == 0 || config.RoomTypes.Count == 0)
        {
            return result;
        }

        foreach (var roomType in config.RoomTypes)
        {
            var nightsDto = new List<WidgetNightDto>();
            var availableNights = 0;

            for (var i = 0; i < nights; i++)
            {
                var date = fromUtc.AddDays(i);
                var available = await AvailabilityHelper.CalculateAvailableRoomsAsync(_context, roomType.RoomTypeId, date);
                if (available > 0)
                {
                    availableNights++;
                }

                nightsDto.Add(new WidgetNightDto
                {
                    Date = date,
                    AvailableUnits = available,
                    Price = roomType.PricePerNight
                });
            }

            result.RoomTypes.Add(new WidgetRoomAvailabilityDto
            {
                RoomTypeId = roomType.RoomTypeId,
                Name = roomType.Name,
                Capacity = roomType.Capacity,
                PricePerNight = roomType.PricePerNight,
                AvailableNights = availableNights,
                Nights = nightsDto
            });
        }

        return result;
    }

    public async Task<PublicBookingResponse> CreatePublicBookingAsync(PublicBookingRequest request)
    {
        // 1) Hotel
        var hotel = await _context.Hotels
            .FirstOrDefaultAsync(h => h.Id == request.HotelId && !h.IsDeleted)
            ?? throw new KeyNotFoundException("Hotel no encontrado.");

        // 2) RoomType con RatePlan
        var roomType = await _context.RoomTypes
            .Include(rt => rt.RatePlan)
            .FirstOrDefaultAsync(rt => rt.Id == request.RoomTypeId && rt.HotelId == request.HotelId)
            ?? throw new KeyNotFoundException("Tipo de habitación no encontrado.");

        // 3) Fechas
        var checkIn = DateTime.SpecifyKind(request.CheckIn.Date, DateTimeKind.Utc);
        var checkOut = DateTime.SpecifyKind(request.CheckOut.Date, DateTimeKind.Utc);
        if (checkOut.Date <= checkIn.Date)
        {
            throw new ValidationException("La reserva debe cubrir al menos una noche.");
        }
        var nights = Math.Max(1, (int)(checkOut.Date - checkIn.Date).Days);

        // 4) Validar MinStay del RatePlan
        var ratePlan = roomType.RatePlan;
        if (ratePlan is { MinStay: > 1 } && nights < ratePlan.MinStay)
        {
            throw new ValidationException($"El plan «{ratePlan.Name}» exige una estancia mínima de {ratePlan.MinStay} noche(s).");
        }

        // 5) Validar capacidad
        if (request.NumberOfGuests > roomType.Capacity + (request.HasExtraBed ? 1 : 0))
        {
            throw new ValidationException($"El tipo de habitación admite hasta {roomType.Capacity + (request.HasExtraBed ? 1 : 0)} huéspedes.");
        }

        // 6) Encontrar una habitación disponible del tipo para TODAS las noches
        var availableRoom = await FindAvailableRoomAsync(roomType.Id, checkIn, checkOut)
            ?? throw new ValidationException("No hay habitaciones disponibles para las fechas seleccionadas.");

        // 7) Resolver/crear huésped
        var guest = await ResolveOrCreateGuestAsync(request.Guest, request.HotelId);

        // 8) Calcular tarifa: promedio del calendario (RoomRates) * multiplier, o BasePrice * multiplier
        var multiplier = ratePlan?.Multiplier ?? 1m;
        var avgRate = await ResolveCalendarRateAsync(roomType.Id, checkIn, checkOut);
        var roomRate = Math.Round(avgRate * multiplier, 2);

        // 9) Crear reserva
        var reservation = new Reservation
        {
            ReservationNumber = GenerateReservationNumber(),
            CheckInDate = checkIn,
            CheckOutDate = checkOut,
            NumberOfGuests = Math.Max(1, request.NumberOfGuests),
            HasExtraBed = request.HasExtraBed,
            SpecialRequests = request.SpecialRequests,
            Status = ReservationStatus.Confirmed,
            Source = string.IsNullOrWhiteSpace(request.Source) ? "Widget" : request.Source,
            BookingReference = $"WIDGET-{Guid.NewGuid().ToString("N")[..8].ToUpper()}",
            RoomRate = roomRate,
            ExtraBedRate = request.HasExtraBed ? Math.Round(roomRate * 0.15m, 2) : 0,
            TaxRate = hotel.TaxRate ?? 0,
            HotelId = hotel.Id,
            RoomId = availableRoom.Id,
            GuestId = guest.Id,
            RatePlanId = ratePlan?.Id,
            PenaltyAmount = 0,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        reservation.CalculateTotal();

        _context.Reservations.Add(reservation);

        // Domain event (sin actor usuario público)
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
            actorUserId: null));

        await _context.SaveChangesAsync();

        // Disparar automatización (ReservationCreated)
        await _automationService.FireAutomationAsync(hotel.Id, "ReservationCreated", reservation.Id);

        // 10) Respuesta
        return new PublicBookingResponse
        {
            ReservationId = reservation.Id,
            ReservationNumber = reservation.ReservationNumber,
            CheckIn = reservation.CheckInDate,
            CheckOut = reservation.CheckOutDate,
            Nights = reservation.NumberOfNights,
            RoomNumber = availableRoom.RoomNumber,
            RoomTypeName = roomType.Name,
            GuestName = $"{guest.FirstName} {guest.LastName}".Trim(),
            TotalAmount = reservation.TotalAmount,
            Currency = hotel.Currency ?? "USD",
            Payment = new PaymentInfoDto
            {
                Gateway = "Azul",
                Amount = reservation.TotalAmount,
                Currency = hotel.Currency ?? "USD",
                Description = $"Reserva {reservation.ReservationNumber} · {roomType.Name}",
                ChargeUrl = "/api/v1/public/payments/charge"
            }
        };
    }

    // Helper: buscar habitación disponible del tipo para todo el rango
    private async Task<Room?> FindAvailableRoomAsync(Guid roomTypeId, DateTime checkIn, DateTime checkOut)
    {
        var rooms = await _context.Rooms
            .Where(r => r.RoomTypeId == roomTypeId && !r.IsDeleted)
            .Select(r => r.Id)
            .ToListAsync();

        foreach (var roomId in rooms)
        {
            var occupied = await _context.Reservations.AnyAsync(r =>
                r.RoomId == roomId &&
                r.CheckInDate < checkOut &&
                r.CheckOutDate > checkIn &&
                (r.Status == ReservationStatus.Confirmed || r.Status == ReservationStatus.CheckedIn));
            if (!occupied)
            {
                return await _context.Rooms
                    .Include(r => r.RoomType)
                    .FirstAsync(r => r.Id == roomId);
            }
        }
        return null;
    }

    // Helper: huésped por email o nuevo
    private async Task<Guest> ResolveOrCreateGuestAsync(GuestInfoDto dto, Guid hotelId)
    {
        if (!string.IsNullOrWhiteSpace(dto.Email))
        {
            var byEmail = await _context.Guests
                .FirstOrDefaultAsync(g => !g.IsDeleted && g.Email.ToLower() == dto.Email.Trim().ToLower());
            if (byEmail != null)
            {
                UpdateGuest(byEmail, dto);
                return byEmail;
            }
        }

        if (!string.IsNullOrWhiteSpace(dto.DocumentNumber))
        {
            var byDoc = await _context.Guests
                .FirstOrDefaultAsync(g => !g.IsDeleted && g.DocumentNumber == dto.DocumentNumber.Trim());
            if (byDoc != null)
            {
                UpdateGuest(byDoc, dto);
                return byDoc;
            }
        }

        var newGuest = new Guest
        {
            FirstName = dto.FirstName.Trim(),
            LastName = dto.LastName.Trim(),
            Email = dto.Email?.Trim() ?? string.Empty,
            PhoneNumber = dto.PhoneNumber?.Trim() ?? string.Empty,
            DocumentType = string.IsNullOrWhiteSpace(dto.DocumentType) ? "Pasaporte" : dto.DocumentType,
            DocumentNumber = dto.DocumentNumber?.Trim() ?? string.Empty,
            Nationality = dto.Nationality?.Trim() ?? string.Empty,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.Guests.Add(newGuest);
        await _context.SaveChangesAsync();
        return newGuest;
    }

    private static void UpdateGuest(Guest guest, GuestInfoDto dto)
    {
        if (!string.IsNullOrWhiteSpace(dto.FirstName)) guest.FirstName = dto.FirstName.Trim();
        if (!string.IsNullOrWhiteSpace(dto.LastName)) guest.LastName = dto.LastName.Trim();
        if (!string.IsNullOrWhiteSpace(dto.PhoneNumber)) guest.PhoneNumber = dto.PhoneNumber.Trim();
        if (!string.IsNullOrWhiteSpace(dto.DocumentNumber)) guest.DocumentNumber = dto.DocumentNumber.Trim();
        if (!string.IsNullOrWhiteSpace(dto.DocumentType)) guest.DocumentType = dto.DocumentType.Trim();
        if (!string.IsNullOrWhiteSpace(dto.Nationality)) guest.Nationality = dto.Nationality.Trim();
        guest.UpdatedAt = DateTime.UtcNow;
    }

    // Helper: tarifa promedio del calendario (RoomRates) * multiplier, fallback a BasePrice
    private async Task<decimal> ResolveCalendarRateAsync(Guid roomTypeId, DateTime checkIn, DateTime checkOut)
    {
        var from = DateOnly.FromDateTime(checkIn.Date);
        var to = DateOnly.FromDateTime(checkOut.Date.AddDays(-1));
        if (to < from)
        {
            var rt = await _context.RoomTypes.AsNoTracking().FirstOrDefaultAsync(x => x.Id == roomTypeId);
            return rt?.BasePrice ?? 0m;
        }

        var rates = await _context.RoomRates
            .Where(rr => rr.RoomTypeId == roomTypeId && rr.Date >= from && rr.Date <= to)
            .ToDictionaryAsync(rr => rr.Date, rr => rr.Price);

        var nights = to.DayNumber - from.DayNumber + 1;
        var total = 0m;
        for (var day = from; day <= to; day = day.AddDays(1))
        {
            total += rates.TryGetValue(day, out var price) ? price : 0m;
        }

        var rtBase = await _context.RoomTypes.AsNoTracking().FirstOrDefaultAsync(x => x.Id == roomTypeId);
        var basePrice = rtBase?.BasePrice ?? 0m;

        if (total <= 0) return basePrice;

        var average = Math.Round(total / nights, 2);
        return average > 0 ? average : basePrice;
    }

    private static string GenerateReservationNumber()
    {
        var ts = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
        var rand = Random.Shared.Next(100, 999);
        return $"RSV-{ts[^6..]}{rand}";
    }
}