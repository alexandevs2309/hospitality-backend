using Hospitality.Application.Common.Interfaces;
using Hospitality.Application.RatePlans.Commands;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Hospitality.API.Controllers;

[ApiController]
[Route("api/v1/rate-plans")]
[Authorize]
public class RatePlansController : ControllerBase
{
    private readonly IRatePlanService _ratePlanService;
    private readonly IHotelAccessGuard _accessGuard;

    public RatePlansController(IRatePlanService ratePlanService, IHotelAccessGuard accessGuard)
    {
        _ratePlanService = ratePlanService;
        _accessGuard = accessGuard;
    }

    /// <summary>
    /// Lista los planes de tarifas de la propiedad.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<RatePlanDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<RatePlanDto>>> GetPlans([FromQuery] Guid? hotelId)
    {
        var resolvedHotelId = _accessGuard.ResolveRequestedHotel(hotelId);
        if (!resolvedHotelId.HasValue)
        {
            return Ok(Array.Empty<RatePlanDto>());
        }

        var plans = await _ratePlanService.GetPlansAsync(resolvedHotelId.Value);
        return Ok(plans);
    }

    /// <summary>
    /// Crea un plan de tarifas (temporada).
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(RatePlanDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<RatePlanDto>> CreatePlan([FromBody] UpsertRatePlanCommand command)
    {
        _accessGuard.EnsureCanAccessHotel(command.HotelId);
        if (!await _accessGuard.IsPropertyOperatorAsync(command.HotelId))
        {
            return Forbid();
        }

        var plan = await _ratePlanService.CreatePlanAsync(command);
        return Ok(plan);
    }

    /// <summary>
    /// Actualiza un plan de tarifas.
    /// </summary>
    [HttpPut("{id}")]
    [ProducesResponseType(typeof(RatePlanDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<RatePlanDto>> UpdatePlan(Guid id, [FromBody] UpsertRatePlanCommand command)
    {
        _accessGuard.EnsureCanAccessHotel(command.HotelId);
        if (!await _accessGuard.IsPropertyOperatorAsync(command.HotelId))
        {
            return Forbid();
        }

        var updated = await _ratePlanService.UpdatePlanAsync(id, command);
        return Ok(updated);
    }

    /// <summary>
    /// Elimina un plan (debe estar sin tipos asignados).
    /// </summary>
    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> DeletePlan(Guid id, [FromQuery] Guid hotelId)
    {
        _accessGuard.EnsureCanAccessHotel(hotelId);
        if (!await _accessGuard.IsPropertyOperatorAsync(hotelId))
        {
            return Forbid();
        }

        try
        {
            await _ratePlanService.DeletePlanAsync(id);
        }
        catch (Hospitality.Domain.Exceptions.ValidationException ex)
        {
            return BadRequest(ex.Message);
        }

        return NoContent();
    }

    /// <summary>
    /// Asigna (o desasigna, con lista vacía) tipos de habitación al plan.
    /// </summary>
    [HttpPut("{id}/assign")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> AssignPlan(Guid id, [FromBody] AssignRatePlanCommand command, [FromQuery] Guid hotelId)
    {
        _accessGuard.EnsureCanAccessHotel(hotelId);
        if (!await _accessGuard.IsPropertyOperatorAsync(hotelId))
        {
            return Forbid();
        }

        await _ratePlanService.AssignPlanAsync(id, command.RoomTypeIds);
        return NoContent();
    }
}