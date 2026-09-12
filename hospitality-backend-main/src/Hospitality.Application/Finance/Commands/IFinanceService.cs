using Hospitality.Application.Common.DTOs;
using Hospitality.Application.Finance.DTOs;

namespace Hospitality.Application.Finance.Commands;

public class CreatePaymentCommand
{
    public Guid ReservationId { get; set; }
    public decimal Amount { get; set; }
    public string PaymentMethod { get; set; } = "Cash";
    public string? CardType { get; set; }
    public string? LastFourDigits { get; set; }
    public string? ReferenceNumber { get; set; }
}

public class CreateInvoiceCommand
{
    public Guid ReservationId { get; set; }
    public string? Notes { get; set; }
    public string? PaymentTerms { get; set; }
}

public class RegisterInvoicePaymentCommand
{
    public Guid InvoiceId { get; set; }
    public decimal Amount { get; set; }
    public string PaymentMethod { get; set; } = "Cash";
}

public interface IFinanceService
{
    Task<FinanceSummaryDto> GetSummaryAsync(Guid? hotelScope = null, DateTime? from = null, DateTime? to = null);

    Task<PaginatedResult<PaymentDto>> GetPaymentsAsync(
        PaginatedQuery query, Guid? hotelScope = null, string? status = null, string? search = null);

    Task<PaginatedResult<InvoiceDto>> GetInvoicesAsync(
        PaginatedQuery query, Guid? hotelScope = null, string? status = null, string? search = null);

    Task<InvoiceDetailDto?> GetInvoiceByIdAsync(Guid id, Guid? hotelScope = null);

    Task<PaymentDto> CreatePaymentAsync(CreatePaymentCommand command, string? processedBy = null);

    Task<InvoiceDto> CreateInvoiceAsync(CreateInvoiceCommand command);

    Task<InvoiceDto> IssueInvoiceAsync(Guid id, Guid? hotelScope = null);

    Task<InvoiceDto> CancelInvoiceAsync(Guid id, string? reason, Guid? hotelScope = null);

    Task<InvoiceDto> RegisterInvoicePaymentAsync(RegisterInvoicePaymentCommand command, string? processedBy = null);

    Task<Guid> GetReservationHotelIdAsync(Guid reservationId);
}