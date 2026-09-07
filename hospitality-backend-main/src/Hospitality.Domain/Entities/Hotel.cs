namespace Hospitality.Domain.Entities;

public class Hotel : BaseEntity, ISoftDelete
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Address { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? Website { get; set; }
    public int StarRating { get; set; } = 3;
    public int TotalRooms { get; set; }
    public bool IsActive { get; set; } = true;
    public string TimeZone { get; set; } = "UTC";
    public string City { get; set; } = string.Empty;
    public string Country { get; set; } = string.Empty;
    
    // Soft delete
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }

    // Navigation properties
    public ICollection<Room> Rooms { get; set; } = new List<Room>();
    public ICollection<RoomType> RoomTypes { get; set; } = new List<RoomType>();
    public ICollection<HotelMetrics> Metrics { get; set; } = new List<HotelMetrics>();
    
    // Métodos de negocio
    public int GetAvailableRooms()
    {
        return Rooms.Count(r => r.Status == Enums.RoomStatus.Available && !r.IsMaintenanceRequired);
    }

    public decimal GetCurrentOccupancyRate()
    {
        if (TotalRooms == 0) return 0;
        var occupiedRooms = Rooms.Count(r => r.Status == Enums.RoomStatus.Occupied);
        return (decimal)occupiedRooms / TotalRooms * 100;
    }
}