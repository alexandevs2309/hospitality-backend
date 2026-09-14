namespace Hospitality.Application.PublicWidget.Commands;

public class WidgetRoomTypeDto
{
    public Guid RoomTypeId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int Capacity { get; set; }
    public decimal BasePrice { get; set; }
    public decimal PricePerNight { get; set; }
    public string? Amenities { get; set; }
    public string? ImageUrl { get; set; }
}

public class WidgetConfigDto
{
    public Guid HotelId { get; set; }
    public string HotelName { get; set; } = string.Empty;
    public string Currency { get; set; } = "USD";
    public List<WidgetRoomTypeDto> RoomTypes { get; set; } = new();
}

public class WidgetNightDto
{
    public DateTime Date { get; set; }
    public int AvailableUnits { get; set; }
    public decimal Price { get; set; }
}

public class WidgetRoomAvailabilityDto
{
    public Guid RoomTypeId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int Capacity { get; set; }
    public decimal PricePerNight { get; set; }
    public int AvailableNights { get; set; }
    public List<WidgetNightDto> Nights { get; set; } = new();
}

public class WidgetAvailabilityDto
{
    public Guid HotelId { get; set; }
    public DateTime From { get; set; }
    public DateTime To { get; set; }
    public string Currency { get; set; } = "USD";
    public List<WidgetRoomAvailabilityDto> RoomTypes { get; set; } = new();
}

public class PublicBookingRequest
{
    public Guid HotelId { get; set; }
    public DateTime CheckIn { get; set; }
    public DateTime CheckOut { get; set; }
    public Guid RoomTypeId { get; set; }
    public int NumberOfGuests { get; set; } = 1;
    public bool HasExtraBed { get; set; }
    public string? SpecialRequests { get; set; }
    public GuestInfoDto Guest { get; set; } = new();
    public string Source { get; set; } = "Widget";
}

public class GuestInfoDto
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public string? DocumentType { get; set; }
    public string? DocumentNumber { get; set; }
    public string? Nationality { get; set; }
}

public class PublicBookingResponse
{
    public Guid ReservationId { get; set; }
    public string ReservationNumber { get; set; } = string.Empty;
    public DateTime CheckIn { get; set; }
    public DateTime CheckOut { get; set; }
    public int Nights { get; set; }
    public string RoomNumber { get; set; } = string.Empty;
    public string RoomTypeName { get; set; } = string.Empty;
    public string GuestName { get; set; } = string.Empty;
    public decimal TotalAmount { get; set; }
    public string Currency { get; set; } = string.Empty;
    public PaymentInfoDto Payment { get; set; } = new();
}

public class PaymentInfoDto
{
    public string Gateway { get; set; } = "Azul";
    public decimal Amount { get; set; }
    public string Currency { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string ChargeUrl { get; set; } = "/api/v1/public/payments/charge";
}