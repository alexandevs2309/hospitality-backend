namespace Hospitality.Application.Onboarding.Commands;

public class OnboardingStatusDto
{
    public Guid HotelId { get; set; }
    public string HotelName { get; set; } = string.Empty;
    public string Currency { get; set; } = "DOP";
    public decimal? TaxRate { get; set; }

    public bool HasRoomTypes { get; set; }
    public int RoomTypeCount { get; set; }
    public int RoomCount { get; set; }

    public bool HasRatePlan { get; set; }
    public int RatePlanCount { get; set; }

    public bool HasChannel { get; set; }
    public int ChannelCount { get; set; }

    public bool AllComplete { get; set; }
    public int CompletedSteps { get; set; }
    public int TotalSteps { get; set; } = 4;
}

public interface IOnboardingService
{
    Task<OnboardingStatusDto> GetStatusAsync(Guid hotelId);
}