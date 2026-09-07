namespace Hospitality.Domain.Entities;

public class Payment : BaseEntity
{
    public decimal Amount { get; set; }
    public string PaymentMethod { get; set; } = "Cash"; // Cash, CreditCard, DebitCard, Transfer, Check
    public string? CardType { get; set; } // Visa, MasterCard, Amex, etc.
    public string? LastFourDigits { get; set; }
    public string? AuthorizationCode { get; set; }
    public string? ReferenceNumber { get; set; }
    public string Status { get; set; } = "Pending"; // Pending, Completed, Failed, Refunded
    public DateTime PaymentDate { get; set; } = DateTime.UtcNow;
    public DateTime? ProcessedAt { get; set; }
    public string? ProcessedBy { get; set; }
    
    // Información de reembolso
    public bool IsRefund { get; set; }
    public decimal? RefundAmount { get; set; }
    public DateTime? RefundedAt { get; set; }
    public string? RefundReason { get; set; }
    public string? RefundReference { get; set; }
    
    // Foreign keys
    public Guid ReservationId { get; set; }
    public Guid? InvoiceId { get; set; }
    
    // Navigation properties
    public Reservation Reservation { get; set; } = null!;
    public Invoice? Invoice { get; set; }
    
    // Métodos de negocio
    public void Process(string processedBy, string? authorizationCode = null)
    {
        if (Status == "Pending")
        {
            Status = "Completed";
            ProcessedAt = DateTime.UtcNow;
            ProcessedBy = processedBy;
            
            if (!string.IsNullOrEmpty(authorizationCode))
            {
                AuthorizationCode = authorizationCode;
            }
            
            // Actualizar el balance de la reservación
            Reservation.AmountPaid += Amount;
        }
    }
    
    public void Fail(string reason)
    {
        if (Status == "Pending")
        {
            Status = "Failed";
            ProcessedAt = DateTime.UtcNow;
            ReferenceNumber = $"[FALLIDO] {reason}";
        }
    }
    
    public void Refund(decimal amount, string reason, string reference)
    {
        if (Status == "Completed" && !IsRefund)
        {
            IsRefund = true;
            RefundAmount = amount;
            RefundReason = reason;
            RefundReference = reference;
            RefundedAt = DateTime.UtcNow;
            Status = "Refunded";
            
            // Actualizar el balance de la reservación
            Reservation.AmountPaid -= amount;
        }
    }
    
    public string GetPaymentMethodDisplay()
    {
        return PaymentMethod switch
        {
            "Cash" => "Efectivo",
            "CreditCard" => $"Tarjeta de Crédito {CardType}",
            "DebitCard" => $"Tarjeta de Débito {CardType}",
            "Transfer" => "Transferencia Bancaria",
            "Check" => "Cheque",
            _ => PaymentMethod
        };
    }
    
    public string GetMaskedCardNumber()
    {
        if (string.IsNullOrEmpty(LastFourDigits) || LastFourDigits.Length != 4)
            return "**** **** **** ****";
        
        return $"**** **** **** {LastFourDigits}";
    }
    
    public bool IsCardPayment => PaymentMethod == "CreditCard" || PaymentMethod == "DebitCard";
    
    public string GetStatusColor()
    {
        return Status switch
        {
            "Completed" => "green",
            "Pending" => "yellow",
            "Failed" => "red",
            "Refunded" => "blue",
            _ => "gray"
        };
    }
    
    public bool IsValidForReconciliation()
    {
        return Status == "Completed" && 
               ProcessedAt.HasValue && 
               !string.IsNullOrEmpty(ReferenceNumber);
    }
    
    public void AddCardDetails(string cardType, string lastFourDigits)
    {
        if (IsCardPayment)
        {
            CardType = cardType;
            LastFourDigits = lastFourDigits;
        }
    }
}