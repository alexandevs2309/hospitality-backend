namespace Hospitality.Application.Rates.Commands;

public class RoomRateDto
{
    public Guid RoomTypeId { get; set; }
    public string RoomTypeName { get; set; } = string.Empty;
    public DateOnly Date { get; set; }
    public decimal BasePrice { get; set; }
    public decimal? OverridePrice { get; set; }
    public decimal EffectivePrice { get; set; }
    public bool IsOverride => OverridePrice.HasValue;
    public Guid? RatePlanId { get; set; }
    public string? RatePlanName { get; set; }
}

public class SetRateRangeCommand
{
    public Guid HotelId { get; set; }
    public Guid RoomTypeId { get; set; }
    public DateOnly From { get; set; }
    public DateOnly To { get; set; }
    public decimal Price { get; set; }
}

public interface IRateService
{
    Task<IReadOnlyList<RoomRateDto>> GetRatesAsync(Guid hotelId, Guid? roomTypeId, DateOnly from, DateOnly to);
    Task<int> SetRateRangeAsync(SetRateRangeCommand command);
    Task<int> ClearRateRangeAsync(Guid hotelId, Guid roomTypeId, DateOnly from, DateOnly to);
}