namespace Hospitality.Domain.Entities;

public class Invoice : BaseEntity
{
    public string InvoiceNumber { get; set; } = string.Empty;
    public DateTime IssueDate { get; set; } = DateTime.UtcNow;
    public DateTime DueDate { get; set; }
    public string Status { get; set; } = "Draft"; // Draft, Issued, Sent, Paid, Overdue, Cancelled
    
    // Información del cliente
    public string CustomerName { get; set; } = string.Empty;
    public string? CustomerEmail { get; set; }
    public string? CustomerPhone { get; set; }
    public string? CustomerAddress { get; set; }
    public string? CustomerTaxId { get; set; }
    
    // Detalles de facturación
    public decimal Subtotal { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal AmountPaid { get; set; }
    public decimal BalanceDue => TotalAmount - AmountPaid;

    // Tasa de impuesto aplicable (por defecto 16% IVA). Configurable por factura.
    public decimal TaxRate { get; set; } = 16.00m;
    
    // Información de pago
    public string? PaymentTerms { get; set; }
    public string? PaymentInstructions { get; set; }
    public DateTime? PaidDate { get; set; }
    public string? PaidBy { get; set; }
    
    // Notas y términos
    public string? Notes { get; set; }
    public string? TermsAndConditions { get; set; }
    
    // Foreign keys
    public Guid? ReservationId { get; set; }
    public Guid? GuestId { get; set; }
    
    // Navigation properties
    public Reservation? Reservation { get; set; }
    public Guest? Guest { get; set; }
    public ICollection<Payment> Payments { get; set; } = new List<Payment>();
    public ICollection<InvoiceLineItem> LineItems { get; set; } = new List<InvoiceLineItem>();
    
    // Métodos de negocio
    public void CalculateTotals()
    {
        Subtotal = LineItems.Sum(item => item.Total);
        TaxAmount = Math.Round(Subtotal * (TaxRate / 100m), 2, MidpointRounding.AwayFromZero);
        TotalAmount = Subtotal + TaxAmount;
    }
    
    public void Issue()
    {
        if (Status == "Draft")
        {
            Status = "Issued";
            IssueDate = DateTime.UtcNow;
            DueDate = IssueDate.AddDays(30); // 30 días por defecto
            
            // Generar número de factura si no existe
            if (string.IsNullOrEmpty(InvoiceNumber))
            {
                InvoiceNumber = $"INV-{IssueDate:yyyyMMdd}-{Id:N}"[..24].ToUpperInvariant();
            }
        }
    }
    
    public void Send()
    {
        if (Status == "Issued")
        {
            Status = "Sent";
        }
    }
    
    public void MarkAsPaid(string paidBy, DateTime paidDate)
    {
        if (Status == "Sent" || Status == "Overdue")
        {
            Status = "Paid";
            PaidBy = paidBy;
            PaidDate = paidDate;
            AmountPaid = TotalAmount;
        }
    }
    
    public void MarkAsOverdue()
    {
        if (Status == "Sent" && DueDate < DateTime.UtcNow)
        {
            Status = "Overdue";
        }
    }
    
    public void Cancel(string reason)
    {
        if (Status != "Paid" && Status != "Cancelled")
        {
            Status = "Cancelled";
            Notes = $"[CANCELADA] {reason} - {DateTime.UtcNow:dd/MM/yyyy HH:mm}";
        }
    }
    
    public void AddPayment(decimal amount, Payment payment)
    {
        Payments.Add(payment);
        AmountPaid += amount;
        
        if (AmountPaid >= TotalAmount && Status != "Paid")
        {
            MarkAsPaid(payment.ProcessedBy ?? "System", payment.PaymentDate);
        }
    }
    
    public void AddLineItem(string description, decimal unitPrice, int quantity, string? taxCode = null)
    {
        var lineItem = new InvoiceLineItem
        {
            Description = description,
            UnitPrice = unitPrice,
            Quantity = quantity,
            TaxCode = taxCode ?? "IVA16",
            InvoiceId = Id
        };
        
        lineItem.CalculateTotal();
        LineItems.Add(lineItem);
        CalculateTotals();
    }
    
    public bool IsOverdue => Status == "Overdue" || 
                           (Status == "Sent" && DueDate < DateTime.UtcNow);
    
    public int DaysOverdue => IsOverdue ? (DateTime.UtcNow - DueDate).Days : 0;
    
    public string GetStatusDescription()
    {
        return Status switch
        {
            "Draft" => "Borrador",
            "Issued" => "Emitida",
            "Sent" => "Enviada",
            "Paid" => "Pagada",
            "Overdue" => $"Vencida ({DaysOverdue} días)",
            "Cancelled" => "Cancelada",
            _ => Status
        };
    }
    
    public string GetStatusColor()
    {
        return Status switch
        {
            "Draft" => "gray",
            "Issued" => "blue",
            "Sent" => "yellow",
            "Paid" => "green",
            "Overdue" => "red",
            "Cancelled" => "gray",
            _ => "gray"
        };
    }
    
    public decimal GetTaxRate() => TaxRate;
}