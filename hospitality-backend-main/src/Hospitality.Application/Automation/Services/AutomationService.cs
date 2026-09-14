using Hospitality.Application.Automation.Commands;
using Hospitality.Application.Common.Interfaces;
using Hospitality.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Hospitality.Application.Automation.Services;

/// <summary>
/// Motor de workflows (Fase 3): reglas que reaccionan a eventos de reserva
/// (ReservationCreated/Confirmed/CheckedIn/CheckedOut/Cancelled) enviando
/// mensajes automáticos al huésped por WhatsApp/SMS/Email (proveedor mock).
/// </summary>
public class AutomationService : IAutomationService
{
    private const string EventPrefix = "Reservation";

    private static readonly IReadOnlyList<TriggerEventInfoDto> _triggers = new List<TriggerEventInfoDto>
    {
        new() { Name = "ReservationCreated", Description = "Reserva creada (pendiente de confirmación)." },
        new() { Name = "ReservationConfirmed", Description = "Reserva confirmada." },
        new() { Name = "ReservationCheckedIn", Description = "Huésped registró check-in." },
        new() { Name = "ReservationCheckedOut", Description = "Huésped registró check-out." },
        new() { Name = "ReservationCancelled", Description = "Reserva cancelada." }
    };

    private static readonly IReadOnlyList<string> _channels = new[] { "WhatsApp", "Sms", "Email" };

    private readonly IApplicationDbContext _context;
    private readonly ILogger<AutomationService> _logger;

