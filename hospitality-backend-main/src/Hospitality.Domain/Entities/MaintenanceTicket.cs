namespace Hospitality.Domain.Entities;

public class MaintenanceTicket : BaseEntity
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public Enums.MaintenancePriority Priority { get; set; }
    public Enums.MaintenanceTicketStatus Status { get; set; }
    
    // Información de asignación
    public string? AssignedTo { get; set; }
    public string? AssignedDepartment { get; set; }
    public DateTime? AssignedAt { get; set; }
    
    // Tiempos y SLA
    public TimeSpan? EstimatedDuration { get; set; }
    public DateTime? DueDate { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public DateTime? ClosedAt { get; set; }
    
    // Información de resolución
    public string? ResolutionNotes { get; set; }
    public string? ResolutionActions { get; set; }
    public string? ClosedBy { get; set; }
    
    // Costos
    public decimal? EstimatedCost { get; set; }
    public decimal? ActualCost { get; set; }
    
    // Foreign keys
    public Guid RoomId { get; set; }
    public string? ReportedByUserId { get; set; }
    
    // Navigation properties
    public Room Room { get; set; } = null!;
    public ApplicationUser? ReportedByUser { get; set; }
    
    // Métodos de negocio
    public void Assign(string assignedTo, string department, TimeSpan? estimatedDuration = null)
    {
        if (Status == Enums.MaintenanceTicketStatus.Open)
        {
            Status = Enums.MaintenanceTicketStatus.InProgress;
            AssignedTo = assignedTo;
            AssignedDepartment = department;
            AssignedAt = DateTime.UtcNow;
            StartedAt = DateTime.UtcNow;
            
            if (estimatedDuration.HasValue)
            {
                EstimatedDuration = estimatedDuration;
                DueDate = DateTime.UtcNow.Add(estimatedDuration.Value);
            }
        }
    }
    
    public void Complete(string resolutionNotes, string resolutionActions, decimal? actualCost = null)
    {
        if (Status == Enums.MaintenanceTicketStatus.InProgress)
        {
            Status = Enums.MaintenanceTicketStatus.Resolved;
            ResolutionNotes = resolutionNotes;
            ResolutionActions = resolutionActions;
            CompletedAt = DateTime.UtcNow;
            
            if (actualCost.HasValue)
            {
                ActualCost = actualCost.Value;
            }
            
            // Actualizar estado de la habitación
            Room.CompleteMaintenance();
        }
    }
    
    public void Close(string closedBy)
    {
        if (Status == Enums.MaintenanceTicketStatus.Resolved)
        {
            Status = Enums.MaintenanceTicketStatus.Closed;
            ClosedBy = closedBy;
            ClosedAt = DateTime.UtcNow;
        }
    }
    
    public void PutOnHold(string reason)
    {
        if (Status == Enums.MaintenanceTicketStatus.InProgress)
        {
            Status = Enums.MaintenanceTicketStatus.OnHold;
            ResolutionNotes = $"[EN ESPERA] {reason}";
        }
    }
    
    public void Cancel(string reason)
    {
        if (Status != Enums.MaintenanceTicketStatus.Closed && 
            Status != Enums.MaintenanceTicketStatus.Cancelled)
        {
            Status = Enums.MaintenanceTicketStatus.Cancelled;
            ResolutionNotes = $"[CANCELADO] {reason}";
            CompletedAt = DateTime.UtcNow;
        }
    }
    
    public bool IsOverdue => DueDate.HasValue && 
                           DueDate.Value < DateTime.UtcNow && 
                           Status != Enums.MaintenanceTicketStatus.Closed && 
                           Status != Enums.MaintenanceTicketStatus.Cancelled;
    
    public TimeSpan? TimeOpen => Status switch
    {
        Enums.MaintenanceTicketStatus.Closed or Enums.MaintenanceTicketStatus.Cancelled when CompletedAt.HasValue 
            => CompletedAt.Value - CreatedAt,
        _ => DateTime.UtcNow - CreatedAt
    };
    
    public string GetPriorityBadgeColor()
    {
        return Priority switch
        {
            Enums.MaintenancePriority.Critical => "red",
            Enums.MaintenancePriority.High => "orange",
            Enums.MaintenancePriority.Medium => "yellow",
            Enums.MaintenancePriority.Low => "green",
            _ => "gray"
        };
    }
    
    public string GetStatusDescription()
    {
        return Status switch
        {
            Enums.MaintenanceTicketStatus.Open => "Abierto",
            Enums.MaintenanceTicketStatus.InProgress => $"En progreso por {AssignedTo}",
            Enums.MaintenanceTicketStatus.OnHold => "En espera",
            Enums.MaintenanceTicketStatus.Resolved => "Resuelto (pendiente verificación)",
            Enums.MaintenanceTicketStatus.Closed => "Cerrado",
            Enums.MaintenanceTicketStatus.Cancelled => "Cancelado",
            _ => "Desconocido"
        };
    }
    
    public void CalculateDueDateBasedOnPriority()
    {
        DueDate = Priority switch
        {
            Enums.MaintenancePriority.Critical => DateTime.UtcNow.AddHours(2),
            Enums.MaintenancePriority.High => DateTime.UtcNow.AddHours(6),
            Enums.MaintenancePriority.Medium => DateTime.UtcNow.AddDays(1),
            Enums.MaintenancePriority.Low => DateTime.UtcNow.AddDays(3),
            _ => DateTime.UtcNow.AddDays(1)
        };
    }
}