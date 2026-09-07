namespace Hospitality.Application.Rooms.Commands;

public class CreateRoomCommand
{
    public string RoomNumber { get; set; } = string.Empty;
    public int Floor { get; set; }
    public string? Description { get; set; }
    public decimal Price { get; set; }
    public int MaxOccupancy { get; set; }
    public Guid RoomTypeId { get; set; }
    public Guid HotelId { get; set; }
}

public class UpdateRoomCommand
{
    public Guid Id { get; set; }
    public string RoomNumber { get; set; } = string.Empty;
    public int Floor { get; set; }
    public string? Description { get; set; }
    public decimal Price { get; set; }
    public int MaxOccupancy { get; set; }
    public Guid RoomTypeId { get; set; }
    public Guid HotelId { get; set; }
}

public class UpdateRoomStatusCommand
{
    public string Status { get; set; } = string.Empty;
    public bool IsClean { get; set; }
    public bool IsMaintenanceRequired { get; set; }
    public string? Notes { get; set; }
}

public class RequestMaintenanceCommand
{
    public string Description { get; set; } = string.Empty;
    public string Priority { get; set; } = "Medium";
}