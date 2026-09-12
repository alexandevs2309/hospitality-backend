using Hospitality.Application.Common.DTOs;
using Hospitality.Application.Common.Interfaces;
using Hospitality.Application.Finance.Commands;
using Hospitality.Application.Finance.DTOs;
using Hospitality.Domain.Exceptions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Hospitality.API.Controllers;

[ApiController]
[Route("api/v1/finance")]
[Authorize]
public class FinanceController : ControllerBase
{
    private readonly IFinanceService _financeService;
    private readonly IHotelAccessGuard _accessGuard;

    public FinanceController(IFinanceService financeService, IHotelAccessGuard accessGuard)
    {
        _financeService = financeService;
        _accessGuard = accessGuard;
    }

    /// <summary>
    /// Resumen financiero: ingresos, cobrado, pendiente y desglose por método
    /// </summary>
    [HttpGet("summary")]
    [ProducesResponseType(typeof(FinanceSummaryDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<FinanceSummaryDto>> GetSummary(
        [FromQuery] string? hotelId = null,
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null)
    {
        var hotelScope = _accessGuard.ResolveRequestedHotel(
            string.IsNullOrWhiteSpace(hotelId) ? null : Guid.Parse(hotelId));
        var summary = await _financeService.GetSummaryAsync(hotelScope, from, to);
        return Ok(summary);
    }

    /// <summary>
    /// Lista de pagos con filtros y paginación
    /// </summary>
    [HttpGet("payments")]
    [ProducesResponseType(typeof(PaginatedResult<PaymentDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PaginatedResult<PaymentDto>>> GetPayments(
        [FromQuery] PaginatedQuery query,
        [FromQuery] string? hotelId = null,
        [FromQuery] string? status = null,
        [FromQuery] string? search = null)
    {
        var hotelScope = _accessGuard.ResolveRequestedHotel(
            string.IsNullOrWhiteSpace(hotelId) ? null : Guid.Parse(hotelId));
        var result = await _financeService.GetPaymentsAsync(query, hotelScope, status, search);
        return Ok(result);
    }

    /// <summary>
    /// Lista de facturas con filtros y paginación
    /// </summary>
    [HttpGet("invoices")]
    [ProducesResponseType(typeof(PaginatedResult<InvoiceDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PaginatedResult<InvoiceDto>>> GetInvoices(
        [FromQuery] PaginatedQuery query,
        [FromQuery] string? hotelId = null,
        [FromQuery] string? status = null,
        [FromQuery] string? search = null)
    {
        var hotelScope = _accessGuard.ResolveRequestedHotel(
            string.IsNullOrWhiteSpace(hotelId) ? null : Guid.Parse(hotelId));
        var result = await _financeService.GetInvoicesAsync(query, hotelScope, status, search);
        return Ok(result);
    }

    /// <summary>
    /// Detalle de una factura con sus líneas
    /// </summary>
    [HttpGet("invoices/{id}")]
    [ProducesResponseType(typeof(InvoiceDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<InvoiceDetailDto>> GetInvoice(Guid id)
    {
        var invoice = await _financeService.GetInvoiceByIdAsync(id, _accessGuard.CurrentHotelId);
        if (invoice == null) return NotFound();
        return Ok(invoice);
    }

    /// <summary>
    /// Registra un pago sobre una reserva activa
    /// </summary>
    [HttpPost("payments")]
    [ProducesResponseType(typeof(PaymentDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PaymentDto>> CreatePayment([FromBody] CreatePaymentCommand command)
    {
        var reservation = await GetReservationHotelIdAsync(command);
        _accessGuard.EnsureCanAccessHotel(reservation);

        try
        {
            var payment = await _financeService.CreatePaymentAsync(command, User.Identity?.Name ?? "Sistema");
            return CreatedAtAction(nameof(GetPayments), new { }, payment);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ex.Message);
        }
        catch (ValidationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    /// <summary>
    /// Genera una factura desde una reserva
    /// </summary>
    [HttpPost("invoices")]
    [ProducesResponseType(typeof(InvoiceDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<InvoiceDto>> CreateInvoice([FromBody] CreateInvoiceCommand command)
    {
        var reservation = await GetReservationHotelIdAsync(new CreatePaymentCommand { ReservationId = command.ReservationId });
        _accessGuard.EnsureCanAccessHotel(reservation);

        try
        {
            var invoice = await _financeService.CreateInvoiceAsync(command);
            return CreatedAtAction(nameof(GetInvoice), new { id = invoice.Id }, invoice);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ex.Message);
        }
        catch (ValidationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    /// <summary>
    /// Emite una factura en borrador
    /// </summary>
    [HttpPatch("invoices/{id}/issue")]
    [ProducesResponseType(typeof(InvoiceDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<InvoiceDto>> IssueInvoice(Guid id)
    {
        try
        {
            return Ok(await _financeService.IssueInvoiceAsync(id, _accessGuard.CurrentHotelId));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ex.Message);
        }
        catch (ForbiddenAccessException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, ex.Message);
        }
        catch (ValidationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    /// <summary>
    /// Cancela una factura emitida
    /// </summary>
    [HttpPatch("invoices/{id}/cancel")]
    [ProducesResponseType(typeof(InvoiceDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<InvoiceDto>> CancelInvoice(Guid id, [FromBody] CancelInvoiceCommand? body = null)
    {
        try
        {
            return Ok(await _financeService.CancelInvoiceAsync(id, body?.Reason, _accessGuard.CurrentHotelId));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ex.Message);
        }
        catch (ForbiddenAccessException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, ex.Message);
        }
    }

    /// <summary>
    /// Registra un pago sobre una factura
    /// </summary>
    [HttpPost("invoices/{id}/payment")]
    [ProducesResponseType(typeof(InvoiceDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<InvoiceDto>> RegisterInvoicePayment(Guid id, [FromBody] RegisterInvoicePaymentCommand command)
    {
        command.InvoiceId = id;
        try
        {
            return Ok(await _financeService.RegisterInvoicePaymentAsync(command, User.Identity?.Name ?? "Sistema"));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ex.Message);
        }
        catch (ValidationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    private async Task<Guid> GetReservationHotelIdAsync(CreatePaymentCommand command)
    {
        var reservation = await _financeService.GetReservationHotelIdAsync(command.ReservationId);
        return reservation;
    }
}

public class CancelInvoiceCommand
{
    public string? Reason { get; set; }
}