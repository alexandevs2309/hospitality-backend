namespace Hospitality.Application.Guests.DTOs;

public class GuestDto
{
    public Guid Id { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public string DocumentType { get; set; } = string.Empty;
    public string DocumentNumber { get; set; } = string.Empty;
    public string Nationality { get; set; } = string.Empty;
    public string? City { get; set; }
    public string? Country { get; set; }
    public string? SpecialRequests { get; set; }
    public int LoyaltyPoints { get; set; }
    public string? LoyaltyTier { get; set; }
    public bool IsVIP { get; set; }
    public int TotalStays { get; set; }
    public decimal TotalSpent { get; set; }
    public DateTime? LastStayDate { get; set; }
    public int ActiveReservations { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class GuestReservationDto
{
    public Guid Id { get; set; }
    public string ReservationNumber { get; set; } = string.Empty;
    public DateTime CheckInDate { get; set; }
    public DateTime CheckOutDate { get; set; }
    public int NumberOfNights { get; set; }
    public string Status { get; set; } = string.Empty;
    public decimal TotalAmount { get; set; }
    public string RoomNumber { get; set; } = string.Empty;
    public string RoomTypeName { get; set; } = string.Empty;
}