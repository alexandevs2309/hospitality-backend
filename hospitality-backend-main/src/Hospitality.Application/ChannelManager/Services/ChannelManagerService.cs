using Hospitality.Application.ChannelManager.Adapters;
using Hospitality.Application.ChannelManager.Commands;
using Hospitality.Application.Common.Interfaces;
using Hospitality.Domain.Entities;
using Hospitality.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Hospitality.Application.ChannelManager.Services;

public class ChannelManagerService : IChannelManagerService
{
    private readonly IApplicationDbContext _context;
    private readonly ChannelAdapterFactory _adapterFactory;

    public ChannelManagerService(IApplicationDbContext context, ChannelAdapterFactory adapterFactory)
    {
        _context = context;
        _adapterFactory = adapterFactory;
    }

    public async Task<bool> TestChannelConnectionAsync(Guid channelId)
    {
        var channel = await _context.Channels
            .FirstOrDefaultAsync(c => c.Id == channelId);
        if (channel == null || string.IsNullOrEmpty(channel.CredentialsJson))
        {
            return false;
        }

        var adapter = _adapterFactory.GetAdapter(channel.ChannelType, channel.Name);
        if (adapter == null)
        {
            return false;
        }

        return await adapter.TestConnectionAsync(channel.CredentialsJson);
    }

    public async Task<bool> TestConnectionAsync(Guid channelId)
    {
        var channel = await _context.Channels.FirstOrDefaultAsync(c => c.Id == channelId);
        if (channel == null || string.IsNullOrEmpty(channel.CredentialsJson))
        {
            return false;
        }
        var adapter = _adapterFactory.GetAdapter(channel.ChannelType, channel.Name);
        if (adapter == null) return false;
        return await adapter.TestConnectionAsync(channel.CredentialsJson);
    }

    public async Task<Dictionary<string, ChannelRoomTypeMap>> FetchRoomTypeMapsAsync(Guid channelId)
    {
        return await FetchChannelRoomTypesAsync(channelId);
    }

    public async Task<Dictionary<string, ChannelRoomTypeMap>> FetchChannelRoomTypesAsync(Guid channelId)
    {
        var channel = await _context.Channels
            .FirstOrDefaultAsync(c => c.Id == channelId);
        if (channel == null || string.IsNullOrEmpty(channel.CredentialsJson))
        {
            return new Dictionary<string, ChannelRoomTypeMap>();
        }

        var adapter = _adapterFactory.GetAdapter(channel.ChannelType, channel.Name);
        if (adapter == null)
        {
            return new Dictionary<string, ChannelRoomTypeMap>();
        }

        return await adapter.FetchRoomTypeMapsAsync(channel.CredentialsJson);
    }

    public async Task<PushResultDto> PushAvailabilityAsync(Guid channelId, DateTime from, DateTime to)
    {
        var channel = await _context.Channels
            .Include(c => c.Mappings)
            .ThenInclude(m => m.RoomType)
            .FirstOrDefaultAsync(c => c.Id == channelId);

        if (channel == null || string.IsNullOrEmpty(channel.CredentialsJson))
        {
            return new PushResultDto { Success = false, Message = "Canal no configurado o sin credenciales." };
        }

        var adapter = _adapterFactory.GetAdapter(channel.ChannelType, channel.Name);
        if (adapter == null)
        {
            return new PushResultDto { Success = false, Message = $"No hay adaptador para el canal {channel.Name}." };
        }

        var mappings = channel.Mappings.Where(m => m.IsActive).ToList();
        if (!mappings.Any())
        {
            return new PushResultDto { Success = false, Message = "No hay mapeos de habitaciones configurados para este canal." };
        }

        var updates = new List<AvailabilityUpdate>();
        var days = (to.Date - from.Date).Days + 1;

        for (var i = 0; i < days; i++)
        {
            var date = from.Date.AddDays(i);
            foreach (var mapping in mappings)
            {
                var available = await CalculateAvailabilityAsync(mapping.RoomTypeId, date);
                updates.Add(new AvailabilityUpdate(
                    mapping.ChannelRoomCode,
                    mapping.ChannelRatePlanCode ?? string.Empty,
                    date,
                    available
                ));
            }
        }

        await adapter.PushAvailabilityAsync(channel.CredentialsJson, updates);

        return new PushResultDto { Success = true, Message = "Disponibilidad enviada correctamente.", ItemsPushed = updates.Count };
    }

