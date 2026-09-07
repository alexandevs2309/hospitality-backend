namespace Hospitality.Domain.Entities;

public class Reservation : BaseEntity
{
    public string ReservationNumber { get; set; } = string.Empty;
    public DateTime CheckInDate { get; set; }
    public DateTime CheckOutDate { get; set; }
    public int NumberOfGuests { get; set; } = 1;
    public bool HasExtraBed { get; set; }
    public string? SpecialRequests { get; set; }
    
    // Estados
    public Enums.ReservationStatus Status { get; set; }
    public DateTime? CheckedInAt { get; set; }
    public DateTime? CheckedOutAt { get; set; }
    public DateTime? CancelledAt { get; set; }
    public string? CancellationReason { get; set; }
    
    // Información de pago
    public decimal RoomRate { get; set; }
    public decimal ExtraBedRate { get; set; }
    public decimal TaxRate { get; set; } = 16.00m; // IVA
    public decimal TotalAmount { get; set; }
    public decimal AmountPaid { get; set; }
    public decimal BalanceDue => TotalAmount - AmountPaid;
    public bool IsFullyPaid => BalanceDue <= 0;
    
    // Información de reserva
    public string Source { get; set; } = "Directo"; // Directo, OTA, Agencia, etc.
    public string? BookingReference { get; set; }
    public DateTime? GuaranteeUntil { get; set; }
    
    // Foreign keys
    public Guid HotelId { get; set; }
    public Guid RoomId { get; set; }
    public Guid GuestId { get; set; }
    
    // Navigation properties
    public Hotel Hotel { get; set; } = null!;
    public Room Room { get; set; } = null!;
    public Guest Guest { get; set; } = null!;
    public ICollection<Payment> Payments { get; set; } = new List<Payment>();
    
    // Propiedades calculadas
    public int NumberOfNights => (int)(CheckOutDate - CheckInDate).TotalDays;
    public decimal RoomCharges => RoomRate * NumberOfNights;
    public decimal ExtraBedCharges => HasExtraBed ? ExtraBedRate * NumberOfNights : 0;
    public decimal Subtotal => RoomCharges + ExtraBedCharges;
    public decimal Taxes => Subtotal * (TaxRate / 100);
    
    public bool IsActive => Status == Enums.ReservationStatus.Confirmed || 
                           Status == Enums.ReservationStatus.CheckedIn;
    
    public bool IsPast => CheckOutDate < DateTime.UtcNow;
    public bool IsFuture => CheckInDate > DateTime.UtcNow;
    public bool IsCurrent => CheckInDate <= DateTime.UtcNow && CheckOutDate >= DateTime.UtcNow;
    
    // Métodos de negocio
    public void CalculateTotal()
    {
        TotalAmount = Subtotal + Taxes;
    }
    
    public void Confirm()
    {
        if (Status == Enums.ReservationStatus.Pending)
        {
            Status = Enums.ReservationStatus.Confirmed;
        }
    }
    
    public void CheckIn()
    {
        if (Status == Enums.ReservationStatus.Confirmed)
        {
            Status = Enums.ReservationStatus.CheckedIn;
            CheckedInAt = DateTime.UtcNow;
            Room.Status = Enums.RoomStatus.Occupied;
        }
    }
    
    public void CheckOut()
    {
        if (Status == Enums.ReservationStatus.CheckedIn)
        {
            Status = Enums.ReservationStatus.CheckedOut;
            CheckedOutAt = DateTime.UtcNow;
            Room.Status = Enums.RoomStatus.Dirty;
        }
    }
    
    public void Cancel(string reason)
    {
        if (Status != Enums.ReservationStatus.CheckedOut && 
            Status != Enums.ReservationStatus.Cancelled)
        {
            Status = Enums.ReservationStatus.Cancelled;
            CancelledAt = DateTime.UtcNow;
            CancellationReason = reason;
        }
    }
    
    public void MarkAsNoShow()
    {
        if (Status == Enums.ReservationStatus.Confirmed && CheckInDate < DateTime.UtcNow)
        {
            Status = Enums.ReservationStatus.NoShow;
        }
    }
    
    public void AddPayment(decimal amount, string method, string reference)
    {
        var payment = new Payment
        {
            Amount = amount,
            PaymentMethod = method,
            ReferenceNumber = reference,
            Status = "Completado",
            ReservationId = Id
        };
        
        Payments.Add(payment);
        AmountPaid += amount;
    }
    
    public decimal CalculateCancellationFee()
    {
        if (CheckInDate <= DateTime.UtcNow) return TotalAmount;
        
        var daysUntilCheckIn = (CheckInDate - DateTime.UtcNow).Days;
        
        return daysUntilCheckIn switch
        {
            < 1 => TotalAmount, // Mismo día
            < 3 => TotalAmount * 0.5m, // 1-2 días
            < 7 => TotalAmount * 0.25m, // 3-6 días
            _ => 0 // 7+ días, sin cargo
        };
    }
}