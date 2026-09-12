using Hospitality.Application.Common.Interfaces;
using Hospitality.Application.Onboarding.Commands;
using Microsoft.EntityFrameworkCore;

namespace Hospitality.Application.Onboarding.Services;

public class OnboardingService : IOnboardingService
{
    private readonly IApplicationDbContext _context;

    public OnboardingService(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<OnboardingStatusDto> GetStatusAsync(Guid hotelId)
    {
        var hotel = await _context.ActiveHotels().FirstOrDefaultAsync(h => h.Id == hotelId)
            ?? throw new KeyNotFoundException($"Hotel con ID {hotelId} no encontrado.");

        var roomTypeCount = await _context.RoomTypes.CountAsync(rt => rt.HotelId == hotelId);
        var roomCount = await _context.Rooms.CountAsync(r => r.HotelId == hotelId && !r.IsDeleted);
        var ratePlanCount = await _context.RatePlans.CountAsync(p => p.HotelId == hotelId);
        var hasCalendarRates = await _context.RoomRates.AnyAsync(rr => rr.HotelId == hotelId);
        var channelCount = await _context.Channels.CountAsync(c => c.HotelId == hotelId);

        var hasRoomTypes = roomTypeCount > 0;
        var hasRates = hasRoomTypes && (hasCalendarRates || ratePlanCount > 0);
        var hasChannels = channelCount > 0;

        var completed = 1 + new[] { hasRoomTypes && roomCount > 0, hasRates, hasChannels }
            .Count(c => c);

        return new OnboardingStatusDto
        {
            HotelId = hotel.Id,
            HotelName = hotel.Name,
            Currency = hotel.Currency ?? "DOP",
            TaxRate = hotel.TaxRate,
            HasRoomTypes = hasRoomTypes,
            RoomTypeCount = roomTypeCount,
            RoomCount = roomCount,
            HasRatePlan = ratePlanCount > 0,
            RatePlanCount = ratePlanCount,
            HasChannel = hasChannels,
            ChannelCount = channelCount,
            AllComplete = hasRoomTypes && roomCount > 0 && hasRates && hasChannels,
            CompletedSteps = completed
        };
    }
}