using Hospitality.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace Hospitality.Application.Common.Interfaces;

public interface IApplicationDbContext
{
    DbSet<Organization> Organizations { get; }
    DbSet<OrganizationMember> OrganizationMembers { get; }
    DbSet<PropertyAssignment> PropertyAssignments { get; }
    DbSet<RoomRate> RoomRates { get; }
    DbSet<RatePlan> RatePlans { get; }
    DbSet<Channel> Channels { get; }
    DbSet<ChannelMapping> ChannelMappings { get; }
    DbSet<DomainEvent> DomainEvents { get; }
    DbSet<OutboxMessage> OutboxMessages { get; }

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
    DbSet<UserSession> UserSessions { get; }

    DatabaseFacade Database { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}