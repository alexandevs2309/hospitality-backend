using Hospitality.Domain.Enums;

namespace Hospitality.Application.Reservations.Commands;

public class CreateReservationCommand
{
    public Guid HotelId { get; set; }
    public Guid RoomId { get; set; }
    public DateTime CheckInDate { get; set; }
    public DateTime CheckOutDate { get; set; }
    public int NumberOfGuests { get; set; } = 1;
    public bool HasExtraBed { get; set; }
    public string? SpecialRequests { get; set; }
    public string Source { get; set; } = "Directo";
    public string? BookingReference { get; set; }
    public decimal? RoomRate { get; set; }

    // Huésped principal
    public string GuestFirstName { get; set; } = string.Empty;
    public string GuestLastName { get; set; } = string.Empty;
    public string GuestEmail { get; set; } = string.Empty;
    public string GuestPhone { get; set; } = string.Empty;
    public string? GuestDocumentType { get; set; }
    public string? GuestDocumentNumber { get; set; }
    public string? GuestNationality { get; set; }
    public string? GuestCity { get; set; }
    public string? GuestCountry { get; set; }
}

public class CancelReservationCommand
{
    public string? Reason { get; set; }
}

public class CheckInClientReservationCommand
{
    public string? Notes { get; set; }
}