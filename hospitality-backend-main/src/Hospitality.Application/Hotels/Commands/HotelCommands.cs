namespace Hospitality.Application.Hotels.Commands;

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
}

public class UpdateHotelStatusCommand
{
    public bool IsActive { get; set; }
}

