using Hospitality.Application.Common.DTOs;
using Hospitality.Application.Common.Interfaces;
using Hospitality.Application.Guests.Commands;
using Hospitality.Application.Guests.DTOs;
using Hospitality.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Hospitality.Application.Guests.Services;

public class GuestService : IGuestService
{
    private readonly IApplicationDbContext _context;

    public GuestService(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<PaginatedResult<GuestDto>> GetGuestsAsync(
        PaginatedQuery query,
        Guid? hotelScope = null,
        string? search = null)
    {
        var dbQuery = _context.Guests
            .Include(g => g.Reservations)
            .Where(g => !g.IsDeleted);

        if (hotelScope.HasValue)
        {
            dbQuery = dbQuery.Where(g => g.Reservations.Any(r => r.HotelId == hotelScope.Value));
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            dbQuery = dbQuery.Where(g =>
                (g.FirstName + " " + g.LastName).ToLower().Contains(term) ||
                g.Email.ToLower().Contains(term) ||
                g.PhoneNumber.ToLower().Contains(term) ||
                g.DocumentNumber.ToLower().Contains(term));
        }

        var totalCount = await dbQuery.CountAsync();

        var items = await dbQuery
            .OrderByDescending(g => g.CreatedAt)
            .Skip((query.PageNumber - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync();

        return new PaginatedResult<GuestDto>(
            items.Select(g => MapToDto(g, hotelScope)).ToList(),
            totalCount,
            query.PageNumber,
            query.PageSize);
    }

    public async Task<GuestDto?> GetGuestByIdAsync(Guid id, Guid? hotelScope = null)
    {
        var guest = await _context.Guests
            .Include(g => g.Reservations).ThenInclude(r => r.Room).ThenInclude(r => r.RoomType)
            .Include(g => g.Reservations).ThenInclude(r => r.Room).ThenInclude(r => r.Hotel)
            .FirstOrDefaultAsync(g => g.Id == id && !g.IsDeleted);

        if (guest == null) return null;

        if (hotelScope.HasValue &&
            !guest.Reservations.Any(r => r.HotelId == hotelScope.Value))
        {
            return null;
        }

        return MapToDto(guest, hotelScope);
    }

    public async Task<List<GuestReservationDto>> GetGuestReservationsAsync(Guid guestId, Guid? hotelScope = null)
    {
        var query = _context.Reservations
            .Include(r => r.Room).ThenInclude(r => r.RoomType)
            .Where(r => r.GuestId == guestId);

        if (hotelScope.HasValue)
        {
            query = query.Where(r => r.HotelId == hotelScope.Value);
        }

        var reservations = await query
            .OrderByDescending(r => r.CheckInDate)
            .ToListAsync();

        return reservations.Select(r => new GuestReservationDto
        {
            Id = r.Id,
            ReservationNumber = r.ReservationNumber,
            CheckInDate = r.CheckInDate,
            CheckOutDate = r.CheckOutDate,
            NumberOfNights = r.NumberOfNights,
            Status = r.Status.ToString(),
            TotalAmount = r.TotalAmount,
            RoomNumber = r.Room?.RoomNumber ?? string.Empty,
            RoomTypeName = r.Room?.RoomType?.Name ?? string.Empty
        }).ToList();
    }

    public async Task<GuestDto> CreateGuestAsync(CreateGuestCommand command)
    {
        if (string.IsNullOrWhiteSpace(command.FirstName))
        {
            throw new Domain.Exceptions.ValidationException("El nombre del huésped es obligatorio.");
        }

        var guest = new Guest
        {
            FirstName = command.FirstName.Trim(),
            LastName = command.LastName?.Trim() ?? string.Empty,
            Email = command.Email?.Trim() ?? string.Empty,
            PhoneNumber = command.PhoneNumber?.Trim() ?? string.Empty,
            DocumentType = string.IsNullOrWhiteSpace(command.DocumentType) ? "DNI" : command.DocumentType,
            DocumentNumber = command.DocumentNumber?.Trim() ?? string.Empty,
            Nationality = command.Nationality?.Trim() ?? string.Empty,
            City = command.City?.Trim(),
            Country = command.Country?.Trim(),
            Address = command.Address?.Trim(),
            SpecialRequests = command.SpecialRequests?.Trim(),
            Preferences = command.Preferences?.Trim(),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.Guests.Add(guest);
        await _context.SaveChangesAsync();

        return MapToDto(guest, null);
    }

    public async Task<GuestDto> UpdateGuestAsync(UpdateGuestCommand command)
    {
        var guest = await _context.Guests
            .Include(g => g.Reservations)
            .FirstOrDefaultAsync(g => g.Id == command.Id && !g.IsDeleted)
            ?? throw new KeyNotFoundException($"Huésped con ID {command.Id} no encontrado.");

        if (!string.IsNullOrWhiteSpace(command.FirstName)) guest.FirstName = command.FirstName.Trim();
        if (!string.IsNullOrWhiteSpace(command.LastName)) guest.LastName = command.LastName.Trim();
        if (!string.IsNullOrWhiteSpace(command.Email)) guest.Email = command.Email.Trim();
        if (!string.IsNullOrWhiteSpace(command.PhoneNumber)) guest.PhoneNumber = command.PhoneNumber.Trim();
        if (!string.IsNullOrWhiteSpace(command.DocumentType)) guest.DocumentType = command.DocumentType;
        if (!string.IsNullOrWhiteSpace(command.DocumentNumber)) guest.DocumentNumber = command.DocumentNumber.Trim();
        if (!string.IsNullOrWhiteSpace(command.Nationality)) guest.Nationality = command.Nationality.Trim();
        if (command.City != null) guest.City = command.City.Trim();
        if (command.Country != null) guest.Country = command.Country.Trim();
        if (command.Address != null) guest.Address = command.Address.Trim();
        if (command.SpecialRequests != null) guest.SpecialRequests = command.SpecialRequests.Trim();
        if (command.Preferences != null) guest.Preferences = command.Preferences.Trim();
        guest.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return MapToDto(guest, null);
    }

    private static GuestDto MapToDto(Guest g, Guid? hotelScope)
    {
        var reservations = hotelScope.HasValue
            ? g.Reservations.Where(r => r.HotelId == hotelScope.Value).ToList()
            : g.Reservations.ToList();

        var completed = reservations.Where(r => r.Status == Domain.Enums.ReservationStatus.CheckedOut).ToList();
        var active = reservations.Count(r =>
            r.Status == Domain.Enums.ReservationStatus.Confirmed ||
            r.Status == Domain.Enums.ReservationStatus.CheckedIn);

        return new GuestDto
        {
            Id = g.Id,
            FirstName = g.FirstName,
            LastName = g.LastName,
            FullName = g.FullName,
            Email = g.Email,
            PhoneNumber = g.PhoneNumber,
            DocumentType = g.DocumentType,
            DocumentNumber = g.DocumentNumber,
            Nationality = g.Nationality,
            City = g.City,
            Country = g.Country,
            SpecialRequests = g.SpecialRequests,
            LoyaltyPoints = g.LoyaltyPoints,
            LoyaltyTier = g.LoyaltyTier ?? "Standard",
            IsVIP = g.IsVIP,
            TotalStays = completed.Count,
            TotalSpent = completed.Sum(r => r.TotalAmount),
            LastStayDate = completed.Count > 0 ? completed.Max(r => r.CheckOutDate) : null,
            ActiveReservations = active,
            CreatedAt = g.CreatedAt
        };
    }
}