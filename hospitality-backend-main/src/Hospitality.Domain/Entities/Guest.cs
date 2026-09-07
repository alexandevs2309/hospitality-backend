namespace Hospitality.Domain.Entities;

public class Guest : BaseEntity, ISoftDelete
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public string? Address { get; set; }
    public string? City { get; set; }
    public string? Country { get; set; }
    public string? PostalCode { get; set; }
    
    // Documento de identificación
    public string DocumentType { get; set; } = "Pasaporte";
    public string DocumentNumber { get; set; } = string.Empty;
    public string Nationality { get; set; } = string.Empty;
    public DateTime? DateOfBirth { get; set; }
    
    // Preferencias
    public string? Preferences { get; set; }
    public string? SpecialRequests { get; set; }
    public string? DietaryRestrictions { get; set; }
    
    // Programa de fidelización
    public int LoyaltyPoints { get; set; }
    public string? LoyaltyTier { get; set; }
    
    // Información de contacto de emergencia
    public string? EmergencyContactName { get; set; }
    public string? EmergencyContactPhone { get; set; }
    public string? EmergencyContactRelationship { get; set; }
    
    // Soft delete
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
    
    // Navigation properties
    public ICollection<Reservation> Reservations { get; set; } = new List<Reservation>();
    
    // Propiedades calculadas
    public string FullName => $"{FirstName} {LastName}";
    
    public int TotalStays => Reservations
        .Count(r => r.Status == Enums.ReservationStatus.CheckedOut);
    
    public decimal TotalSpent => Reservations
        .Where(r => r.Status == Enums.ReservationStatus.CheckedOut)
        .Sum(r => r.TotalAmount);
    
    public DateTime? LastStayDate => Reservations
        .Where(r => r.Status == Enums.ReservationStatus.CheckedOut)
        .OrderByDescending(r => r.CheckOutDate)
        .FirstOrDefault()?.CheckOutDate;
    
    // Métodos de negocio
    public void AddLoyaltyPoints(int points)
    {
        LoyaltyPoints += points;
        
        // Actualizar tier basado en puntos
        LoyaltyTier = LoyaltyPoints switch
        {
            >= 10000 => "Platinum",
            >= 5000 => "Gold",
            >= 1000 => "Silver",
            _ => "Standard"
        };
    }
    
    public bool IsVIP => LoyaltyTier == "Platinum" || LoyaltyTier == "Gold";
    
    public IEnumerable<Reservation> GetUpcomingReservations()
    {
        return Reservations
            .Where(r => r.CheckInDate > DateTime.UtcNow && 
                       r.Status == Enums.ReservationStatus.Confirmed);
    }
    
    public IEnumerable<Reservation> GetActiveReservations()
    {
        return Reservations
            .Where(r => r.CheckInDate <= DateTime.UtcNow && 
                       r.CheckOutDate >= DateTime.UtcNow &&
                       (r.Status == Enums.ReservationStatus.Confirmed || 
                        r.Status == Enums.ReservationStatus.CheckedIn));
    }
}