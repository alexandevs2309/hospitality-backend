namespace Hospitality.Domain.Enums;

public enum MaintenancePriority
{
    Critical = 0,    // Crítico: afecta operación inmediata (ej: no hay agua)
    High = 1,        // Alto: afecta confort del huésped (ej: aire acondicionado)
    Medium = 2,      // Medio: mantenimiento preventivo (ej: cambio de filtros)
    Low = 3          // Bajo: mejoras o mantenimiento cosmético
}