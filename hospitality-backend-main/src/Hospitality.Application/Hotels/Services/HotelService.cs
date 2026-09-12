using Hospitality.Application.Common.DTOs;
using Hospitality.Application.Common.Interfaces;
using Hospitality.Application.Hotels.Commands;
using Hospitality.Domain.Entities;
using Hospitality.Domain.Enums;
using Hospitality.Domain.Exceptions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace Hospitality.Application.Hotels.Services;

public class HotelService : IHotelService
{
    private readonly IApplicationDbContext _context;
    private readonly ILogger<HotelService> _logger;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ICurrentUserService _currentUserService;

    public HotelService(
        IApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        ICurrentUserService currentUserService,
        ILogger<HotelService> logger)
    {
        _context = context;
        _userManager = userManager;
        _currentUserService = currentUserService;
        _logger = logger;
    }

    public async Task<PaginatedResult<HotelDto>> GetHotelsAsync(PaginatedQuery query, Guid? hotelScope = null)
    {
        var dbQuery = ApplyFilters(_context.ActiveHotels()
            .Include(h => h.Rooms.Where(r => !r.IsDeleted)), query);

        if (hotelScope.HasValue)
        {
            dbQuery = dbQuery.Where(h => h.Id == hotelScope.Value);
        }

        var totalCount = await dbQuery.CountAsync();
        var hotels = await dbQuery
            .Skip((query.PageNumber - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync();

        return new PaginatedResult<HotelDto>(
            hotels.Select(MapToDto).ToList(),
            totalCount,
            query.PageNumber,
            query.PageSize);
    }

    public async Task<HotelDto?> GetHotelByIdAsync(Guid id)
    {
        var hotel = await QueryWithRooms()
            .FirstOrDefaultAsync(h => h.Id == id);
        return hotel == null ? null : MapToDto(hotel);
    }

    public async Task<HotelDto> CreateHotelAsync(CreateHotelCommand command)
    {
        var ownerUserId = _currentUserService.UserId;
        var ownerAlreadyHasHotel = _currentUserService.HotelId.HasValue;

        // Un hotel se crea vinculado al usuario que lo crea (salvo Admin, que no se auto-vincula).
        var bindToOwner = !string.IsNullOrEmpty(ownerUserId) && !ownerAlreadyHasHotel;

        // Pre-check (caso habitual): un propietario solo puede tener un hotel activo.
        if (bindToOwner && await _context.Users.AnyAsync(u => u.Id == ownerUserId && u.HotelId.HasValue))
        {
            throw new ConflictException("El usuario ya tiene un hotel asignado.");
        }

        // Dos escrituras acopladas (hotel + usuario propietario) dentro de una transacción.
        await using var transaction = await _context.Database.BeginTransactionAsync();

        // Fase 0: cada hotel nuevo crea su Organización (tenant) con Id = hotel.Id (1:1),
        // de modo que la jerarquía Organización→Propiedad queda desde el alta.
        var organization = new Organization
        {
            Id = Guid.NewGuid(),
            Name = command.Name,
            BusinessName = command.BusinessName,
            Email = command.Email,
            PhoneNumber = command.PhoneNumber,
            Plan = "small",
            SelectedModules = ToJsonModules(command.SelectedModules),
            DefaultCurrency = string.IsNullOrWhiteSpace(command.Currency) ? "USD" : command.Currency,
            TimeZone = string.IsNullOrWhiteSpace(command.TimeZone) ? "UTC" : command.TimeZone,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var hotel = new Hotel
        {
            Id = organization.Id,
            OrganizationId = organization.Id,
            Name = command.Name,
            Description = command.Description,
            Address = command.Address,
            PhoneNumber = command.PhoneNumber,
            Email = command.Email,
            Website = command.Website,
            StarRating = command.StarRating,
            TotalRooms = command.TotalRooms,
            IsActive = true,
            TimeZone = command.TimeZone,
            City = command.City,
            Country = command.Country,
            BusinessName = command.BusinessName,
            YearOpened = command.YearOpened,
            PostalCode = command.PostalCode,
            Currency = command.Currency,
            TaxRate = command.TaxRate,
            CheckInTime = command.CheckInTime,
            CheckOutTime = command.CheckOutTime,
            HotelLanguages = command.HotelLanguages,
            SelectedModules = command.SelectedModules,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        if (bindToOwner)
        {
            organization.Members.Add(new OrganizationMember
            {
                OrganizationId = organization.Id,
                UserId = ownerUserId!,
                OrganizationRole = "Owner",
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });

            _context.PropertyAssignments.Add(new PropertyAssignment
            {
                PropertyId = hotel.Id,
                UserId = ownerUserId!,
                PropertyRole = "Owner",
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
        }

        _context.Organizations.Add(organization);
        _context.Hotels.Add(hotel);
        await _context.SaveChangesAsync();

        foreach (var roomType in DefaultRoomTypes(hotel.Id))
        {
            _context.RoomTypes.Add(roomType);
        }
        await _context.SaveChangesAsync();

        foreach (var channel in DefaultChannels(hotel.Id))
        {
            _context.Channels.Add(channel);
        }
        await _context.SaveChangesAsync();

        try
        {
            if (bindToOwner)
            {
                var owner = await _userManager.FindByIdAsync(ownerUserId!);
                if (owner is not null && !owner.HotelId.HasValue)
                {
                    owner.HotelId = hotel.Id;
                    owner.UpdatedAt = DateTime.UtcNow;
                    var updateResult = await _userManager.UpdateAsync(owner);
                    if (!updateResult.Succeeded)
                    {
                        throw new InvalidOperationException("No se pudo vincular el hotel al usuario.");
                    }
                }
            }

            await transaction.CommitAsync();
        }
        catch (DbUpdateException) when (bindToOwner)
        {
            // Dos creaciones concurrentes del mismo propietario: el índice único
            // de AspNetUsers.HotelId rechaza la segunda. Se revierte y se informa.
            await transaction.RollbackAsync();
            throw new ConflictException("El usuario ya tiene un hotel asignado (creación simultánea detectada).");
        }

        _logger.LogInformation("Hotel creado con ID {HotelId} {BoundToOwner}", hotel.Id, bindToOwner);
        return MapToDto(hotel);
    }

    public async Task<HotelDto> UpdateHotelAsync(UpdateHotelCommand command)
    {
        var hotel = await QueryWithRooms()
            .FirstOrDefaultAsync(h => h.Id == command.Id)
            ?? throw new KeyNotFoundException($"Hotel con ID {command.Id} no encontrado.");

        hotel.Name = command.Name;
        hotel.Description = command.Description;
        hotel.Address = command.Address;
        hotel.PhoneNumber = command.PhoneNumber;
        hotel.Email = command.Email;
        hotel.Website = command.Website;
        hotel.StarRating = command.StarRating;
        hotel.TotalRooms = command.TotalRooms;
        hotel.IsActive = command.IsActive;
        hotel.TimeZone = command.TimeZone;
        hotel.City = command.City;
        hotel.Country = command.Country;
        hotel.BusinessName = command.BusinessName;
        hotel.YearOpened = command.YearOpened;
        hotel.PostalCode = command.PostalCode;
        hotel.Currency = command.Currency;
        hotel.TaxRate = command.TaxRate;
        hotel.CheckInTime = command.CheckInTime;
        hotel.CheckOutTime = command.CheckOutTime;
        hotel.HotelLanguages = command.HotelLanguages;
        hotel.SelectedModules = command.SelectedModules;
        hotel.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        _logger.LogInformation("Hotel actualizado con ID {HotelId}", hotel.Id);
        return MapToDto(hotel);
    }

    public async Task<HotelDto> UpdateHotelStatusAsync(Guid id, UpdateHotelStatusCommand command)
    {
        var hotel = await _context.Hotels
            .FirstOrDefaultAsync(h => h.Id == id && !h.IsDeleted)
            ?? throw new KeyNotFoundException($"Hotel con ID {id} no encontrado.");

        hotel.IsActive = command.IsActive;
        hotel.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        _logger.LogInformation("Estado del hotel {HotelId} cambiado a {IsActive}", hotel.Id, command.IsActive);
        return MapToDto(hotel);
    }

    public async Task<bool> DeleteHotelAsync(Guid id)
    {
        var hotel = await _context.Hotels
            .FirstOrDefaultAsync(h => h.Id == id && !h.IsDeleted)
            ?? throw new KeyNotFoundException($"Hotel con ID {id} no encontrado.");

        hotel.IsDeleted = true;
        hotel.DeletedAt = DateTime.UtcNow;
        hotel.IsActive = false;
        hotel.UpdatedAt = DateTime.UtcNow;

        // Libera el vínculo del propietario (1:1 usuario-hoteles) para permitir
        // re-crear un hotel tras el soft-delete sin violar el índice único.
        var owner = await _context.Users.FirstOrDefaultAsync(u => u.HotelId == id);
        if (owner is not null)
        {
            owner.HotelId = null;
            owner.UpdatedAt = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync();
        _logger.LogInformation("Hotel eliminado (soft delete) con ID {HotelId}", id);
        return true;
    }

    public async Task<HotelStatsDto> GetHotelStatsAsync(Guid id)
    {
        if (!await _context.ActiveHotels().AnyAsync(h => h.Id == id))
        {
            throw new KeyNotFoundException($"Hotel con ID {id} no encontrado.");
        }

        var occupiedRooms = await _context.ActiveRooms()
            .CountAsync(r => r.HotelId == id && r.Status == RoomStatus.Occupied);

        var totalRoomsSetting = await _context.ActiveHotels()
            .Where(h => h.Id == id)
            .Select(h => h.TotalRooms)
            .FirstOrDefaultAsync();
        var roomCount = await _context.ActiveRooms().CountAsync(r => r.HotelId == id);
        var totalRooms = totalRoomsSetting > 0 ? totalRoomsSetting : roomCount;

        var occupancyRate = totalRooms > 0 ? Math.Round((decimal)occupiedRooms / totalRooms * 100, 2) : 0m;

        var closed = await _context.Reservations
            .Where(r => r.HotelId == id &&
                        r.Status == ReservationStatus.CheckedOut &&
                        r.CheckedOutAt.HasValue)
            .Select(r => new { r.RoomRate, r.AmountPaid, r.NumberOfGuests })
            .ToListAsync();

        var averageDailyRate = closed.Count > 0 ? Math.Round(closed.Average(r => r.RoomRate), 2) : 0m;
        var totalRevenue = closed.Sum(r => r.AmountPaid);
        var revenuePerAvailableRoom = totalRooms > 0 ? Math.Round(totalRevenue / totalRooms, 2) : 0m;
        var totalGuests = await _context.Reservations
            .Where(r => r.HotelId == id &&
                        r.Status != ReservationStatus.Cancelled &&
                        r.Status != ReservationStatus.NoShow)
            .SumAsync(r => (int?)r.NumberOfGuests) ?? 0;

        return new HotelStatsDto
        {
            HotelId = id,
            AverageDailyRate = averageDailyRate,
            RevenuePerAvailableRoom = revenuePerAvailableRoom,
            OccupancyRate = occupancyRate,
            TotalGuests = totalGuests
        };
    }

    public async Task<List<RoomTypeDto>> GetHotelRoomTypesAsync(Guid id)
    {
if (!await _context.ActiveHotels().AnyAsync(h => h.Id == id))
        {
            throw new NotFoundException(nameof(Hotel), id);
        }

        return await _context.RoomTypes
            .Where(rt => rt.HotelId == id)
            .OrderBy(rt => rt.Name)
            .Select(rt => new RoomTypeDto
            {
                Id = rt.Id,
                Name = rt.Name,
                Description = rt.Description,
                BasePrice = rt.BasePrice,
                MaxOccupancy = rt.Capacity
            })
            .ToListAsync();
    }

    public async Task<RoomTypeDto> CreateHotelRoomTypeAsync(UpsertRoomTypeCommand command)
    {
        if (string.IsNullOrWhiteSpace(command.Name))
        {
            throw new ValidationException("El nombre del tipo de habitación es obligatorio.");
        }

        if (!await _context.ActiveHotels().AnyAsync(h => h.Id == command.HotelId))
        {
            throw new NotFoundException(nameof(Hotel), command.HotelId);
        }

        var exists = await _context.RoomTypes.AnyAsync(rt =>
            rt.HotelId == command.HotelId && rt.Name.Trim().ToLower() == command.Name.Trim().ToLower());
        if (exists)
        {
            throw new ValidationException("Ya existe un tipo de habitación con ese nombre.");
        }

        var now = DateTime.UtcNow;
        var roomType = new RoomType
        {
            Id = Guid.NewGuid(),
            HotelId = command.HotelId,
            Name = command.Name.Trim(),
            Description = command.Description,
            BasePrice = command.BasePrice,
            Capacity = Math.Max(1, command.Capacity),
            ExtraBedCapacity = command.ExtraBedCapacity ?? 0,
            ExtraBedPrice = command.ExtraBedPrice ?? 0,
            Amenities = "Wifi,TV",
            CreatedAt = now,
            UpdatedAt = now
        };

        _context.RoomTypes.Add(roomType);
        await _context.SaveChangesAsync();
        return ToRoomTypeDto(roomType);
    }

    public async Task<RoomTypeDto> UpdateHotelRoomTypeAsync(UpsertRoomTypeCommand command)
    {
        var roomType = await _context.RoomTypes.FirstOrDefaultAsync(rt => rt.Id == command.RoomTypeId)
            ?? throw new NotFoundException(nameof(RoomType), command.RoomTypeId ?? Guid.Empty);

        var dup = await _context.RoomTypes.AnyAsync(rt =>
            rt.HotelId == command.HotelId && rt.Id != roomType.Id && rt.Name.Trim().ToLower() == command.Name.Trim().ToLower());
        if (dup)
        {
            throw new ValidationException("Ya existe un tipo de habitación con ese nombre.");
        }

        roomType.Name = command.Name.Trim();
        roomType.Description = command.Description;
        roomType.BasePrice = command.BasePrice;
        roomType.Capacity = Math.Max(1, command.Capacity);
        roomType.ExtraBedCapacity = command.ExtraBedCapacity ?? 0;
        roomType.ExtraBedPrice = command.ExtraBedPrice ?? 0;
        roomType.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        return ToRoomTypeDto(roomType);
    }

    public async Task DeleteHotelRoomTypeAsync(Guid hotelId, Guid roomTypeId)
    {
        var roomType = await _context.RoomTypes.FirstOrDefaultAsync(rt => rt.HotelId == hotelId && rt.Id == roomTypeId)
            ?? throw new NotFoundException(nameof(RoomType), roomTypeId);

        var inUse = await _context.Rooms.AnyAsync(r => r.RoomTypeId == roomTypeId && !r.IsDeleted);
        if (inUse)
        {
            throw new ValidationException("No se puede eliminar: el tipo está en uso por una o más habitaciones.");
        }

        var hasRates = await _context.RoomRates.AnyAsync(rr => rr.RoomTypeId == roomTypeId);
        if (hasRates)
        {
            throw new ValidationException("No se puede eliminar: el tipo tiene tarifas de calendario asociadas.");
        }

        _context.RoomTypes.Remove(roomType);
        await _context.SaveChangesAsync();
    }

    private static RoomTypeDto ToRoomTypeDto(RoomType rt)
    {
        return new RoomTypeDto
        {
            Id = rt.Id,
            Name = rt.Name,
            Description = rt.Description,
            BasePrice = rt.BasePrice,
            MaxOccupancy = rt.Capacity
        };
    }

    public async Task<List<HotelNameDto>> GetHotelNamesAsync(Guid? hotelScope = null)
    {
        var query = _context.ActiveHotels()
            .Where(h => h.IsActive);

        if (hotelScope.HasValue)
        {
            query = query.Where(h => h.Id == hotelScope.Value);
        }

        return await query
            .OrderBy(h => h.Name)
            .Select(h => new HotelNameDto
            {
                Id = h.Id,
                Name = h.Name
            })
            .ToListAsync();
    }

    public async Task<List<HotelDto>> SearchHotelsAsync(string? name, string? city, int? minStars, bool? isActive, Guid? hotelScope = null)
    {
        IQueryable<Hotel> dbQuery = _context.ActiveHotels()
            .Include(h => h.Rooms.Where(r => !r.IsDeleted));

        if (hotelScope.HasValue)
        {
            dbQuery = dbQuery.Where(h => h.Id == hotelScope.Value);
        }

        if (!string.IsNullOrWhiteSpace(name))
        {
            dbQuery = dbQuery.Where(h => h.Name.Contains(name.Trim()));
        }

        if (!string.IsNullOrWhiteSpace(city))
        {
            dbQuery = dbQuery.Where(h => h.City.Contains(city.Trim()));
        }

        if (minStars.HasValue)
        {
            dbQuery = dbQuery.Where(h => h.StarRating >= minStars.Value);
        }

        if (isActive.HasValue)
        {
            dbQuery = dbQuery.Where(h => h.IsActive == isActive.Value);
        }

        var hotels = await dbQuery.OrderBy(h => h.Name).ToListAsync();
        return hotels.Select(MapToDto).ToList();
    }

    private IQueryable<Hotel> QueryWithRooms()
    {
        return _context.ActiveHotels()
            .Include(h => h.Rooms.Where(r => !r.IsDeleted));
    }

    private static IQueryable<Hotel> ApplyFilters(IQueryable<Hotel> query, PaginatedQuery paginatedQuery)
    {
        if (!string.IsNullOrWhiteSpace(paginatedQuery.Search))
        {
            var search = paginatedQuery.Search.Trim().ToLower();
            query = query.Where(h =>
                h.Name.ToLower().Contains(search) ||
                h.City.ToLower().Contains(search) ||
                h.Country.ToLower().Contains(search));
        }

        if (!string.IsNullOrWhiteSpace(paginatedQuery.SortBy))
        {
            var ascending = paginatedQuery.SortDirection != "desc";
            query = paginatedQuery.SortBy.ToLowerInvariant() switch
            {
                "name" => ascending ? query.OrderBy(h => h.Name) : query.OrderByDescending(h => h.Name),
                "city" => ascending ? query.OrderBy(h => h.City) : query.OrderByDescending(h => h.City),
                "starrating" => ascending ? query.OrderBy(h => h.StarRating) : query.OrderByDescending(h => h.StarRating),
                "createdat" => ascending ? query.OrderBy(h => h.CreatedAt) : query.OrderByDescending(h => h.CreatedAt),
                _ => query.OrderBy(h => h.Name)
            };
        }
        else
        {
            query = query.OrderBy(h => h.Name);
        }

        return query;
    }

    private static HotelDto MapToDto(Hotel hotel)
    {
        return new HotelDto
        {
            Id = hotel.Id,
            Name = hotel.Name,
            Description = hotel.Description,
            Address = hotel.Address,
            PhoneNumber = hotel.PhoneNumber,
            Email = hotel.Email,
            Website = hotel.Website,
            StarRating = hotel.StarRating,
            TotalRooms = hotel.TotalRooms,
            AvailableRooms = hotel.Rooms.Count(r => r.IsAvailable),
            IsActive = hotel.IsActive,
            TimeZone = hotel.TimeZone,
            City = hotel.City,
            Country = hotel.Country,
            BusinessName = hotel.BusinessName,
            YearOpened = hotel.YearOpened,
            PostalCode = hotel.PostalCode,
            Currency = hotel.Currency,
            TaxRate = hotel.TaxRate,
            CheckInTime = hotel.CheckInTime,
            CheckOutTime = hotel.CheckOutTime,
            HotelLanguages = hotel.HotelLanguages,
            SelectedModules = hotel.SelectedModules,
            CreatedAt = hotel.CreatedAt,
            UpdatedAt = hotel.UpdatedAt
        };
    }

    // Convierte el CSV legado de módulos ("bookings,housekeeping") a un array JSON
    // válido para la columna jsonb de Organization.SelectedModules.
    private static string? ToJsonModules(string? selectedModules)
    {
        if (string.IsNullOrWhiteSpace(selectedModules)) return null;
        var modules = selectedModules
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return modules.Length == 0 ? null : JsonSerializer.Serialize(modules);
    }

    private static List<RoomType> DefaultRoomTypes(Guid hotelId)
    {        var now = DateTime.UtcNow;
        return new List<RoomType>
        {
            new() { HotelId = hotelId, Name = "Sencilla", Description = "Habitación individual con cama individual.", BasePrice = 45, Capacity = 1, Amenities = "Wifi,TV,Cofre" },
            new() { HotelId = hotelId, Name = "Doble", Description = "Habitación con cama doble o dos individuales.", BasePrice = 75, Capacity = 2, ExtraBedCapacity = 1, ExtraBedPrice = 15, Amenities = "Wifi,TV,Aire acondicionado" },
            new() { HotelId = hotelId, Name = "Suite", Description = "Habitación amplia con sala de estar.", BasePrice = 140, Capacity = 3, ExtraBedCapacity = 1, ExtraBedPrice = 20, Amenities = "Wifi,TV,MinibarJacuzzi" },
            new() { HotelId = hotelId, Name = "Familiar", Description = "Amplia habitación ideal para familias.", BasePrice = 120, Capacity = 4, ExtraBedCapacity = 2, ExtraBedPrice = 15, Amenities = "Wifi,TV,Cocina" }
        }.Select(rt => { rt.CreatedAt = now; rt.UpdatedAt = now; return rt; }).ToList();
    }

    private static List<Channel> DefaultChannels(Guid hotelId)
    {
        var now = DateTime.UtcNow;
        return new List<Channel>
        {
            new() { HotelId = hotelId, Name = "Walk-in", ChannelType = "WalkIn", CommissionRate = 0, IsActive = true },
            new() { HotelId = hotelId, Name = "Teléfono", ChannelType = "Phone", CommissionRate = 0, IsActive = true },
            new() { HotelId = hotelId, Name = "Sitio web", ChannelType = "Site", CommissionRate = 0, IsActive = true }
        }.Select(c => { c.Id = Guid.NewGuid(); c.CreatedAt = now; c.UpdatedAt = now; return c; }).ToList();
    }
}