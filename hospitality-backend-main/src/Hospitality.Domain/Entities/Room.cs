namespace Hospitality.Domain.Entities;

public class Room : BaseEntity, ISoftDelete
{
    public string RoomNumber { get; set; } = string.Empty;
    public int Floor { get; set; }
    public Enums.RoomStatus Status { get; set; }
    public string? Features { get; set; }
    public string? Notes { get; set; }
    public bool IsClean { get; set; } = true;
    public bool IsMaintenanceRequired { get; set; }
    public DateTime? LastCleanedAt { get; set; }
    public DateTime? LastMaintenanceAt { get; set; }
    
    // Soft delete
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
    
    // Foreign keys
    public Guid HotelId { get; set; }
    public Guid RoomTypeId { get; set; }
    
    // Navigation properties
    public Hotel Hotel { get; set; } = null!;
    public RoomType RoomType { get; set; } = null!;
    public ICollection<Reservation> Reservations { get; set; } = new List<Reservation>();
    public ICollection<HousekeepingStatus> HousekeepingStatuses { get; set; } = new List<HousekeepingStatus>();
    public ICollection<MaintenanceTicket> MaintenanceTickets { get; set; } = new List<MaintenanceTicket>();
    
    // Propiedades calculadas
    public bool IsAvailable => Status == Enums.RoomStatus.Available && 
                              !IsMaintenanceRequired && 
                              IsClean;
    
    public Reservation? CurrentReservation => Reservations
        .FirstOrDefault(r => r.CheckInDate <= DateTime.UtcNow && 
                           r.CheckOutDate >= DateTime.UtcNow && 
                           r.Status != Enums.ReservationStatus.Cancelled &&
                           r.Status != Enums.ReservationStatus.CheckedOut);
    
    public DateTime? NextCheckIn => Reservations
        .Where(r => r.CheckInDate > DateTime.UtcNow && 
                   r.Status == Enums.ReservationStatus.Confirmed)
        .OrderBy(r => r.CheckInDate)
        .FirstOrDefault()?.CheckInDate;
    
    public DateTime? NextCheckOut => Reservations
        .Where(r => r.CheckOutDate > DateTime.UtcNow && 
                   (r.Status == Enums.ReservationStatus.Confirmed || 
                    r.Status == Enums.ReservationStatus.CheckedIn))
        .OrderBy(r => r.CheckOutDate)
        .FirstOrDefault()?.CheckOutDate;
    
    // Métodos de negocio
    public void MarkAsDirty()
    {
        IsClean = false;
        Status = Enums.RoomStatus.Dirty;
    }
    
    public void MarkAsClean()
    {
        IsClean = true;
        LastCleanedAt = DateTime.UtcNow;
        Status = IsMaintenanceRequired ? Enums.RoomStatus.Maintenance : Enums.RoomStatus.Available;
    }
    
    public void RequestMaintenance()
    {
        IsMaintenanceRequired = true;
        Status = Enums.RoomStatus.Maintenance;
    }
    
    public void CompleteMaintenance()
    {
        IsMaintenanceRequired = false;
        LastMaintenanceAt = DateTime.UtcNow;
        Status = IsClean ? Enums.RoomStatus.Available : Enums.RoomStatus.Dirty;
    }
    
    public bool IsAvailableForDates(DateTime checkIn, DateTime checkOut)
    {
        if (!IsAvailable) return false;
        
        return !Reservations.Any(r => 
            r.Status != Enums.ReservationStatus.Cancelled &&
            r.Status != Enums.ReservationStatus.CheckedOut &&
            checkIn < r.CheckOutDate && 
            checkOut > r.CheckInDate);
    }
}