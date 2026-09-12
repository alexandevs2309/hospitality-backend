using Hospitality.Application.Common.DTOs;

namespace Hospitality.Application.Hotels.Commands;

public class HotelDto
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
    public int AvailableRooms { get; set; }
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
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public interface IHotelService
{
    Task<PaginatedResult<HotelDto>> GetHotelsAsync(PaginatedQuery query, Guid? hotelScope = null);
    Task<HotelDto?> GetHotelByIdAsync(Guid id);
    Task<HotelDto> CreateHotelAsync(CreateHotelCommand command);
    Task<HotelDto> UpdateHotelAsync(UpdateHotelCommand command);
    Task<bool> DeleteHotelAsync(Guid id);
    Task<HotelDto> UpdateHotelStatusAsync(Guid id, UpdateHotelStatusCommand command);
    Task<HotelStatsDto> GetHotelStatsAsync(Guid id);
    Task<List<RoomTypeDto>> GetHotelRoomTypesAsync(Guid id);
    Task<RoomTypeDto> CreateHotelRoomTypeAsync(UpsertRoomTypeCommand command);
    Task<RoomTypeDto> UpdateHotelRoomTypeAsync(UpsertRoomTypeCommand command);
    Task DeleteHotelRoomTypeAsync(Guid hotelId, Guid roomTypeId);
    Task<List<HotelNameDto>> GetHotelNamesAsync(Guid? hotelScope = null);
    Task<List<HotelDto>> SearchHotelsAsync(string? name, string? city, int? minStars, bool? isActive, Guid? hotelScope = null);
}