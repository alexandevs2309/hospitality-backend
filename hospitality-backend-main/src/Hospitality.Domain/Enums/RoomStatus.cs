namespace Hospitality.Domain.Enums;

public enum RoomStatus
{
    Available = 0,        // Habitación disponible
    Occupied = 1,         // Habitación ocupada
    OutOfOrder = 2,       // Fuera de servicio
    Housekeeping = 3,     // En limpieza
    Maintenance = 4,      // En mantenimiento
    Inspected = 5,        // Inspeccionada y lista
    Dirty = 6             // Sucio, necesita limpieza
}