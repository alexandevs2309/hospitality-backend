using Hospitality.Application.Common.DTOs;

namespace Hospitality.Application.Reservations.Commands;

public class ReservationDto
{
    public Guid Id { get; set; }
    public string ReservationNumber { get; set; } = string.Empty;
    public DateTime CheckInDate { get; set; }
    public DateTime CheckOutDate { get; set; }
    public int NumberOfNights { get; set; }
    public int NumberOfGuests { get; set; }
    public bool HasExtraBed { get; set; }
    public string? SpecialRequests { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime? CheckedInAt { get; set; }
    public DateTime? CheckedOutAt { get; set; }
    public DateTime? CancelledAt { get; set; }
    public string? CancellationReason { get; set; }
    public string Source { get; set; } = "Directo";
    public string? BookingReference { get; set; }
    public decimal RoomRate { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal AmountPaid { get; set; }
    public decimal BalanceDue { get; set; }
    public bool IsFullyPaid { get; set; }
    public decimal TaxRate { get; set; }

    public Guid HotelId { get; set; }
    public string HotelName { get; set; } = string.Empty;

    public Guid RoomId { get; set; }
    public string RoomNumber { get; set; } = string.Empty;
    public string RoomTypeName { get; set; } = string.Empty;

    public Guid GuestId { get; set; }
    public string GuestName { get; set; } = string.Empty;
    public string GuestEmail { get; set; } = string.Empty;
    public string GuestPhone { get; set; } = string.Empty;

    public Guid? RatePlanId { get; set; }
    public string? RatePlanName { get; set; }
    public decimal PenaltyAmount { get; set; }

    public DateTime CreatedAt { get; set; }
}

public interface IReservationService
{
    Task<PaginatedResult<ReservationDto>> GetReservationsAsync(
        PaginatedQuery query,
        Guid? hotelScope = null,
        string? status = null,
        DateTime? from = null,
        DateTime? to = null,
        string? search = null);
    Task<ReservationDto?> GetReservationByIdAsync(Guid id);
    Task<ReservationDto> CreateReservationAsync(CreateReservationCommand command);
    Task<ReservationDto> ConfirmReservationAsync(Guid id);
    Task<ReservationDto> CheckInReservationAsync(Guid id);
    Task<ReservationDto> CheckOutReservationAsync(Guid id);
    Task<ReservationDto> CancelReservationAsync(Guid id, string? reason);
    Task<List<ReservationDto>> GetReservationsByGuestAsync(Guid guestId, Guid? hotelScope = null);
}