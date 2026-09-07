using Hospitality.Application.Common.DTOs;

namespace Hospitality.Application.Rooms.Commands;

public class RoomDto
{
    public Guid Id { get; set; }
    public string RoomNumber { get; set; } = string.Empty;
    public int Floor { get; set; }
    public string? Description { get; set; }
    public decimal Price { get; set; }
    public int MaxOccupancy { get; set; }
    public string Status { get; set; } = "Available";
    public bool IsClean { get; set; } = true;
    public bool IsMaintenanceRequired { get; set; } = false;
    public Guid RoomTypeId { get; set; }
    public string RoomTypeName { get; set; } = string.Empty;
    public Guid HotelId { get; set; }
    public string HotelName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public string? MaintenanceNotes { get; set; }
}

public interface IRoomService
{
    Task<PaginatedResult<RoomDto>> GetRoomsAsync(PaginatedQuery query, Guid? hotelScope = null);
    Task<RoomDto?> GetRoomByIdAsync(Guid id);
    Task<List<RoomDto>> GetRoomsByHotelAsync(Guid hotelId);
    Task<List<RoomDto>> GetAvailableRoomsAsync(Guid hotelId, DateTime startDate, DateTime endDate, Guid? roomTypeId = null);
    Task<RoomDto> CreateRoomAsync(CreateRoomCommand command);
    Task<RoomDto> UpdateRoomAsync(UpdateRoomCommand command);
    Task<RoomDto> UpdateRoomStatusAsync(Guid id, UpdateRoomStatusCommand command);
    Task<RoomDto> MarkRoomAsDirtyAsync(Guid roomId, string? reason = null);
    Task<RoomDto> MarkRoomAsCleanAsync(Guid roomId);
    Task<RoomDto> RequestMaintenanceAsync(Guid roomId, RequestMaintenanceCommand command);
    Task<RoomDto> CompleteMaintenanceAsync(Guid roomId);
    Task<List<RoomHistoryDto>> GetRoomHistoryAsync(Guid roomId, DateTime? from = null, DateTime? to = null);
    Task<RoomStatsDto> GetRoomStatsAsync(Guid hotelId);
    Task<List<RoomDto>> SearchRoomsAsync(Guid hotelId, string? roomNumber, string? status, int? floor, Guid? roomTypeId);
}