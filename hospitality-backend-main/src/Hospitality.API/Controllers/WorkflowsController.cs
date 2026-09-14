using Hospitality.Application.Automation.Commands;
using Hospitality.Application.Common.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Hospitality.API.Controllers;

[ApiController]
[Route("api/v1/workflows")]
[Authorize]
public class WorkflowsController : ControllerBase
{
    private readonly IAutomationService _automationService;
    private readonly IHotelAccessGuard _accessGuard;

    public WorkflowsController(IAutomationService automationService, IHotelAccessGuard accessGuard)
    {
        _automationService = automationService;
        _accessGuard = accessGuard;
    }

    /// <summary>
    /// Lista los eventos de reserva que pueden disparar automatizaciones.
    /// </summary>
    [HttpGet("triggers")]
    [ProducesResponseType(typeof(IReadOnlyList<TriggerEventInfoDto>), StatusCodes.Status200OK)]
    public IActionResult GetTriggers()
    {
        return Ok(_automationService.GetTriggerEvents());
    }

    /// <summary>
    /// Lista los canales de mensajería disponibles.
    /// </summary>
    [HttpGet("channels")]
    [ProducesResponseType(typeof(IReadOnlyList<string>), StatusCodes.Status200OK)]
    public IActionResult GetChannels()
    {
        return Ok(_automationService.GetChannels());
    }

    /// <summary>
    /// Lista las reglas de automatización del hotel.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(List<AutomationRuleDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<AutomationRuleDto>>> GetRules([FromQuery] Guid? hotelId)
    {
        var resolvedHotelId = _accessGuard.ResolveRequestedHotel(hotelId);
        if (!resolvedHotelId.HasValue)
        {
            return Ok(new List<AutomationRuleDto>());
        }

        var rules = await _automationService.GetRulesAsync(resolvedHotelId.Value);
        return Ok(rules);
    }

    /// <summary>
    /// Crea una regla de automatización (mensajería automática).
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(AutomationRuleDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<AutomationRuleDto>> CreateRule([FromBody] UpsertAutomationRuleCommand command)
    {
        _accessGuard.EnsureCanAccessHotel(command.HotelId);
        if (!await _accessGuard.IsPropertyOperatorAsync(command.HotelId))
        {
            return Forbid();
        }

        try
        {
            var rule = await _automationService.CreateRuleAsync(command);
            return Ok(rule);
        }
        catch (Hospitality.Domain.Exceptions.ValidationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    /// <summary>
    /// Actualiza una regla de automatización.
    /// </summary>
    [HttpPut("{id}")]
    [ProducesResponseType(typeof(AutomationRuleDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<AutomationRuleDto>> UpdateRule(Guid id, [FromBody] UpsertAutomationRuleCommand command)
    {
        var hotelId = _automationService.GetRuleHotelId(id);
        if (!hotelId.HasValue)
        {
            return NotFound("Regla de automatización no encontrada.");
        }
        if (!await IsOperatorAsync(hotelId.Value))
        {
            return Forbid();
        }

        try
        {
            var rule = await _automationService.UpdateRuleAsync(id, command);
            return Ok(rule);
        }
        catch (Hospitality.Domain.Exceptions.ValidationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    /// <summary>
    /// Activa/desactiva una regla sin editar el resto de campos.
    /// </summary>
    [HttpPut("{id}/toggle")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> ToggleRule(Guid id)
    {
        var hotelId = _automationService.GetRuleHotelId(id);
        if (!hotelId.HasValue)
        {
            return NotFound("Regla de automatización no encontrada.");
        }
        if (!await IsOperatorAsync(hotelId.Value))
        {
            return Forbid();
        }

        await _automationService.ToggleRuleAsync(id);
        return NoContent();
    }

    /// <summary>
    /// Elimina una regla de automatización.
    /// </summary>
    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> DeleteRule(Guid id)
    {
        var hotelId = _automationService.GetRuleHotelId(id);
        if (!hotelId.HasValue)
        {
            return NotFound("Regla de automatización no encontrada.");
        }
        if (!await IsOperatorAsync(hotelId.Value))
        {
            return Forbid();
        }

        await _automationService.DeleteRuleAsync(id);
        return NoContent();
    }

    private async Task<bool> IsOperatorAsync(Guid hotelId)
    {
        if (!_accessGuard.CanAccessHotel(hotelId))
        {
            return false;
        }
        return await _accessGuard.IsPropertyOperatorAsync(hotelId);
    }
}