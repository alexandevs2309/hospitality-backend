using Hospitality.Domain.Entities;
using Enums = Hospitality.Domain.Enums;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Hospitality.Infrastructure.Persistence;

public class DatabaseInitializer
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<IdentityRole> _roleManager;
    private readonly IConfiguration _configuration;
    private readonly ILogger<DatabaseInitializer> _logger;

    public DatabaseInitializer(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole> roleManager,
        IConfiguration configuration,
        ILogger<DatabaseInitializer> logger)
    {
        _context = context;
        _userManager = userManager;
        _roleManager = roleManager;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task InitializeAsync()
    {
        try
        {
            // Aplica migraciones pendientes (crea/esquema evoluciona la base de datos).
            await _context.Database.MigrateAsync();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "No se pudo conectar a la base de datos. La aplicación continuará iniciando.");
            return;
        }

        await EnsureRolesAsync();
        await EnsureAdminUserAsync();
        await EnsureDemoDataAsync();
    }

    private async Task EnsureRolesAsync()
    {
        var roles = new[] { "Admin", "Manager", "Receptionist", "Housekeeping", "Maintenance", "User" };
        foreach (var role in roles)
        {
            if (!await _roleManager.RoleExistsAsync(role))
            {
                await _roleManager.CreateAsync(new IdentityRole(role));
            }
        }
    }

    private async Task EnsureAdminUserAsync()
    {
        var section = _configuration.GetSection("Seed");
        var adminEmail = section["AdminEmail"] ?? "admin@auronsuite.com";
        var adminPassword = section["AdminPassword"] ?? "Admin#2026";

        if (await _userManager.FindByEmailAsync(adminEmail) is not null)
        {
            return;
        }

        var admin = new ApplicationUser
        {
            UserName = adminEmail,
            Email = adminEmail,
            EmailConfirmed = true,
            FirstName = "Admin",
            LastName = "Hospitality",
            Department = "Management",
            Position = "Administrador",
            Language = "es",
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var result = await _userManager.CreateAsync(admin, adminPassword);
        if (!result.Succeeded)
        {
            _logger.LogError("No se pudo crear el usuario administrador: {Errors}",
                string.Join(", ", result.Errors.Select(e => e.Description)));
            return;
        }

        await _userManager.AddToRoleAsync(admin, "Admin");
        _logger.LogInformation("Usuario administrador '{Email}' creado.", adminEmail);
    }

    private async Task EnsureDemoDataAsync()
    {
        var seedEnabled = _configuration.GetValue("Seed:DemoData", true);
        if (!seedEnabled)
        {
            return;
        }

        if (await _context.Hotels.AnyAsync())
        {
            return;
        }

        await using var transaction = await _context.Database.BeginTransactionAsync();

        // Fase 0: la propiedad de demo también nace bajo una Organización (Id = hotel.Id).
        var organization = new Organization
        {
            Id = Guid.NewGuid(),
            Name = "Hotel Aurora",
            Email = "contacto@hotelaurora.com",
            PhoneNumber = "+34 910 000 000",
            Plan = "small",
            DefaultCurrency = "EUR",
            TimeZone = "Europe/Madrid",
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var hotel = new Hotel
        {
            Id = organization.Id,
            OrganizationId = organization.Id,
            Name = "Hotel Aurora",
            Description = "Hotel boutique de demostración.",
            Address = "Av. Reforma 123",
            PhoneNumber = "+34 910 000 000",
            Email = "contacto@hotelaurora.com",
            Website = "https://hotelaurora.example.com",
            StarRating = 4,
            TotalRooms = 12,
            IsActive = true,
            TimeZone = "Europe/Madrid",
            City = "Madrid",
            Country = "España",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var roomTypes = new[]
        {
            new RoomType { Name = "Estándar", Description = "Cama doble, baño completo.", BasePrice = 95m, Capacity = 2, Amenities = "WiFi, TV, A/C", Hotel = hotel },
            new RoomType { Name = "Doble", Description = "Dos camas, ideal para parejas.", BasePrice = 120m, Capacity = 2, Amenities = "WiFi, TV, A/C, Balcón", Hotel = hotel },
            new RoomType { Name = "Suite", Description = "Sala de estar y cama king.", BasePrice = 210m, Capacity = 3, Amenities = "WiFi, TV, A/C, Minibar", Hotel = hotel },
        };

        var rooms = new List<Room>();
        for (var floor = 1; floor <= 3; floor++)
        {
            for (var i = 1; i <= 4; i++)
            {
                rooms.Add(new Room
                {
                    Hotel = hotel,
                    RoomType = floor == 3 ? roomTypes[2] : (i % 2 == 0 ? roomTypes[1] : roomTypes[0]),
                    RoomNumber = $"{floor}{i:D2}",
                    Floor = floor,
                    Features = "Vista exterior",
                    IsClean = true,
                    Status = Enums.RoomStatus.Available,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                });
            }
        }
        hotel.Rooms = rooms;

        var guests = new[]
        {
            CreateGuest("María", "Fernández", "maria.fernandez@correo.com", "+34 600 111 222", "México"),
            CreateGuest("John", "Smith", "john.smith@correo.com", "+44 7700 900 123", "Reino Unido"),
            CreateGuest("Lucía", "Gómez", "lucia.gomez@correo.com", "+34 611 222 333", "España"),
            CreateGuest("Ana", "Rodríguez", "ana.rodriguez@correo.com", "+34 622 333 444", "Argentina"),
            CreateGuest("Wei", "Zhang", "wei.zhang@correo.com", "+86 138 0013 8000", "China")
        };

        await _context.Hotels.AddAsync(hotel);
        await _context.Organizations.AddAsync(organization);
        await _context.RoomTypes.AddRangeAsync(roomTypes);
        await _context.Rooms.AddRangeAsync(rooms);
        await _context.Guests.AddRangeAsync(guests);
        await _context.SaveChangesAsync();

        await SeedReservationsAsync(hotel, rooms, guests);
        await SeedHousekeepingAsync(rooms);
        await SeedMaintenanceTicketsAsync(hotel, rooms);
        await _context.SaveChangesAsync();

        await transaction.CommitAsync();
        _logger.LogInformation("Datos de demostración sembrados (Hotel Aurora, {RoomCount} habitaciones).", rooms.Count);
    }

    private static Guest CreateGuest(string firstName, string lastName, string email, string phone, string country)
    {
        return new Guest
        {
            FirstName = firstName,
            LastName = lastName,
            Email = email,
            PhoneNumber = phone,
            Country = country,
            Nationality = country,
            DocumentType = "Pasaporte",
            DocumentNumber = Guid.NewGuid().ToString("N").Substring(0, 9).ToUpperInvariant(),
            LoyaltyPoints = 0,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }

    private async Task SeedReservationsAsync(Hotel hotel, List<Room> rooms, Guest[] guests)
    {
        var today = DateTime.UtcNow.Date;
        var rnd = new Random(42);
        var reservations = new List<Reservation>();

        for (var offset = -30; offset <= 7; offset++)
        {
            var checkIn = today.AddDays(offset);
            var nights = rnd.Next(1, 4);
            var checkOut = checkIn.AddDays(nights);

            if (checkOut <= today && rnd.Next(0, 5) == 0)
            {
                reservations.Add(CreateReservation(hotel, PickRoom(rooms, rnd), guests[rnd.Next(guests.Length)],
                    checkIn, checkOut, Enums.ReservationStatus.Cancelled));
                continue;
            }

            Enums.ReservationStatus status;
            DateTime? checkedInAt = null;
            DateTime? checkedOutAt = null;

            if (checkOut <= today)
            {
                status = Enums.ReservationStatus.CheckedOut;
                checkedInAt = checkIn.AddHours(14);
                checkedOutAt = checkOut.AddHours(11);
            }
            else if (checkIn <= today)
            {
                status = Enums.ReservationStatus.CheckedIn;
                checkedInAt = checkIn.AddHours(14);
            }
            else
            {
                status = rnd.Next(0, 2) == 0 ? Enums.ReservationStatus.Confirmed : Enums.ReservationStatus.Pending;
            }

            reservations.Add(CreateReservation(hotel, PickRoom(rooms, rnd), guests[rnd.Next(guests.Length)],
                checkIn, checkOut, status, checkedInAt, checkedOutAt));
        }

        await _context.Reservations.AddRangeAsync(reservations);

        foreach (var reservation in reservations)
        {
            if (reservation.Status == Enums.ReservationStatus.CheckedIn)
            {
                reservation.Room.Status = Enums.RoomStatus.Occupied;
                reservation.Room.IsClean = false;
            }
            else if (reservation.Status == Enums.ReservationStatus.CheckedOut &&
                     reservation.CheckedOutAt.HasValue &&
                     reservation.CheckedOutAt.Value.Date == today)
            {
                reservation.Room.Status = Enums.RoomStatus.Dirty;
                reservation.Room.IsClean = false;
            }
        }
    }

    private static Room PickRoom(List<Room> rooms, Random rnd)
    {
        Room room;
        do
        {
            room = rooms[rnd.Next(rooms.Count)];
        } while (room.IsMaintenanceRequired);

        return room;
    }

    private static Reservation CreateReservation(
        Hotel hotel,
        Room room,
        Guest guest,
        DateTime checkIn,
        DateTime checkOut,
        Enums.ReservationStatus status,
        DateTime? checkedInAt = null,
        DateTime? checkedOutAt = null)
    {
        var rate = room.RoomType.BasePrice + (guest.LoyaltyTier == "VIP" ? 0 : room.RoomType.BasePrice * 0.1m);
        var taxRate = 16m;
        var total = rate * (decimal)checkOut.Subtract(checkIn).Days;
        var tax = total * taxRate / 100m;

        return new Reservation
        {
            ReservationNumber = $"RES-{Guid.NewGuid():N}"[..13].ToUpperInvariant(),
            Hotel = hotel,
            Room = room,
            Guest = guest,
            CheckInDate = checkIn,
            CheckOutDate = checkOut,
            NumberOfGuests = 2,
            RoomRate = rate,
            TaxRate = taxRate,
            TotalAmount = total + tax,
            AmountPaid = status is Enums.ReservationStatus.CheckedOut or Enums.ReservationStatus.Confirmed ? total + tax : 0m,
            Status = status,
            CheckedInAt = checkedInAt,
            CheckedOutAt = checkedOutAt,
            Source = "Directo",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }

    private async Task SeedHousekeepingAsync(List<Room> rooms)
    {
        var now = DateTime.UtcNow;
        var today = now.Date;
        var eligible = rooms
            .Where(r => r.Status != Enums.RoomStatus.Occupied &&
                        r.Status != Enums.RoomStatus.Maintenance &&
                        r.Status != Enums.RoomStatus.OutOfOrder)
            .ToList();
        var picks = new[]
        {
            eligible[1],
            eligible[^1],
            eligible[(eligible.Count - 1) / 2],
            eligible[eligible.Count / 2 + 1]
        };
        var housekeeping = new List<HousekeepingStatus>
        {
            new HousekeepingStatus { Room = picks[0], Status = Enums.HousekeepingStatusType.InProgress, Priority = "Media", HousekeeperName = "Carmen Ruiz", StartedAt = today.AddHours(9) },
            new HousekeepingStatus { Room = picks[1], Status = Enums.HousekeepingStatusType.Inspection, Priority = "Alta",  InspectorName = "Luis Pérez", InspectedAt = today.AddHours(10) },
            new HousekeepingStatus { Room = picks[2], Status = Enums.HousekeepingStatusType.Dirty,     Priority = "Baja",  CreatedAt = today.AddHours(8) },
            new HousekeepingStatus { Room = picks[3], Status = Enums.HousekeepingStatusType.InProgress, Priority = "Media", HousekeeperName = "Carmen Ruiz", StartedAt = today.AddHours(11) },
        };

        foreach (var hk in housekeeping)
        {
            hk.CreatedAt = now;
            hk.Room.IsClean = hk.Status == Enums.HousekeepingStatusType.Clean;
            if (hk.Status == Enums.HousekeepingStatusType.Dirty)
            {
                hk.Room.Status = Enums.RoomStatus.Dirty;
            }
            else if (hk.Status == Enums.HousekeepingStatusType.InProgress)
            {
                hk.Room.Status = Enums.RoomStatus.Housekeeping;
            }
        }

        await _context.HousekeepingStatuses.AddRangeAsync(housekeeping);
    }

    private async Task SeedMaintenanceTicketsAsync(Hotel hotel, List<Room> rooms)
    {
        var now = DateTime.UtcNow;
        var tickets = new[]
        {
            new MaintenanceTicket
            {
                Room = rooms[0],
                Title = "Aire acondicionado no enfría",
                Description = "El equipo no enfría por debajo de 24°C.",
                Priority = Enums.MaintenancePriority.High,
                Status = Enums.MaintenanceTicketStatus.Open,
                AssignedTo = "Taller Pérez",
                AssignedDepartment = "Mantenimiento",
                DueDate = now.AddHours(4),
                EstimatedCost = 120m,
                CreatedAt = now.AddHours(-3),
                UpdatedAt = now.AddHours(-3)
            },
            new MaintenanceTicket
            {
                Room = rooms[6],
                Title = "Fuga en el baño",
                Description = "Pequeña fuga bajo el lavabo.",
                Priority = Enums.MaintenancePriority.Critical,
                Status = Enums.MaintenanceTicketStatus.InProgress,
                AssignedTo = "Fontanería López",
                AssignedDepartment = "Mantenimiento",
                AssignedAt = now.AddHours(-1),
                StartedAt = now.AddHours(-1),
                DueDate = now.AddHours(2),
                EstimatedCost = 80m,
                CreatedAt = now.AddHours(-5),
                UpdatedAt = now.AddHours(-1)
            },
        };

        rooms[0].Status = Enums.RoomStatus.Maintenance;
        rooms[0].IsMaintenanceRequired = true;
        rooms[0].IsClean = false;
        rooms[6].Status = Enums.RoomStatus.Maintenance;
        rooms[6].IsMaintenanceRequired = true;
        rooms[6].IsClean = false;

        await _context.MaintenanceTickets.AddRangeAsync(tickets);
    }
}