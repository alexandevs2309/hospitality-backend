using Hospitality.Application.Automation.Commands;
using Hospitality.Application.Common.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Hospitality.API.Controllers;

[ApiController]
[Route("api/v1/messages")]
[Authorize]
public class MessagesController : ControllerBase
{
    private readonly IAutomationService _automationService;
    private readonly IHotelAccessGuard _accessGuard;

    public MessagesController(IAutomationService automationService, IHotelAccessGuard accessGuard)
    {
        _automationService = automationService;
        _accessGuard = accessGuard;
    }

    /// <summary>
    /// Lista los canales de mensajería disponibles (WhatsApp/Sms/Email).
    /// </summary>
    [HttpGet("channels")]
    [ProducesResponseType(typeof(IReadOnlyList<string>), StatusCodes.Status200OK)]
    public IActionResult GetChannels()
    {
        return Ok(_automationService.GetChannels());
    }

    /// <summary>
    /// Historial de mensajes enviados al huésped (automatizados y manuales).
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(List<GuestMessageDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<GuestMessageDto>>> GetMessages(
        [FromQuery] Guid? hotelId = null,
        [FromQuery] Guid? reservationId = null,
        [FromQuery] int limit = 50)
    {
        var resolvedHotelId = _accessGuard.ResolveRequestedHotel(hotelId);
        if (!resolvedHotelId.HasValue)
        {
            return Ok(new List<GuestMessageDto>());
        }

        var messages = await _automationService.GetMessagesAsync(resolvedHotelId.Value, reservationId, limit);
        return Ok(messages);
    }

    /// <summary>
    /// Envía un mensaje manual al huésped de una reserva por el canal indicado.
    /// </summary>
    [HttpPost("send")]
    [ProducesResponseType(typeof(GuestMessageDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<GuestMessageDto>> SendMessage([FromBody] SendMessageCommand command)
    {
        _accessGuard.EnsureCanAccessHotel(command.HotelId);
        if (!await _accessGuard.IsPropertyOperatorAsync(command.HotelId))
        {
            return Forbid();
        }

        try
        {
            var message = await _automationService.SendManualAsync(command);
            return Ok(message);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ex.Message);
        }
    }
}