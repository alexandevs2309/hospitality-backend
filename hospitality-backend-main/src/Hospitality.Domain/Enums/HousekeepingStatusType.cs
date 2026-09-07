namespace Hospitality.Domain.Enums;

public enum HousekeepingStatusType
{
    Clean = 0,           // Limpia y lista
    Dirty = 1,           // Sucio, necesita limpieza
    InProgress = 2,      // En proceso de limpieza
    Inspection = 3,      // En inspección
    Maintenance = 4,     // En mantenimiento (bloqueada)
    OutOfService = 5     // Fuera de servicio
}