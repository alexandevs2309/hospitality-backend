using Hospitality.Application.Common.Interfaces;
using Hospitality.Domain.Entities;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Hospitality.Infrastructure.Persistence;

public class ApplicationDbContext : IdentityDbContext<ApplicationUser>, IApplicationDbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<Hotel> Hotels => Set<Hotel>();
    public DbSet<Room> Rooms => Set<Room>();
    public DbSet<RoomType> RoomTypes => Set<RoomType>();
    public DbSet<Guest> Guests => Set<Guest>();
    public DbSet<Reservation> Reservations => Set<Reservation>();
    public DbSet<HousekeepingStatus> HousekeepingStatuses => Set<HousekeepingStatus>();
    public DbSet<MaintenanceTicket> MaintenanceTickets => Set<MaintenanceTicket>();
    public DbSet<HotelMetrics> HotelMetrics => Set<HotelMetrics>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<Invoice> Invoices => Set<Invoice>();
    public DbSet<InvoiceLineItem> InvoiceLineItems => Set<InvoiceLineItem>();
    
    // ApplicationUsers ya está definido en IdentityDbContext

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        
        // Aplicar configuraciones desde el assembly de Infrastructure
        builder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);

        // Índices para las consultas más frecuentes (dashboard, disponibilidad, stats).
        builder.Entity<Reservation>().HasIndex(r => new { r.HotelId, r.CheckInDate, r.CheckOutDate });
        builder.Entity<Room>().HasIndex(r => new { r.HotelId, r.Status });

        // Un usuario propietario solo puede tener UN hotel activo. El índice filtrado
        // evita la carrera de doble creación y permite re-crear tras un soft-delete.
        builder.Entity<ApplicationUser>()
            .HasIndex(u => u.HotelId)
            .IsUnique()
            .HasFilter("\"HotelId\" IS NOT NULL AND NOT \"IsDeleted\"");
    }
}