using Hospitality.Application.Common;
using Hospitality.Application.Common.DTOs;
using Hospitality.Application.Common.Interfaces;
using Hospitality.Application.Finance.Commands;
using Hospitality.Application.Finance.DTOs;
using Hospitality.Domain.Entities;
using Hospitality.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Hospitality.Application.Finance.Services;

public class FinanceService : IFinanceService
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public FinanceService(IApplicationDbContext context, ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<FinanceSummaryDto> GetSummaryAsync(
        Guid? hotelScope = null, DateTime? from = null, DateTime? to = null)
    {
        var reservations = _context.Reservations
            .Where(r => r.Status != ReservationStatus.Cancelled &&
                        r.Status != ReservationStatus.NoShow)
            .AsQueryable();

        if (hotelScope.HasValue) reservations = reservations.Where(r => r.HotelId == hotelScope.Value);
        if (from.HasValue) reservations = reservations.Where(r => r.CheckOutDate.Date >= from.Value.Date);
        if (to.HasValue) reservations = reservations.Where(r => r.CheckInDate.Date <= to.Value.Date);

        var list = await reservations.ToListAsync();

        var completed = list.Where(r => r.Status == ReservationStatus.CheckedOut).ToList();
        var active = list.Where(r => r.Status == ReservationStatus.Confirmed || r.Status == ReservationStatus.CheckedIn).ToList();

        var totalRevenue = completed.Sum(r => r.TotalAmount);
        var outstanding = active.Sum(r => r.BalanceDue);

        var paymentsQuery = _context.Payments
            .Include(p => p.Reservation)
            .Where(p => p.Status == "Completed");

        if (hotelScope.HasValue)
        {
            paymentsQuery = paymentsQuery.Where(p => p.Reservation.HotelId == hotelScope.Value);
        }
        if (from.HasValue) paymentsQuery = paymentsQuery.Where(p => p.PaymentDate.Date >= from.Value.Date);
        if (to.HasValue) paymentsQuery = paymentsQuery.Where(p => p.PaymentDate.Date <= to.Value.Date);

        var payments = await paymentsQuery.ToListAsync();
        var collected = payments.Sum(p => p.IsRefund ? -p.Amount : p.Amount);

        var stays = completed.Concat(active).ToList();
        var adr = stays.Count > 0 ? Math.Round(stays.Average(r => r.RoomRate), 2) : 0;

        var singleHotel = hotelScope.HasValue
            ? await _context.ActiveHotels().FirstAsync(h => h.Id == hotelScope.Value)
            : null;
        var occupancyRate = 0m;
        if (singleHotel != null && singleHotel.TotalRooms > 0)
        {
            var nights = stays.Sum(r => r.NumberOfNights);
            var daySpan = 1;
            if (from.HasValue && to.HasValue)
            {
                daySpan = Math.Max(1, (to.Value.Date - from.Value.Date).Days);
            }
            occupancyRate = Math.Min(100, Math.Round(nights / (singleHotel.TotalRooms * (decimal)daySpan) * 100, 1));
        }

        var invoices = _context.Invoices.ToList();
        if (hotelScope.HasValue)
        {
            invoices = invoices.Where(i => i.Reservation != null && i.Reservation.HotelId == hotelScope.Value).ToList();
        }
        var openInvoices = invoices.Count(i => i.Status is "Issued" or "Sent" or "Overdue");
        var overdueInvoices = invoices.Count(i => i.IsOverdue);

        var methods = payments
            .Where(p => !p.IsRefund)
            .GroupBy(p => p.PaymentMethod)
            .Select(g => new PaymentMethodBreakdown
            {
                Method = g.Key,
                DisplayName = g.First().GetPaymentMethodDisplay(),
                Amount = g.Sum(p => p.Amount),
                Count = g.Count()
            })
            .OrderByDescending(m => m.Amount)
            .ToList();

        return new FinanceSummaryDto
        {
            TotalRevenue = totalRevenue,
            Collected = collected,
            Outstanding = outstanding,
            AverageDailyRate = adr,
            OccupancyRate = occupancyRate,
            CompletedStays = completed.Count,
            OpenInvoices = openInvoices,
            OverdueInvoices = overdueInvoices,
            Methods = methods
        };
    }

    public async Task<PaginatedResult<PaymentDto>> GetPaymentsAsync(
        PaginatedQuery query, Guid? hotelScope = null, string? status = null, string? search = null)
    {
        var dbQuery = _context.Payments
            .Include(p => p.Reservation).ThenInclude(r => r.Room)
            .Include(p => p.Reservation).ThenInclude(r => r.Guest)
            .AsQueryable();

        if (hotelScope.HasValue) dbQuery = dbQuery.Where(p => p.Reservation.HotelId == hotelScope.Value);
        if (!string.IsNullOrWhiteSpace(status) && status != "Todos") dbQuery = dbQuery.Where(p => p.Status == status);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            dbQuery = dbQuery.Where(p =>
                p.Reservation.ReservationNumber.ToLower().Contains(term) ||
                (p.Reservation.Guest.FirstName + " " + p.Reservation.Guest.LastName).ToLower().Contains(term) ||
                (p.ReferenceNumber != null && p.ReferenceNumber.ToLower().Contains(term)));
        }

        var totalCount = await dbQuery.CountAsync();
        var items = await dbQuery
            .OrderByDescending(p => p.PaymentDate)
            .ThenByDescending(p => p.CreatedAt)
            .Skip((query.PageNumber - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync();

        return new PaginatedResult<PaymentDto>(
            items.Select(MapPayment).ToList(),
            totalCount, query.PageNumber, query.PageSize);
    }

    public async Task<PaginatedResult<InvoiceDto>> GetInvoicesAsync(
        PaginatedQuery query, Guid? hotelScope = null, string? status = null, string? search = null)
    {
        var dbQuery = _context.Invoices
            .Include(i => i.Reservation).ThenInclude(r => r.Hotel)
            .Include(i => i.Guest)
            .Where(i => i.ReservationId != null)
            .AsQueryable();

        if (hotelScope.HasValue) dbQuery = dbQuery.Where(i => i.Reservation!.HotelId == hotelScope.Value);
        if (!string.IsNullOrWhiteSpace(status) && status != "Todos") dbQuery = dbQuery.Where(i => i.Status == status);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            dbQuery = dbQuery.Where(i =>
                i.InvoiceNumber.ToLower().Contains(term) ||
                i.CustomerName.ToLower().Contains(term) ||
                (i.Reservation != null && i.Reservation.ReservationNumber.ToLower().Contains(term)));
        }

        var totalCount = await dbQuery.CountAsync();
        var items = await dbQuery
            .OrderByDescending(i => i.IssueDate)
            .Skip((query.PageNumber - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync();

        return new PaginatedResult<InvoiceDto>(
            items.Select(MapInvoice).ToList(),
            totalCount, query.PageNumber, query.PageSize);
    }

    public async Task<InvoiceDetailDto?> GetInvoiceByIdAsync(Guid id, Guid? hotelScope = null)
    {
        var invoice = await _context.Invoices
            .Include(i => i.LineItems)
            .Include(i => i.Reservation).ThenInclude(r => r.Hotel)
            .Include(i => i.Reservation).ThenInclude(r => r.Guest)
            .Include(i => i.Payments)
            .FirstOrDefaultAsync(i => i.Id == id);

        if (invoice == null) return null;
        if (hotelScope.HasValue && invoice.Reservation?.HotelId != hotelScope.Value) return null;

        var dto = MapInvoice(invoice);
        return new InvoiceDetailDto
        {
            Id = dto.Id,
            InvoiceNumber = dto.InvoiceNumber,
            IssueDate = dto.IssueDate,
            DueDate = dto.DueDate,
            Status = dto.Status,
            StatusDescription = dto.StatusDescription,
            CustomerName = dto.CustomerName,
            Subtotal = dto.Subtotal,
            TaxAmount = dto.TaxAmount,
            TotalAmount = dto.TotalAmount,
            AmountPaid = dto.AmountPaid,
            BalanceDue = dto.BalanceDue,
            ReservationNumber = dto.ReservationNumber,
            ReservationId = dto.ReservationId,
            HotelId = dto.HotelId,
            HotelName = dto.HotelName,
            DaysOverdue = dto.DaysOverdue,
            Notes = invoice.Notes,
            PaymentTerms = invoice.PaymentTerms,
            CustomerEmail = invoice.CustomerEmail,
            LineItems = invoice.LineItems.Select(l => new InvoiceLineDto
            {
                Description = l.Description,
                UnitPrice = l.UnitPrice,
                Quantity = l.Quantity,
                Total = l.Total,
                TaxCode = l.TaxCode,
                Category = l.Category
            }).ToList()
        };
    }

    public async Task<PaymentDto> CreatePaymentAsync(CreatePaymentCommand command, string? processedBy = null)
    {
        var reservation = await _context.Reservations
            .Include(r => r.Room)
            .Include(r => r.Guest)
            .FirstOrDefaultAsync(r => r.Id == command.ReservationId)
            ?? throw new KeyNotFoundException($"Reservación con ID {command.ReservationId} no encontrada.");

        if (reservation.Status == ReservationStatus.Cancelled || reservation.Status == ReservationStatus.CheckedOut)
        {
            throw new Domain.Exceptions.ValidationException("No se pueden registrar pagos sobre una reserva cancelada o cerrada.");
        }

        if (command.Amount <= 0)
        {
            throw new Domain.Exceptions.ValidationException("El importe del pago debe ser mayor que cero.");
        }

        var payment = new Payment
        {
            Amount = command.Amount,
            PaymentMethod = command.PaymentMethod,
            CardType = command.CardType,
            LastFourDigits = command.LastFourDigits,
            AuthorizationCode = null,
            ReferenceNumber = command.ReferenceNumber ?? $"PAY-{DateTime.UtcNow:yyyyMMdd}-{Random.Shared.Next(1000, 9999)}",
            Status = "Completed",
            PaymentDate = DateTime.UtcNow,
            ProcessedAt = DateTime.UtcNow,
            ProcessedBy = processedBy ?? "Sistema",
            ReservationId = reservation.Id
        };

        _context.Payments.Add(payment);
        reservation.AmountPaid += payment.Amount;
        reservation.UpdatedAt = DateTime.UtcNow;

        _context.DomainEvents.Add(await DomainEventLog.NewAsync(_context, "invoice", reservation.Id, "PaymentRegistered",
            new
            {
                ReservationId = reservation.Id,
                Amount = payment.Amount,
                PaymentMethod = payment.PaymentMethod,
                ReferenceNumber = payment.ReferenceNumber,
                Currency = reservation.Hotel?.Currency ?? "USD",
                Status = payment.Status
            },
            actorUserId: processedBy ?? _currentUserService.UserId,
            correlationId: reservation.Id));

        await _context.SaveChangesAsync();

        return MapPayment(payment);
    }

    public async Task<InvoiceDto> CreateInvoiceAsync(CreateInvoiceCommand command)
    {
        var reservation = await _context.Reservations
            .Include(r => r.Hotel)
            .Include(r => r.Room).ThenInclude(r => r.RoomType)
            .Include(r => r.Guest)
            .FirstOrDefaultAsync(r => r.Id == command.ReservationId)
            ?? throw new KeyNotFoundException($"Reservación con ID {command.ReservationId} no encontrada.");

        var existing = await _context.Invoices.AnyAsync(i => i.ReservationId == reservation.Id &&
            (i.Status == "Draft" || i.Status == "Issued" || i.Status == "Sent" || i.Status == "Overdue"));
        if (existing)
        {
            throw new Domain.Exceptions.ValidationException("Esta reservación ya tiene una factura activa.");
        }

        var invoice = new Invoice
        {
            InvoiceNumber = $"INV-{DateTime.UtcNow:yyyyMMdd}-{Random.Shared.Next(10000, 99999)}",
            IssueDate = DateTime.UtcNow,
            DueDate = DateTime.UtcNow.AddDays(30),
            Status = "Draft",
            TaxRate = reservation.TaxRate,
            CustomerName = reservation.Guest.FullName,
            CustomerEmail = reservation.Guest.Email,
            CustomerPhone = reservation.Guest.PhoneNumber,
            CustomerTaxId = reservation.Guest.DocumentNumber,
            ReservationId = reservation.Id,
            GuestId = reservation.GuestId,
            PaymentTerms = command.PaymentTerms,
            Notes = command.Notes
        };

        _context.Invoices.Add(invoice);
        await _context.SaveChangesAsync();

        invoice.AddLineItem(
            $"Alojamiento {reservation.Room.RoomNumber} ({reservation.NumberOfNights} noche{(reservation.NumberOfNights > 1 ? "s" : "")})",
            reservation.RoomRate,
            Math.Max(1, reservation.NumberOfNights),
            "IVA16");

        if (reservation.HasExtraBed && reservation.ExtraBedRate > 0)
        {
            invoice.AddLineItem(
                $"Cama extra ({reservation.NumberOfNights} noche{(reservation.NumberOfNights > 1 ? "s" : "")})",
                reservation.ExtraBedRate,
                Math.Max(1, reservation.NumberOfNights),
                "IVA16");
        }

        await _context.SaveChangesAsync();

        return MapInvoice(invoice);
    }

    public async Task<InvoiceDto> IssueInvoiceAsync(Guid id, Guid? hotelScope = null)
    {
        var invoice = await GetScopedInvoiceAsync(id, hotelScope);
        if (invoice.Status != "Draft")
        {
            throw new Domain.Exceptions.ValidationException("Solo se puede emitir una factura en borrador.");
        }

        invoice.Issue();

        var issuanceCurrency = invoice.Reservation?.Hotel?.Currency ?? "USD";
        _context.DomainEvents.Add(await DomainEventLog.NewAsync(_context, "invoice", invoice.Id, "InvoiceIssued",
            new
            {
                InvoiceNumber = invoice.InvoiceNumber,
                ReservationId = invoice.ReservationId,
                Currency = issuanceCurrency,
                Subtotal = invoice.Subtotal,
                TaxAmount = invoice.TaxAmount,
                Total = invoice.TotalAmount
            },
            actorUserId: _currentUserService.UserId,
            correlationId: invoice.ReservationId));

        await _context.SaveChangesAsync();
        return MapInvoice(invoice);
    }

    public async Task<InvoiceDto> CancelInvoiceAsync(Guid id, string? reason, Guid? hotelScope = null)
    {
        var invoice = await GetScopedInvoiceAsync(id, hotelScope);
        invoice.Cancel(string.IsNullOrWhiteSpace(reason) ? "Cancelada por el usuario" : reason);

        _context.DomainEvents.Add(await DomainEventLog.NewAsync(_context, "invoice", invoice.Id, "InvoiceCancelled",
            new
            {
                InvoiceNumber = invoice.InvoiceNumber,
                ReservationId = invoice.ReservationId,
                Currency = invoice.Reservation?.Hotel?.Currency ?? "USD",
                Reason = reason
            },
            actorUserId: _currentUserService.UserId,
            correlationId: invoice.ReservationId));

        await _context.SaveChangesAsync();
        return MapInvoice(invoice);
    }

    public async Task<InvoiceDto> RegisterInvoicePaymentAsync(RegisterInvoicePaymentCommand command, string? processedBy = null)
    {
        var invoice = await _context.Invoices
            .Include(i => i.Reservation).ThenInclude(r => r.Guest)
            .FirstOrDefaultAsync(i => i.Id == command.InvoiceId)
            ?? throw new KeyNotFoundException($"Factura con ID {command.InvoiceId} no encontrada.");

        if (invoice.Status is "Draft" or "Cancelled" or "Paid")
        {
            throw new Domain.Exceptions.ValidationException("La factura no admite pagos en su estado actual.");
        }

        if (command.Amount <= 0)
        {
            throw new Domain.Exceptions.ValidationException("El importe del pago debe ser mayor que cero.");
        }

        var payment = new Payment
        {
            Amount = command.Amount,
            PaymentMethod = command.PaymentMethod,
            ReferenceNumber = $"PAY-{DateTime.UtcNow:yyyyMMdd}-{Random.Shared.Next(1000, 9999)}",
            Status = "Completed",
            PaymentDate = DateTime.UtcNow,
            ProcessedAt = DateTime.UtcNow,
            ProcessedBy = processedBy ?? "Sistema",
            ReservationId = invoice.ReservationId ?? Guid.Empty,
            InvoiceId = invoice.Id
        };

        _context.Payments.Add(payment);
        invoice.AddPayment(command.Amount, payment);

        _context.DomainEvents.Add(await DomainEventLog.NewAsync(_context, "invoice", invoice.Id, "PaymentRegistered",
            new
            {
                InvoiceId = invoice.Id,
                InvoiceNumber = invoice.InvoiceNumber,
                ReservationId = invoice.ReservationId,
                Amount = payment.Amount,
                PaymentMethod = payment.PaymentMethod,
                ReferenceNumber = payment.ReferenceNumber,
                Currency = invoice.Reservation?.Hotel?.Currency ?? "USD",
                Status = payment.Status
            },
            actorUserId: processedBy ?? _currentUserService.UserId,
            correlationId: invoice.ReservationId));

        await _context.SaveChangesAsync();

        return MapInvoice(invoice);
    }

    private async Task<Invoice> GetScopedInvoiceAsync(Guid id, Guid? hotelScope)
    {
        var invoice = await _context.Invoices
            .Include(i => i.Reservation).ThenInclude(r => r.Hotel)
            .FirstOrDefaultAsync(i => i.Id == id)
            ?? throw new KeyNotFoundException($"Factura con ID {id} no encontrada.");

        if (hotelScope.HasValue && invoice.Reservation?.HotelId != hotelScope.Value)
        {
            throw new Domain.Exceptions.ForbiddenAccessException("No tiene permisos para acceder a esta factura.");
        }

        return invoice;
    }

    public async Task<Guid> GetReservationHotelIdAsync(Guid reservationId)
    {
        var reservation = await _context.Reservations
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == reservationId);
        return reservation?.HotelId ?? Guid.Empty;
    }

    private static PaymentDto MapPayment(Payment p)
    {
        return new PaymentDto
        {
            Id = p.Id,
            ReservationId = p.ReservationId,
            ReservationNumber = p.Reservation?.ReservationNumber ?? string.Empty,
            RoomNumber = p.Reservation?.Room?.RoomNumber ?? string.Empty,
            GuestName = p.Reservation?.Guest != null ? p.Reservation.Guest.FullName : string.Empty,
            Amount = p.Amount,
            PaymentMethod = p.PaymentMethod,
            PaymentMethodDisplay = p.GetPaymentMethodDisplay(),
            Status = p.Status,
            ReferenceNumber = p.ReferenceNumber,
            ProcessedBy = p.ProcessedBy,
            PaymentDate = p.PaymentDate,
            IsRefund = p.IsRefund
        };
    }

    private static InvoiceDto MapInvoice(Invoice i)
    {
        return new InvoiceDto
        {
            Id = i.Id,
            InvoiceNumber = i.InvoiceNumber,
            IssueDate = i.IssueDate,
            DueDate = i.DueDate,
            Status = i.Status,
            StatusDescription = i.GetStatusDescription(),
            CustomerName = i.CustomerName,
            Subtotal = i.Subtotal,
            TaxAmount = i.TaxAmount,
            TotalAmount = i.TotalAmount,
            AmountPaid = i.AmountPaid,
            BalanceDue = i.BalanceDue,
            ReservationNumber = i.Reservation?.ReservationNumber ?? string.Empty,
            ReservationId = i.ReservationId,
            HotelId = i.Reservation?.HotelId ?? Guid.Empty,
            HotelName = i.Reservation?.Hotel?.Name ?? string.Empty,
            DaysOverdue = i.DaysOverdue
        };
    }
}