namespace Hospitality.Application.Common.DTOs;

// DTOs básicos para completar API
public class RoomStatsDto
{
    public Guid HotelId { get; set; }
    public decimal AverageDailyRate { get; set; }
    public int TotalNights { get; set; }
    public decimal TotalRevenue { get; set; }
    public decimal OccupancyRate { get; set; }
}

public class RoomHistoryDto
{
    public DateTime Date { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? GuestName { get; set; }
    public decimal? Price { get; set; }
}

public class HotelStatsDto
{
    public Guid HotelId { get; set; }
    public decimal AverageDailyRate { get; set; }
    public decimal RevenuePerAvailableRoom { get; set; }
    public decimal OccupancyRate { get; set; }
    public int TotalGuests { get; set; }
}

public class RoomTypeDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal BasePrice { get; set; }
    public int MaxOccupancy { get; set; }
}

public class HotelNameDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
}