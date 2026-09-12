using Hospitality.Application.Common.DTOs;
using Hospitality.Application.Guests.DTOs;

namespace Hospitality.Application.Guests.Commands;

public class CreateGuestCommand
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public string DocumentType { get; set; } = "DNI";
    public string DocumentNumber { get; set; } = string.Empty;
    public string Nationality { get; set; } = string.Empty;
    public string? City { get; set; }
    public string? Country { get; set; }
    public string? Address { get; set; }
    public string? SpecialRequests { get; set; }
    public string? Preferences { get; set; }
}

public class UpdateGuestCommand : CreateGuestCommand
{
    public Guid Id { get; set; }
}

public interface IGuestService
{
    Task<PaginatedResult<GuestDto>> GetGuestsAsync(
        PaginatedQuery query,
        Guid? hotelScope = null,
        string? search = null);

    Task<GuestDto?> GetGuestByIdAsync(Guid id, Guid? hotelScope = null);

    Task<List<GuestReservationDto>> GetGuestReservationsAsync(Guid guestId, Guid? hotelScope = null);

    Task<GuestDto> CreateGuestAsync(CreateGuestCommand command);

    Task<GuestDto> UpdateGuestAsync(UpdateGuestCommand command);
}