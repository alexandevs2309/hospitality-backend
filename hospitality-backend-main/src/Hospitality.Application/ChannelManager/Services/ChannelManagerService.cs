using Hospitality.Application.ChannelManager.Adapters;
using Hospitality.Application.ChannelManager.Commands;
using Hospitality.Application.Automation.Commands;
using Hospitality.Application.Common;
using Hospitality.Application.Common.Helpers;
using Hospitality.Application.Common.Interfaces;
using Hospitality.Domain.Entities;
using Hospitality.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Hospitality.Application.ChannelManager.Services;

public class ChannelManagerService : IChannelManagerService
{
    private readonly IApplicationDbContext _context;
    private readonly ChannelAdapterFactory _adapterFactory;
    private readonly IAutomationService _automationService;

    public ChannelManagerService(IApplicationDbContext context, ChannelAdapterFactory adapterFactory, IAutomationService automationService)
    {
        _context = context;
        _adapterFactory = adapterFactory;
        _automationService = automationService;
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

    public async Task<Guid?> GetChannelHotelIdAsync(Guid channelId)
    {
        return await _context.Channels
            .Where(c => c.Id == channelId)
            .Select(c => (Guid?)c.HotelId)
            .FirstOrDefaultAsync();
    }

    public async Task<Guid?> GetMappingChannelHotelIdAsync(Guid mappingId)
    {
        return await _context.ChannelMappings
            .Where(m => m.Id == mappingId)
            .Select(m => (Guid?)m.Channel.HotelId)
            .FirstOrDefaultAsync();
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
            var date = DateTime.SpecifyKind(from.Date.AddDays(i), DateTimeKind.Utc);
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
        var created = await CreateMappingsFromFetchAsync(channelId, maps);
        return new PushResultDto { Success = true, Message = $"Mapeos creados: {created}", ItemsPushed = created };
    }

    public async Task<int> CreateMappingsFromFetchAsync(Guid channelId, Dictionary<string, ChannelRoomTypeMap> fetchedMaps)
    {
        var channel = await _context.Channels
            .FirstOrDefaultAsync(c => c.Id == channelId);
        if (channel == null)
        {
            throw new KeyNotFoundException("Canal no encontrado.");
        }

        // Load all room types for client-side matching (small dataset)
        var roomTypes = await _context.RoomTypes
            .Where(rt => rt.HotelId == channel.HotelId)
            .ToListAsync();

        var created = 0;
        foreach (var (channelCode, map) in fetchedMaps)
        {
            var candidates = channelCode
                .Split('_', StringSplitOptions.RemoveEmptyEntries)
                .Select(token => token switch
                {
                    "DBL" => "Doble",
                    "SGL" => "Sencilla",
                    "STE" => "Suite",
                    _ => token
                })
                .Where(c => !string.IsNullOrWhiteSpace(c))
                .Distinct()
                .ToList();

            var roomType = roomTypes.FirstOrDefault(rt =>
                candidates.Any(c => rt.Name.Contains(c, StringComparison.OrdinalIgnoreCase)) ||
                candidates.Any(c => c.Contains(rt.Name, StringComparison.OrdinalIgnoreCase)));

            if (roomType == null)
            {
                continue;
            }

            var exists = await _context.ChannelMappings
                .AnyAsync(m => m.ChannelId == channelId && m.RoomTypeId == roomType.Id);
            if (exists)
            {
                continue;
            }

            _context.ChannelMappings.Add(new ChannelMapping
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
            created++;
        }

        await _context.SaveChangesAsync();
        return created;
    }

    private async Task<int> CalculateAvailabilityAsync(Guid roomTypeId, DateTime date)
    {
        return await AvailabilityHelper.CalculateAvailableRoomsAsync(_context, roomTypeId, date);
    }

    public async Task<List<BookingImportDto>> ImportBookingsAsync(Guid channelId, DateTime from, DateTime to)
    {
        var channel = await _context.Channels
            .Include(c => c.Mappings)
            .ThenInclude(m => m.RoomType)
            .ThenInclude(rt => rt.RatePlan)
            .FirstOrDefaultAsync(c => c.Id == channelId);
        if (channel == null)
        {
            throw new KeyNotFoundException("Canal no encontrado.");
        }
        if (string.IsNullOrEmpty(channel.CredentialsJson))
        {
            return new List<BookingImportDto>
            {
                new() { Status = "Error", Message = "Canal sin credenciales configuradas." }
            };
        }

        var adapter = _adapterFactory.GetAdapter(channel.ChannelType, channel.Name);
        if (adapter == null)
        {
            return new List<BookingImportDto>
            {
                new() { Status = "Error", Message = $"No hay adaptador para el canal {channel.Name}." }
            };
        }

        var activeMappings = channel.Mappings.Where(m => m.IsActive).ToList();
        if (!activeMappings.Any())
        {
            return new List<BookingImportDto>
            {
                new() { Status = "Error", Message = "No hay mapeos activos para el canal." }
            };
        }

        var pulled = await adapter.PullBookingsAsync(channel.CredentialsJson, from, to);
        if (!pulled.Any())
        {
            return new List<BookingImportDto>();
        }

        var results = new List<BookingImportDto>();
        foreach (var item in pulled)
        {
            results.Add(await ImportSingleBookingAsync(channel, activeMappings, item));
        }

        return results;
    }

    private async Task<BookingImportDto> ImportSingleBookingAsync(
        Channel channel,
        List<ChannelMapping> activeMappings,
        BookingPullResult item)
    {
        var baseResult = new BookingImportDto
        {
            ExternalBookingId = item.ExternalBookingId,
            ChannelRoomCode = item.ChannelRoomCode
        };

        // Dedupe por BookingReference + Source del canal
        var alreadyImported = await _context.Reservations.AnyAsync(r =>
            r.Source == channel.Name &&
            r.BookingReference == item.ExternalBookingId);
        if (alreadyImported)
        {
            baseResult.Status = "Skipped";
            baseResult.Message = "Ya importada anteriormente.";
            return baseResult;
        }

        var mapping = activeMappings.FirstOrDefault(m => m.ChannelRoomCode == item.ChannelRoomCode);
        if (mapping == null)
        {
            baseResult.Status = "Error";
            baseResult.Message = $"Sin mapeo activo para el código OTA «{item.ChannelRoomCode}».";
            return baseResult;
        }

        var checkIn = DateTime.SpecifyKind(item.CheckIn.Date, DateTimeKind.Utc);
        var checkOut = DateTime.SpecifyKind(item.CheckOut.Date, DateTimeKind.Utc);
        var nights = (checkOut - checkIn).Days;
        if (nights < 1)
        {
            baseResult.Status = "Error";
            baseResult.Message = "Fechas inválidas recibidas desde el canal.";
            return baseResult;
        }

        var roomType = mapping.RoomType;
        var availableRoom = await FindAvailableRoomAsync(roomType.Id, checkIn, checkOut);
        if (availableRoom == null)
        {
            baseResult.Status = "Error";
            baseResult.Message = "Sin disponibilidad para las fechas del canal.";
            return baseResult;
        }

        var guest = await ResolveOrCreateGuestAsync(item);

        var multiplier = roomType.RatePlan?.Multiplier ?? 1m;
        var roomRate = item.TotalPrice > 0
            ? Math.Round(item.TotalPrice / nights, 2)
            : Math.Round(roomType.BasePrice * multiplier, 2);

        var reservation = new Reservation
        {
            ReservationNumber = GenerateReservationNumber(),
            CheckInDate = checkIn,
            CheckOutDate = checkOut,
            NumberOfGuests = Math.Max(1, item.Adults + item.Children),
            HasExtraBed = false,
            SpecialRequests = string.Empty,
            Status = ReservationStatus.Confirmed,
            Source = channel.Name,
            BookingReference = item.ExternalBookingId,
            RoomRate = roomRate,
            ExtraBedRate = 0,
            TaxRate = 0,
            HotelId = channel.HotelId,
            RoomId = availableRoom.Id,
            GuestId = guest.Id,
            RatePlanId = roomType.RatePlan?.Id,
            PenaltyAmount = 0,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        reservation.CalculateTotal();

        _context.Reservations.Add(reservation);
        _context.DomainEvents.Add(await DomainEventLog.NewAsync(_context, "reservation", reservation.Id, "ReservationCreated",
            new
            {
                reservation.HotelId,
                reservation.RoomId,
                reservation.GuestId,
                reservation.CheckInDate,
                reservation.CheckOutDate,
                reservation.NumberOfNights,
                reservation.RoomRate,
                reservation.ExtraBedRate,
                reservation.TaxRate,
                reservation.TotalAmount,
                reservation.Source,
                reservation.BookingReference
            },
            actorUserId: null));

        await _context.SaveChangesAsync();

        await _automationService.FireAutomationAsync(channel.HotelId, "ReservationCreated", reservation.Id);

        baseResult.Status = "Imported";
        baseResult.Message = "Reserva creada en el PMS.";
        baseResult.ReservationNumber = reservation.ReservationNumber;
        baseResult.TotalAmount = reservation.TotalAmount;
        baseResult.Nights = reservation.NumberOfNights;
        return baseResult;
    }

    private async Task<Room?> FindAvailableRoomAsync(Guid roomTypeId, DateTime checkIn, DateTime checkOut)
    {
        var rooms = await _context.Rooms
            .Where(r => r.RoomTypeId == roomTypeId && !r.IsDeleted)
            .Select(r => r.Id)
            .ToListAsync();

        foreach (var roomId in rooms)
        {
            var occupied = await _context.Reservations.AnyAsync(r =>
                r.RoomId == roomId &&
                r.CheckInDate < checkOut &&
                r.CheckOutDate > checkIn &&
                (r.Status == ReservationStatus.Confirmed || r.Status == ReservationStatus.CheckedIn));
            if (!occupied)
            {
                return await _context.Rooms
                    .Include(r => r.RoomType)
                    .FirstAsync(r => r.Id == roomId);
            }
        }
        return null;
    }

    private async Task<Guest> ResolveOrCreateGuestAsync(BookingPullResult item)
    {
        var email = item.GuestEmail?.Trim() ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(email))
        {
            var byEmail = await _context.Guests
                .FirstOrDefaultAsync(g => !g.IsDeleted && g.Email.ToLower() == email.ToLower());
            if (byEmail != null)
            {
                byEmail.UpdatedAt = DateTime.UtcNow;
                return byEmail;
            }
        }

        var fullName = (item.GuestName ?? string.Empty).Trim();
        string firstName = fullName, lastName = string.Empty;
        var sep = fullName.IndexOf(' ');
        if (sep > 0)
        {
            firstName = fullName[..sep];
            lastName = fullName[(sep + 1)..];
        }

        var guest = new Guest
        {
            FirstName = firstName,
            LastName = lastName,
            Email = email,
            PhoneNumber = item.GuestPhone?.Trim() ?? string.Empty,
            DocumentType = "Pasaporte",
            DocumentNumber = string.Empty,
            Nationality = string.Empty,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.Guests.Add(guest);
        await _context.SaveChangesAsync();
        return guest;
    }

    private static string GenerateReservationNumber()
    {
        var ts = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
        var rand = Random.Shared.Next(100, 999);
        return $"RSV-{ts[^6..]}{rand}";
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