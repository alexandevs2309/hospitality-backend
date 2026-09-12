namespace Hospitality.Application.Hotels.Commands;

public class UpsertRoomTypeCommand
{
    public Guid? RoomTypeId { get; set; }
    public Guid HotelId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal BasePrice { get; set; }
    public int Capacity { get; set; } = 1;
    public int? ExtraBedCapacity { get; set; }
    public decimal? ExtraBedPrice { get; set; }
}

public class CreateHotelCommand
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Address { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? Website { get; set; }
    public int StarRating { get; set; } = 3;
    public int TotalRooms { get; set; }
    public string TimeZone { get; set; } = "UTC";
    public string City { get; set; } = string.Empty;
    public string Country { get; set; } = string.Empty;
    public string? BusinessName { get; set; }
    public int? YearOpened { get; set; }
    public string? PostalCode { get; set; }
    public string? Currency { get; set; } = "USD";
    public decimal? TaxRate { get; set; }
    public string? CheckInTime { get; set; }
    public string? CheckOutTime { get; set; }
    public string? HotelLanguages { get; set; }
    public string? SelectedModules { get; set; }
}

public class UpdateHotelCommand
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Address { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? Website { get; set; }
    public int StarRating { get; set; }
    public int TotalRooms { get; set; }
    public bool IsActive { get; set; }
    public string TimeZone { get; set; } = "UTC";
    public string City { get; set; } = string.Empty;
    public string Country { get; set; } = string.Empty;
    public string? BusinessName { get; set; }
    public int? YearOpened { get; set; }
    public string? PostalCode { get; set; }
    public string? Currency { get; set; } = "USD";
    public decimal? TaxRate { get; set; }
    public string? CheckInTime { get; set; }
    public string? CheckOutTime { get; set; }
    public string? HotelLanguages { get; set; }
    public string? SelectedModules { get; set; }
}

public class UpdateHotelStatusCommand
{
    public bool IsActive { get; set; }
}

