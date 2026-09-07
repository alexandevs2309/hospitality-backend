using Hospitality.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace Hospitality.Application.Common.Interfaces;

public interface IApplicationDbContext
{
    DbSet<Hotel> Hotels { get; }
    DbSet<Room> Rooms { get; }
    DbSet<RoomType> RoomTypes { get; }
    DbSet<Guest> Guests { get; }
    DbSet<Reservation> Reservations { get; }
    DbSet<HousekeepingStatus> HousekeepingStatuses { get; }
    DbSet<MaintenanceTicket> MaintenanceTickets { get; }
    DbSet<HotelMetrics> HotelMetrics { get; }
    DbSet<Payment> Payments { get; }
    DbSet<Invoice> Invoices { get; }
    DbSet<InvoiceLineItem> InvoiceLineItems { get; }
    DbSet<ApplicationUser> Users { get; }

    DatabaseFacade Database { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}