    public AutomationService(IApplicationDbContext context, ILogger<AutomationService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public IReadOnlyList<TriggerEventInfoDto> GetTriggerEvents() => _triggers;

    public Guid? GetRuleHotelId(Guid ruleId)
        => _context.AutomationRules.Where(r => r.Id == ruleId).Select(r => (Guid?)r.HotelId).FirstOrDefault();

    public Guid? GetMessageHotelId(Guid messageId)
        => _context.GuestMessages.Where(m => m.Id == messageId).Select(m => (Guid?)m.HotelId).FirstOrDefault();

    public IReadOnlyList<string> GetChannels() => _channels;

    public async Task<List<AutomationRuleDto>> GetRulesAsync(Guid hotelId)
    {
        return await _context.AutomationRules
            .Where(r => r.HotelId == hotelId)
            .OrderBy(r => r.TriggerEvent).ThenBy(r => r.Name)
            .Select(r => new AutomationRuleDto
            {
                Id = r.Id,
                HotelId = r.HotelId,
                Name = r.Name,
                TriggerEvent = r.TriggerEvent,
                Channel = r.Channel,
                Template = r.Template,
                IsEnabled = r.IsEnabled
            })
            .ToListAsync();
    }

    public async Task<AutomationRuleDto> CreateRuleAsync(UpsertAutomationRuleCommand command)
    {
        Validate(command);
        var rule = new AutomationRule
        {
            Id = Guid.NewGuid(),
            HotelId = command.HotelId,
            Name = command.Name.Trim(),
            TriggerEvent = command.TriggerEvent,
            Channel = NormalizeChannel(command.Channel),
            Template = command.Template,
            IsEnabled = command.IsEnabled,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _context.AutomationRules.Add(rule);
        await _context.SaveChangesAsync();
        return MapRule(rule);
    }

    public async Task<AutomationRuleDto> UpdateRuleAsync(Guid id, UpsertAutomationRuleCommand command)
    {
        Validate(command);
        var rule = await _context.AutomationRules.FindAsync(id);
        if (rule == null)
        {
            throw new KeyNotFoundException("Regla de automatización no encontrada.");
        }

        rule.Name = command.Name.Trim();
        rule.TriggerEvent = command.TriggerEvent;
        rule.Channel = NormalizeChannel(command.Channel);
        rule.Template = command.Template;
        rule.IsEnabled = command.IsEnabled;
        rule.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        return MapRule(rule);
    }

    public async Task ToggleRuleAsync(Guid id)
    {
        var rule = await _context.AutomationRules.FindAsync(id);
        if (rule == null)
        {
            throw new KeyNotFoundException("Regla de automatización no encontrada.");
        }

        rule.IsEnabled = !rule.IsEnabled;
        rule.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
    }

    public async Task DeleteRuleAsync(Guid id)
    {
        var rule = await _context.AutomationRules.FindAsync(id);
        if (rule != null)
        {
            _context.AutomationRules.Remove(rule);
            await _context.SaveChangesAsync();
        }
    }

    /// <inheritdoc />
    public async Task FireAutomationAsync(Guid hotelId, string triggerEvent, Guid reservationId)
    {
        try
        {
            var rules = await _context.AutomationRules
                .Where(r => r.HotelId == hotelId && r.TriggerEvent == triggerEvent && r.IsEnabled)
                .ToListAsync();

            if (!rules.Any())
            {
                return;
            }

            var context = await LoadRenderContextAsync(reservationId);
            if (context == null)
            {
                return;
            }

            foreach (var rule in rules)
            {
                var message = BuildMessage(rule, context.Value);
                _context.GuestMessages.Add(message);
            }

            await _context.SaveChangesAsync();
            _logger.LogInformation(
                "Automatización: {Count} mensaje(s) generado(s) para evento {Event} de la reserva {Folio} (hotel {Hotel}).",
                rules.Count, triggerEvent, context.Value.Folio, hotelId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "No se pudo ejecutar la automatización {Event} para la reserva {Reservation} (hotel {Hotel}).",
                triggerEvent, reservationId, hotelId);
        }
    }

    public async Task<List<GuestMessageDto>> GetMessagesAsync(Guid hotelId, Guid? reservationId = null, int limit = 50)
    {
        var query = _context.GuestMessages
            .Where(m => m.HotelId == hotelId)
            .OrderByDescending(m => m.CreatedAt)
            .Take(limit);

        if (reservationId.HasValue)
        {
            query = _context.GuestMessages
                .Where(m => m.HotelId == hotelId && m.ReservationId == reservationId.Value)
                .OrderByDescending(m => m.CreatedAt)
                .Take(limit);
        }

        return await query.Select(m => new GuestMessageDto
        {
            Id = m.Id,
            HotelId = m.HotelId,
            RuleId = m.RuleId,
            ReservationId = m.ReservationId,
            Recipient = m.Recipient,
            Channel = m.Channel,
            Subject = m.Subject,
            Body = m.Body,
            IsSent = m.IsSent,
            SentAt = m.SentAt,
            ChannelReference = m.ChannelReference,
            Error = m.Error
        }).ToListAsync();
    }

    public async Task<GuestMessageDto> SendManualAsync(SendMessageCommand command)
    {
        var context = await LoadRenderContextAsync(command.ReservationId);
        if (context == null)
        {
            throw new KeyNotFoundException("Reserva no encontrada.");
        }

        var message = new GuestMessage
        {
            Id = Guid.NewGuid(),
            HotelId = command.HotelId,
            ReservationId = command.ReservationId,
            Recipient = context.Value.Recipient,
            Channel = NormalizeChannel(command.Channel),
            Subject = $"Folio {context.Value.Folio}",
            Body = RenderTemplate(command.Message, context.Value),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        Send(message);
        _context.GuestMessages.Add(message);
        await _context.SaveChangesAsync();

        return MapMessage(message);
    }

    private void Validate(UpsertAutomationRuleCommand command)
    {
        if (string.IsNullOrWhiteSpace(command.Name))
        {
            throw new Hospitality.Domain.Exceptions.ValidationException("El nombre de la regla es obligatorio.");
        }
        if (string.IsNullOrWhiteSpace(command.Template))
        {
            throw new Hospitality.Domain.Exceptions.ValidationException("El template del mensaje es obligatorio.");
        }
        if (!_triggers.Any(t => t.Name == command.TriggerEvent))
        {
            throw new Hospitality.Domain.Exceptions.ValidationException(
                $"Evento de disparo inválido. Use: {string.Join(", ", _triggers.Select(t => t.Name))}.");
        }
        if (!_channels.Contains(command.Channel, StringComparer.OrdinalIgnoreCase))
        {
            throw new Hospitality.Domain.Exceptions.ValidationException(
                $"Canal inválido. Use: {string.Join(", ", _channels)}.");
        }
    }

    private string NormalizeChannel(string channel)
    {
        return _channels.FirstOrDefault(c => c.Equals(channel, StringComparison.OrdinalIgnoreCase)) ?? "WhatsApp";
    }

    private GuestMessage BuildMessage(AutomationRule rule, RenderContext ctx)
    {
        var message = new GuestMessage
        {
            Id = Guid.NewGuid(),
            HotelId = rule.HotelId,
            RuleId = rule.Id,
            ReservationId = ctx.ReservationId,
            Recipient = ctx.Recipient,
            Channel = rule.Channel,
            Subject = $"{rule.Name} · {ctx.Folio}",
            Body = RenderTemplate(rule.Template, ctx),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        Send(message);
        return message;
    }

    /// <summary>
    /// Proveedor mock: simula el envío por el canal (WhatsApp Business API,
    /// SMS gateway o Email) devolviendo un ID de referencia waid_/smsid_/emailid_.
    /// El envío nunca lanza: los errores quedan en GuestMessage.Error.
    /// </summary>
    private static void Send(GuestMessage message)
    {
        try
        {
            var prefix = message.Channel.ToLowerInvariant() switch
            {
                "whatsapp" => "waid_",
                "sms" => "smsid_",
                _ => "emailid_"
            };
            message.IsSent = true;
            message.SentAt = DateTime.UtcNow;
            message.ChannelReference = $"{prefix}{Guid.NewGuid():N}"[..12];
            message.Error = null;
        }
        catch (Exception ex)
        {
            message.IsSent = false;
            message.Error = ex.Message;
        }
    }

    private async Task<RenderContext?> LoadRenderContextAsync(Guid reservationId)
    {
        var reservation = await _context.Reservations
            .Include(r => r.Guest)
            .Include(r => r.Room).ThenInclude(r => r.RoomType)
            .Include(r => r.Hotel)
            .FirstOrDefaultAsync(r => r.Id == reservationId);
        if (reservation == null)
        {
            return null;
        }

        var phone = string.IsNullOrWhiteSpace(reservation.Guest.PhoneNumber) ? "Sin teléfono" : reservation.Guest.PhoneNumber;
        return new RenderContext
        {
            ReservationId = reservation.Id,
            HotelName = reservation.Hotel.Name,
            Recipient = phone,
            GuestName = $"{reservation.Guest.FirstName} {reservation.Guest.LastName}".Trim(),
            Folio = reservation.ReservationNumber,
            Room = reservation.Room.RoomNumber,
            RoomType = reservation.Room.RoomType?.Name ?? string.Empty,
            CheckIn = reservation.CheckInDate.ToString("ddd dd MMM"),
            CheckOut = reservation.CheckOutDate.ToString("ddd dd MMM"),
            Nights = reservation.NumberOfNights,
            Total = reservation.TotalAmount,
            Currency = reservation.Hotel.Currency ?? "USD"
        };
    }

    private static string RenderTemplate(string template, RenderContext ctx)
    {
        return template
            .Replace("{Hotel}", ctx.HotelName)
            .Replace("{Huesped}", ctx.GuestName)
            .Replace("{Folio}", ctx.Folio)
            .Replace("{Habitacion}", ctx.Room)
            .Replace("{TipoHabitacion}", ctx.RoomType)
            .Replace("{Llegada}", ctx.CheckIn)
            .Replace("{Salida}", ctx.CheckOut)
            .Replace("{Noches}", ctx.Nights.ToString())
            .Replace("{Total}", $"{ctx.Total:0.##} {ctx.Currency}");
    }

    private static AutomationRuleDto MapRule(AutomationRule rule) => new()
    {
        Id = rule.Id,
        HotelId = rule.HotelId,
        Name = rule.Name,
        TriggerEvent = rule.TriggerEvent,
        Channel = rule.Channel,
        Template = rule.Template,
        IsEnabled = rule.IsEnabled
    };

    private static GuestMessageDto MapMessage(GuestMessage m) => new()
    {
        Id = m.Id,
        HotelId = m.HotelId,
        RuleId = m.RuleId,
        ReservationId = m.ReservationId,
        Recipient = m.Recipient,
        Channel = m.Channel,
        Subject = m.Subject,
        Body = m.Body,
        IsSent = m.IsSent,
        SentAt = m.SentAt,
        ChannelReference = m.ChannelReference,
        Error = m.Error
    };

    private readonly record struct RenderContext(
        Guid ReservationId,
        string HotelName,
        string Recipient,
        string GuestName,
        string Folio,
        string Room,
        string RoomType,
        string CheckIn,
        string CheckOut,
        int Nights,
        decimal Total,
        string Currency);
}