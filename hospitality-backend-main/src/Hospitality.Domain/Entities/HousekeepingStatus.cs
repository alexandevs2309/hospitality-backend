namespace Hospitality.Domain.Entities;

public class HousekeepingStatus : BaseEntity
{
    public Enums.HousekeepingStatusType Status { get; set; }
    public string? Notes { get; set; }
    public string? HousekeeperName { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public TimeSpan? Duration => StartedAt.HasValue && CompletedAt.HasValue 
        ? CompletedAt.Value - StartedAt.Value 
        : null;
    
    // Prioridad
    public string Priority { get; set; } = "Normal"; // Alta, Media, Baja
    public string? InspectionNotes { get; set; }
    public bool PassedInspection { get; set; }
    public string? InspectorName { get; set; }
    public DateTime? InspectedAt { get; set; }
    
    // Foreign keys
    public Guid RoomId { get; set; }
    
    // Navigation properties
    public Room Room { get; set; } = null!;
    
    // Métodos de negocio
    public void StartCleaning(string housekeeperName)
    {
        if (Status == Enums.HousekeepingStatusType.Dirty)
        {
            Status = Enums.HousekeepingStatusType.InProgress;
            HousekeeperName = housekeeperName;
            StartedAt = DateTime.UtcNow;
            CompletedAt = null;
        }
    }
    
    public void CompleteCleaning(bool passedInspection, string? inspectorName = null)
    {
        if (Status == Enums.HousekeepingStatusType.InProgress)
        {
            Status = Enums.HousekeepingStatusType.Clean;
            CompletedAt = DateTime.UtcNow;
            PassedInspection = passedInspection;
            
            if (passedInspection && !string.IsNullOrEmpty(inspectorName))
            {
                InspectorName = inspectorName;
                InspectedAt = DateTime.UtcNow;
            }
            
            // Actualizar estado de la habitación
            if (passedInspection)
            {
                Room.IsClean = true;
                Room.LastCleanedAt = DateTime.UtcNow;
                Room.Status = Room.IsMaintenanceRequired 
                    ? Enums.RoomStatus.Maintenance 
                    : Enums.RoomStatus.Available;
            }
        }
    }
    
    public void FailInspection(string notes)
    {
        if (Status == Enums.HousekeepingStatusType.Inspection)
        {
            Status = Enums.HousekeepingStatusType.Dirty;
            InspectionNotes = notes;
            PassedInspection = false;
            InspectorName = null;
            InspectedAt = DateTime.UtcNow;
        }
    }
    
    public void SendToInspection()
    {
        if (Status == Enums.HousekeepingStatusType.InProgress)
        {
            Status = Enums.HousekeepingStatusType.Inspection;
            CompletedAt = DateTime.UtcNow;
        }
    }
    
    public void MarkForMaintenance()
    {
        Status = Enums.HousekeepingStatusType.Maintenance;
        Room.RequestMaintenance();
    }
    
    public bool IsUrgent => Priority == "Alta" || 
                           Room.NextCheckIn.HasValue && 
                           (Room.NextCheckIn.Value - DateTime.UtcNow).TotalHours < 4;
    
    public string GetStatusDescription()
    {
        return Status switch
        {
            Enums.HousekeepingStatusType.Clean => "Limpia y lista",
            Enums.HousekeepingStatusType.Dirty => "Necesita limpieza",
            Enums.HousekeepingStatusType.InProgress => $"En limpieza por {HousekeeperName}",
            Enums.HousekeepingStatusType.Inspection => "En inspección",
            Enums.HousekeepingStatusType.Maintenance => "En mantenimiento",
            Enums.HousekeepingStatusType.OutOfService => "Fuera de servicio",
            _ => "Desconocido"
        };
    }
}