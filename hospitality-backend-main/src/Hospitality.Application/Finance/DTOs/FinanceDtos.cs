namespace Hospitality.Application.Finance.DTOs;

public class FinanceSummaryDto
{
    public decimal TotalRevenue { get; set; }
    public decimal Collected { get; set; }
    public decimal Outstanding { get; set; }
    public decimal AverageDailyRate { get; set; }
    public decimal OccupancyRate { get; set; }
    public int CompletedStays { get; set; }
    public int OpenInvoices { get; set; }
    public int OverdueInvoices { get; set; }
    public List<PaymentMethodBreakdown> Methods { get; set; } = new();
}

public class PaymentMethodBreakdown
{
    public string Method { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public int Count { get; set; }
}

public class PaymentDto
{
    public Guid Id { get; set; }
    public Guid ReservationId { get; set; }
    public string ReservationNumber { get; set; } = string.Empty;
    public string RoomNumber { get; set; } = string.Empty;
    public string GuestName { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string PaymentMethod { get; set; } = string.Empty;
    public string PaymentMethodDisplay { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? ReferenceNumber { get; set; }
    public string? ProcessedBy { get; set; }
    public DateTime PaymentDate { get; set; }
    public bool IsRefund { get; set; }
}

public class InvoiceDto
{
    public Guid Id { get; set; }
    public string InvoiceNumber { get; set; } = string.Empty;
    public DateTime IssueDate { get; set; }
    public DateTime DueDate { get; set; }
    public string Status { get; set; } = string.Empty;
    public string StatusDescription { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public decimal Subtotal { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal AmountPaid { get; set; }
    public decimal BalanceDue { get; set; }
    public string? ReservationNumber { get; set; }
    public Guid? ReservationId { get; set; }
    public Guid HotelId { get; set; }
    public string HotelName { get; set; } = string.Empty;
    public int DaysOverdue { get; set; }
} 
public class InvoiceDetailDto : InvoiceDto
{
    public List<InvoiceLineDto> LineItems { get; set; } = new();
    public string? Notes { get; set; }
    public string? PaymentTerms { get; set; }
    public string? CustomerEmail { get; set; }
}

public class InvoiceLineDto
{
    public string Description { get; set; } = string.Empty;
    public decimal UnitPrice { get; set; }
    public int Quantity { get; set; }
    public decimal Total { get; set; }
    public string? TaxCode { get; set; }
    public string Category { get; set; } = string.Empty;
}