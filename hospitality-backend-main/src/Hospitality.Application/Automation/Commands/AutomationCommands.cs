namespace Hospitality.Application.Automation.Commands;

public class AutomationRuleDto
{
    public Guid Id { get; set; }
    public Guid HotelId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string TriggerEvent { get; set; } = string.Empty;
    public string Channel { get; set; } = "WhatsApp";
    public string Template { get; set; } = string.Empty;
    public bool IsEnabled { get; set; }
}

public class UpsertAutomationRuleCommand
{
    public Guid HotelId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string TriggerEvent { get; set; } = string.Empty;
    public string Channel { get; set; } = "WhatsApp";
    public string Template { get; set; } = string.Empty;
    public bool IsEnabled { get; set; } = true;
}

public class TriggerEventInfoDto
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}

public class GuestMessageDto
{
    public Guid Id { get; set; }
    public Guid HotelId { get; set; }
    public Guid? RuleId { get; set; }
    public Guid? ReservationId { get; set; }
    public string Recipient { get; set; } = string.Empty;
    public string Channel { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string? Body { get; set; }
    public bool IsSent { get; set; }
    public DateTime? SentAt { get; set; }
    public string? ChannelReference { get; set; }
    public string? Error { get; set; }
}

public class SendMessageCommand
{
    public Guid HotelId { get; set; }
    public Guid ReservationId { get; set; }
    public string Channel { get; set; } = "WhatsApp";
    public string Message { get; set; } = string.Empty;
}

public interface IAutomationService
{
    Guid? GetRuleHotelId(Guid ruleId);
    Guid? GetMessageHotelId(Guid messageId);
    IReadOnlyList<TriggerEventInfoDto> GetTriggerEvents();
    IReadOnlyList<string> GetChannels();
    Task<List<AutomationRuleDto>> GetRulesAsync(Guid hotelId);
    Task<AutomationRuleDto> CreateRuleAsync(UpsertAutomationRuleCommand command);
    Task<AutomationRuleDto> UpdateRuleAsync(Guid id, UpsertAutomationRuleCommand command);
    Task ToggleRuleAsync(Guid id);
    Task DeleteRuleAsync(Guid id);

    /// <summary>
    /// Ejecuta las reglas habilitadas del hotel para un evento de reserva.
    /// Genera y envía un GuestMessage por regla. Nunca lanza sobre el flujo
    /// principal de la reserva (los errores de envío quedan registrados).
    /// </summary>
    Task FireAutomationAsync(Guid hotelId, string triggerEvent, Guid reservationId);

    Task<List<GuestMessageDto>> GetMessagesAsync(Guid hotelId, Guid? reservationId = null, int limit = 50);
    Task<GuestMessageDto> SendManualAsync(SendMessageCommand command);
}