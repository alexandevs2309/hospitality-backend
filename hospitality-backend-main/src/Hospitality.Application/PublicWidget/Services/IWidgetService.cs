using Hospitality.Application.PublicWidget.Commands;

namespace Hospitality.Application.PublicWidget.Services;

public interface IWidgetService
{
    Task<WidgetConfigDto> GetConfigAsync(Guid hotelId);
    Task<WidgetAvailabilityDto> GetAvailabilityAsync(Guid hotelId, DateTime from, DateTime to);
    Task<PublicBookingResponse> CreatePublicBookingAsync(PublicBookingRequest request);
}