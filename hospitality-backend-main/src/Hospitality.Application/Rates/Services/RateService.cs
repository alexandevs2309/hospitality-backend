using Hospitality.Application.Common.Interfaces;
using Hospitality.Application.Rates.Commands;
using Hospitality.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Hospitality.Application.Rates.Services;

public class RateService : IRateService
{
    private readonly IApplicationDbContext _context;

    public RateService(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<RoomRateDto>> GetRatesAsync(Guid hotelId, Guid? roomTypeId, DateOnly from, DateOnly to)
    {
        if (from > to)
        {
            return Array.Empty<RoomRateDto>();
        }

        var days = Enumerable.Range(0, to.DayNumber - from.DayNumber + 1)
            .Select(offset => from.AddDays(offset))
            .ToList();

        var roomTypes = await _context.RoomTypes
            .Include(rt => rt.RatePlan)
            .Where(rt => rt.HotelId == hotelId && (!roomTypeId.HasValue || rt.Id == roomTypeId.Value))
            .OrderBy(rt => rt.Name)
            .ToListAsync();

        if (roomTypes.Count == 0)
        {
            return Array.Empty<RoomRateDto>();
        }

        var typeIds = roomTypes.Select(rt => rt.Id).ToList();
        var overrides = await _context.RoomRates
            .Where(rr => rr.HotelId == hotelId && typeIds.Contains(rr.RoomTypeId) && rr.Date >= from && rr.Date <= to)
            .ToListAsync();

        var overrideLookup = overrides.ToDictionary(rr => (rr.RoomTypeId, rr.Date));

        var result = new List<RoomRateDto>(days.Count * roomTypes.Count);
        foreach (var day in days)
        {
            foreach (var rt in roomTypes)
            {
                var hasOverride = overrideLookup.TryGetValue((rt.Id, day), out var rate);
                var overridePrice = hasOverride ? rate!.Price : (decimal?)null;
                var effective = hasOverride ? rate!.Price : rt.BasePrice;
                result.Add(new RoomRateDto
                {
                    RoomTypeId = rt.Id,
                    RoomTypeName = rt.Name,
                    Date = day,
                    BasePrice = rt.BasePrice,
                    OverridePrice = overridePrice,
                    EffectivePrice = effective,
                    RatePlanId = rt.RatePlan?.Id,
                    RatePlanName = rt.RatePlan?.Name
                });
            }
        }

        return result;
    }

    public async Task<int> SetRateRangeAsync(SetRateRangeCommand command)
    {
        if (command.From > command.To || command.Price <= 0)
        {
            throw new ArgumentException("Rango o tarifa inválidos.");
        }

        var days = Enumerable.Range(0, command.To.DayNumber - command.From.DayNumber + 1)
            .Select(offset => command.From.AddDays(offset))
            .ToList();

        var existing = await _context.RoomRates
            .Where(rr => rr.HotelId == command.HotelId && rr.RoomTypeId == command.RoomTypeId && rr.Date >= command.From && rr.Date <= command.To)
            .ToDictionaryAsync(rr => rr.Date);

        foreach (var day in days)
        {
            if (existing.TryGetValue(day, out var rate))
            {
                rate.Price = command.Price;
                rate.UpdatedAt = DateTime.UtcNow;
            }
            else
            {
                _context.RoomRates.Add(new RoomRate
                {
                    Id = Guid.NewGuid(),
                    HotelId = command.HotelId,
                    RoomTypeId = command.RoomTypeId,
                    Date = day,
                    Price = command.Price,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                });
            }
        }

        await _context.SaveChangesAsync();
        return days.Count;
    }

    public async Task<int> ClearRateRangeAsync(Guid hotelId, Guid roomTypeId, DateOnly from, DateOnly to)
    {
        if (from > to)
        {
            return 0;
        }

        var rates = await _context.RoomRates
            .Where(rr => rr.HotelId == hotelId && rr.RoomTypeId == roomTypeId && rr.Date >= from && rr.Date <= to)
            .ToListAsync();

        _context.RoomRates.RemoveRange(rates);
        await _context.SaveChangesAsync();
        return rates.Count;
    }
}