    public async Task<PushResultDto> PushRatesAsync(Guid channelId, DateTime from, DateTime to)
    {
        var channel = await _context.Channels
            .Include(c => c.Mappings)
            .ThenInclude(m => m.RoomType)
            .FirstOrDefaultAsync(c => c.Id == channelId);

        if (channel == null || string.IsNullOrEmpty(channel.CredentialsJson))
        {
            return new PushResultDto { Success = false, Message = "Canal no configurado o sin credenciales." };
        }

        var adapter = _adapterFactory.GetAdapter(channel.ChannelType, channel.Name);
        if (adapter == null)
        {
            return new PushResultDto { Success = false, Message = $"No hay adaptador para el canal {channel.Name}." };
        }

        var mappings = channel.Mappings.Where(m => m.IsActive).ToList();
        if (!mappings.Any())
        {
            return new PushResultDto { Success = false, Message = "No hay mapeos de habitaciones configurados para este canal." };
        }

        var updates = new List<RateUpdate>();
        foreach (var mapping in mappings)
        {
            var roomType = mapping.RoomType;
            var ratePlan = roomType.RatePlan;
            var basePrice = ratePlan != null
                ? roomType.BasePrice * ratePlan.Multiplier
                : roomType.BasePrice;

            updates.Add(new RateUpdate(
                mapping.ChannelRoomCode,
                mapping.ChannelRatePlanCode ?? string.Empty,
                from,
                to,
                basePrice,
                ratePlan?.MinStay,
                roomType.Hotel?.Currency ?? "USD"
            ));
        }

        await adapter.PushRatesAsync(channel.CredentialsJson, updates);

        return new PushResultDto { Success = true, Message = "Tarifas enviadas correctamente.", ItemsPushed = updates.Count };
    }

    public async Task<List<BookingPullDto>> PullBookingsAsync(Guid channelId, DateTime from, DateTime to)
    {
        var channel = await _context.Channels
            .FirstOrDefaultAsync(c => c.Id == channelId);
        if (channel == null || string.IsNullOrEmpty(channel.CredentialsJson))
        {
            throw new InvalidOperationException("Canal no configurado o sin credenciales.");
        }

        var adapter = _adapterFactory.GetAdapter(channel.ChannelType, channel.Name);
        if (adapter == null)
        {
            throw new InvalidOperationException($"No hay adaptador para el canal {channel.Name}.");
        }

        var results = await adapter.PullBookingsAsync(channel.CredentialsJson, from, to);

        return results.Select(r => new BookingPullDto
        {
            ExternalBookingId = r.ExternalBookingId,
            ChannelRoomCode = r.ChannelRoomCode,
            ChannelRatePlanCode = r.ChannelRatePlanCode,
            CheckIn = r.CheckIn,
            CheckOut = r.CheckOut,
            Adults = r.Adults,
            Children = r.Children,
            GuestName = r.GuestName,
            GuestEmail = r.GuestEmail,
            GuestPhone = r.GuestPhone,
            TotalPrice = r.TotalPrice,
            Currency = r.Currency,
            RawData = r.RawData
        }).ToList();
    }

    public async Task<PushResultDto> CreateMappingsAsync(Guid channelId)
    {
        var maps = await FetchChannelRoomTypesAsync(channelId);
        await CreateMappingsFromFetchAsync(channelId, maps);
        return new PushResultDto { Success = true, Message = $"Mapeos creados: {maps.Count}", ItemsPushed = maps.Count };
    }

    public async Task CreateMappingsFromFetchAsync(Guid channelId, Dictionary<string, ChannelRoomTypeMap> fetchedMaps)
    {
        var channel = await _context.Channels
            .Include(c => c.Mappings)
            .FirstOrDefaultAsync(c => c.Id == channelId);
        if (channel == null)
        {
            throw new KeyNotFoundException("Canal no encontrado.");
        }

        var existingCodes = channel.Mappings.Select(m => m.ChannelRoomCode).ToHashSet();

        foreach (var (channelCode, map) in fetchedMaps)
        {
            if (existingCodes.Contains(channelCode))
            {
                continue;
            }

            var prefix = channelCode.Split('_')[0];
            var normalized = channelCode.Replace("EXP_", "").Replace("DBL", "Doble").Replace("SGL", "Sencilla").Replace("STE", "Suite");

            var roomType = await _context.RoomTypes
                .FirstOrDefaultAsync(rt => rt.Name.Contains(prefix, StringComparison.OrdinalIgnoreCase)
                    || rt.Name.Contains(normalized, StringComparison.OrdinalIgnoreCase));

            if (roomType != null)
            {
                channel.Mappings.Add(new ChannelMapping
                {
                    Id = Guid.NewGuid(),
                    ChannelId = channelId,
                    RoomTypeId = roomType.Id,
                    ChannelRoomCode = channelCode,
                    ChannelRatePlanCode = map.ChannelRatePlanCode,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                });
            }
        }

        await _context.SaveChangesAsync();
    }

