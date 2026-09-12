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

    public DbSet<Organization> Organizations => Set<Organization>();
    public DbSet<OrganizationMember> OrganizationMembers => Set<OrganizationMember>();
    public DbSet<PropertyAssignment> PropertyAssignments => Set<PropertyAssignment>();
    public DbSet<RoomRate> RoomRates => Set<RoomRate>();
    public DbSet<RatePlan> RatePlans => Set<RatePlan>();
    public DbSet<Channel> Channels => Set<Channel>();
    public DbSet<ChannelMapping> ChannelMappings => Set<ChannelMapping>();
    public DbSet<DomainEvent> DomainEvents => Set<DomainEvent>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

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
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<UserSession> UserSessions => Set<UserSession>();
    
    // ApplicationUsers ya está definido en IdentityDbContext

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        
        // Aplicar configuraciones desde el assembly de Infrastructure
        builder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);

        // Fase 0: Organización → Propiedad.
        builder.Entity<Organization>(entity =>
        {
            entity.ToTable("Organizations");
            entity.Property(o => o.Name).HasMaxLength(200).IsRequired();
            entity.Property(o => o.BusinessName).HasMaxLength(200);
            entity.Property(o => o.TaxId).HasMaxLength(32);
            entity.Property(o => o.Email).HasMaxLength(256);
            entity.Property(o => o.PhoneNumber).HasMaxLength(64);
            entity.Property(o => o.Plan).HasMaxLength(32);
            entity.Property(o => o.SelectedModules).HasColumnType("jsonb");
            entity.Property(o => o.DefaultCurrency).HasMaxLength(8);
            entity.Property(o => o.TimeZone).HasMaxLength(64);
            entity.HasIndex(o => o.Name);
            entity.HasIndex(o => new { o.IsActive, o.IsDeleted });
        });

        builder.Entity<OrganizationMember>(entity =>
        {
            entity.HasIndex(m => new { m.OrganizationId, m.UserId })
                .IsUnique()
                .HasFilter("\"IsDeleted\" = false");
            entity.HasIndex(m => m.UserId);
        });

        builder.Entity<PropertyAssignment>(entity =>
        {
            entity.HasIndex(p => new { p.PropertyId, p.UserId })
                .IsUnique()
                .HasFilter("\"IsDeleted\" = false");
            entity.HasIndex(p => p.UserId);
            entity.HasIndex(p => p.PropertyId);
        });

        // Motor de tarifas: una tarifa por (hotel, tipo de habitación, fecha).
        builder.Entity<RoomRate>(entity =>
        {
            entity.HasOne(r => r.Hotel)
                .WithMany()
                .HasForeignKey(r => r.HotelId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(r => r.RoomType)
                .WithMany()
                .HasForeignKey(r => r.RoomTypeId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(r => new { r.HotelId, r.RoomTypeId, r.Date }).IsUnique();
        });

        builder.Entity<RatePlan>(entity =>
        {
            entity.HasIndex(p => new { p.HotelId, p.Name }).IsUnique();
            entity.HasIndex(p => p.IsDefault);
            entity.HasOne(p => p.Hotel)
                .WithMany()
                .HasForeignKey(p => p.HotelId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Canales de venta (onboarding).
        builder.Entity<Channel>(entity =>
        {
            entity.HasIndex(c => new { c.HotelId, c.Name }).IsUnique();
            entity.HasOne(c => c.Hotel)
                .WithMany()
                .HasForeignKey(c => c.HotelId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<ChannelMapping>(entity =>
        {
            entity.HasIndex(m => new { m.ChannelId, m.RoomTypeId }).IsUnique();
            entity.HasOne(m => m.Channel)
                .WithMany()
                .HasForeignKey(m => m.ChannelId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(m => m.RoomType)
                .WithMany()
                .HasForeignKey(m => m.RoomTypeId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Un RoomType pertenece a un único plan de tarifas (opcional).
        builder.Entity<RoomType>()
            .HasOne(rt => rt.RatePlan)
            .WithMany(plan => plan.RoomTypes)
            .HasForeignKey(rt => rt.RatePlanId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.SetNull);

        // Índices para las consultas más frecuentes (dashboard, disponibilidad, stats).
        builder.Entity<Reservation>().HasIndex(r => new { r.HotelId, r.CheckInDate, r.CheckOutDate });
        builder.Entity<Room>().HasIndex(r => new { r.HotelId, r.Status });

        // Propiedad → Organización (restrict: no arrastrar los hoteles al borrar el tenant).
        builder.Entity<Hotel>()
            .HasOne(h => h.Organization)
            .WithMany(o => o.Properties)
            .HasForeignKey(h => h.OrganizationId)
            .OnDelete(DeleteBehavior.Restrict);

        // Un usuario propietario solo puede tener UN hotel activo. El índice filtrado
        // evita la carrera de doble creación y permite re-crear tras un soft-delete.
        builder.Entity<ApplicationUser>()
            .HasIndex(u => u.HotelId)
            .IsUnique()
            .HasFilter("\"HotelId\" IS NOT NULL AND NOT \"IsDeleted\"");

        // Event sourcing: log de eventos por agregado (reservación / folio).
        builder.Entity<DomainEvent>(entity =>
        {
            entity.ToTable("DomainEvents");
            entity.Property(e => e.AggregateType).HasMaxLength(64).IsRequired();
            entity.Property(e => e.EventType).HasMaxLength(100).IsRequired();
            entity.Property(e => e.Payload).HasColumnType("jsonb");
            entity.Property(e => e.ActorUserId).HasMaxLength(450);
            entity.HasIndex(e => new { e.AggregateType, e.AggregateId });
            entity.HasIndex(e => new { e.AggregateType, e.AggregateId, e.Version }).IsUnique();
            entity.HasIndex(e => e.OccurredOn);
        });

        // Outbox: publicación hacia integraciones (MCP Gateway, WhatsApp, webhooks).
        builder.Entity<OutboxMessage>(entity =>
        {
            entity.ToTable("OutboxMessages");
            entity.Property(o => o.Topic).HasMaxLength(64).IsRequired();
            entity.Property(o => o.Payload).HasColumnType("jsonb");
            entity.Property(o => o.Status).HasMaxLength(16);
            entity.HasIndex(o => new { o.Status, o.NextAttemptAt });
        });

        // Auditoría: índices para los filtros habituales (usuario, acción, fecha).
        builder.Entity<AuditLog>(entity =>
        {
            entity.ToTable("AuditLogs");
            entity.Property(e => e.Action).HasMaxLength(64).IsRequired();
            entity.Property(e => e.Module).HasMaxLength(64);
            entity.Property(e => e.Entity).HasMaxLength(64);
            entity.Property(e => e.EntityId).HasMaxLength(128);
            entity.Property(e => e.UserName).HasMaxLength(256);
            entity.Property(e => e.UserId).HasMaxLength(450);
            entity.Property(e => e.IpAddress).HasMaxLength(64);
            entity.Property(e => e.Details).HasMaxLength(2000);
            entity.HasIndex(e => e.CreatedAt);
            entity.HasIndex(e => e.Action);
            entity.HasIndex(e => e.UserId);
        });

        // Sesiones de usuario: un token de refresco por dispositivo.
        builder.Entity<UserSession>(entity =>
        {
            entity.ToTable("UserSessions");
            entity.Property(e => e.UserId).HasMaxLength(450).IsRequired();
            entity.Property(e => e.DeviceName).HasMaxLength(200);
            entity.Property(e => e.DeviceFingerprint).HasMaxLength(128);
            entity.Property(e => e.UserAgent).HasMaxLength(512);
            entity.Property(e => e.IpAddress).HasMaxLength(64);
            entity.Property(e => e.RefreshTokenHash).HasMaxLength(128).IsRequired();
            entity.HasIndex(e => e.UserId);
            entity.HasIndex(e => e.RefreshTokenHash).IsUnique();
            entity.HasIndex(e => e.DeviceFingerprint);
        });
    }
}