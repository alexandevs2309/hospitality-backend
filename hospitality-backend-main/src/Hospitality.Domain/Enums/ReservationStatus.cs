namespace Hospitality.Domain.Enums;

public enum ReservationStatus
{
    Pending = 0,      // Reserva pendiente de confirmación
    Confirmed = 1,    // Reserva confirmada
    CheckedIn = 2,    // Huésped ha hecho check-in
    CheckedOut = 3,   // Huésped ha hecho check-out
    Cancelled = 4,    // Reserva cancelada
    NoShow = 5        // Huésped no se presentó
}