    private async Task<int> CalculateAvailabilityAsync(Guid roomTypeId, DateTime date)
    {
        var totalRooms = await _context.Rooms
            .CountAsync(r => r.RoomTypeId == roomTypeId && !r.IsDeleted);

        if (totalRooms == 0) return 0;

        var occupied = await _context.Reservations
            .CountAsync(r => r.Room.RoomTypeId == roomTypeId &&
                             r.CheckInDate.Date <= date &&
                             r.CheckOutDate.Date > date &&
                             (r.Status == ReservationStatus.Confirmed ||
                              r.Status == ReservationStatus.CheckedIn));

        return Math.Max(0, totalRooms - occupied);
    }

    public async Task<List<ChannelMappingDto>> GetMappingsAsync(Guid channelId)
    {
        return await _context.ChannelMappings
            .Where(m => m.ChannelId == channelId)
            .Include(m => m.RoomType)
            .Include(m => m.Channel)
            .Select(m => new ChannelMappingDto
            {
                Id = m.Id,
                ChannelId = m.ChannelId,
                ChannelName = m.Channel.Name,
                RoomTypeId = m.RoomTypeId,
                RoomTypeName = m.RoomType.Name,
                ChannelRoomCode = m.ChannelRoomCode,
                ChannelRatePlanCode = m.ChannelRatePlanCode,
                IsActive = m.IsActive
            })
            .ToListAsync();
    }

    public async Task<ChannelMappingDto> UpsertMappingAsync(UpsertChannelMappingCommand command)
    {
        var mapping = await _context.ChannelMappings
            .FirstOrDefaultAsync(m => m.ChannelId == command.ChannelId && m.RoomTypeId == command.RoomTypeId);

        if (mapping == null)
        {
            mapping = new ChannelMapping
            {
                Id = Guid.NewGuid(),
                ChannelId = command.ChannelId,
                RoomTypeId = command.RoomTypeId,
                ChannelRoomCode = command.ChannelRoomCode,
                ChannelRatePlanCode = command.ChannelRatePlanCode,
                IsActive = command.IsActive,
                CreatedAt = DateTime.UtcNow
            };
            _context.ChannelMappings.Add(mapping);
        }
        else
        {
            mapping.ChannelRoomCode = command.ChannelRoomCode;
            mapping.ChannelRatePlanCode = command.ChannelRatePlanCode;
            mapping.IsActive = command.IsActive;
            mapping.UpdatedAt = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync();

        var roomType = await _context.RoomTypes.FindAsync(command.RoomTypeId);
        var channel = await _context.Channels.FindAsync(command.ChannelId);

        return new ChannelMappingDto
        {
            Id = mapping.Id,
            ChannelId = mapping.ChannelId,
            ChannelName = channel?.Name ?? string.Empty,
            RoomTypeId = mapping.RoomTypeId,
            RoomTypeName = roomType?.Name ?? string.Empty,
            ChannelRoomCode = mapping.ChannelRoomCode,
            ChannelRatePlanCode = mapping.ChannelRatePlanCode,
            IsActive = mapping.IsActive
        };
    }

    public async Task DeleteMappingAsync(Guid mappingId)
    {
        var mapping = await _context.ChannelMappings.FindAsync(mappingId);
        if (mapping != null)
        {
            _context.ChannelMappings.Remove(mapping);
            await _context.SaveChangesAsync();
        }
    }

    public async Task<ChannelCredentialsDto> GetCredentialsAsync(Guid channelId)
    {
        var channel = await _context.Channels.FindAsync(channelId);
        return new ChannelCredentialsDto
        {
            ChannelId = channelId,
            CredentialsJson = channel?.CredentialsJson ?? string.Empty
        };
    }

    public async Task UpdateCredentialsAsync(ChannelCredentialsDto command)
    {
        var channel = await _context.Channels.FindAsync(command.ChannelId);
        if (channel == null)
        {
            throw new KeyNotFoundException("Canal no encontrado.");
        }

        channel.CredentialsJson = command.CredentialsJson;
        channel.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
    }
}