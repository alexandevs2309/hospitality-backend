namespace Hospitality.Domain.Enums;

public enum MaintenanceTicketStatus
{
    Open = 0,        // Ticket abierto
    InProgress = 1,  // En progreso
    OnHold = 2,      // En espera (esperando partes o aprobación)
    Resolved = 3,    // Resuelto
    Closed = 4,      // Cerrado (verificado)
    Cancelled = 5    // Cancelado
}