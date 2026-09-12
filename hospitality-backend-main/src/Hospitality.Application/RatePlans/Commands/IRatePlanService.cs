using Hospitality.Domain.Enums;

namespace Hospitality.Application.RatePlans.Commands;

public class RatePlanDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal Multiplier { get; set; }
    public int MinStay { get; set; }
    public RefundabilityType Refundability { get; set; }
    public int CancellationDeadlineHours { get; set; }
    public bool IsDefault { get; set; }
    public int UsageCount { get; set; }
    public string RefundabilityLabel => Refundability switch
    {
        RefundabilityType.NonRefundable => "No reembolsable",
        RefundabilityType.Moderate => "Moderada",
        _ => "Flexible"
    };
}

public class UpsertRatePlanCommand
{
    public Guid Id { get; set; }
    public Guid HotelId { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal? Multiplier { get; set; }
    public int? MinStay { get; set; }
    public RefundabilityType? Refundability { get; set; }
    public int? CancellationDeadlineHours { get; set; }
    public bool IsDefault { get; set; }
}

public class AssignRatePlanCommand
{
    public List<Guid> RoomTypeIds { get; set; } = new();
}

public interface IRatePlanService
{
    Task<IReadOnlyList<RatePlanDto>> GetPlansAsync(Guid hotelId);
    Task<RatePlanDto> CreatePlanAsync(UpsertRatePlanCommand command);
    Task<RatePlanDto> UpdatePlanAsync(Guid id, UpsertRatePlanCommand command);
    Task DeletePlanAsync(Guid id);
    Task<int> AssignPlanAsync(Guid id, List<Guid> roomTypeIds);
}