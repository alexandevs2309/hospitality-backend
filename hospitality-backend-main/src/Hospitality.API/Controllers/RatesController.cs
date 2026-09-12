using Hospitality.Application.Common.Interfaces;
using Hospitality.Application.Rates.Commands;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Hospitality.API.Controllers;

[ApiController]
[Route("api/v1/rates")]
[Authorize]
public class RatesController : ControllerBase
{
    private readonly IRateService _rateService;
    private readonly IHotelAccessGuard _accessGuard;

    public RatesController(IRateService rateService, IHotelAccessGuard accessGuard)
    {
        _rateService = rateService;
        _accessGuard = accessGuard;
    }

    /// <summary>
    /// Obtiene la parrilla de tarifas diarias por tipo de habitación (override ?? precio base).
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<RoomRateDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<RoomRateDto>>> GetRates(
        [FromQuery] Guid? hotelId,
        [FromQuery] Guid? roomTypeId,
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to)
    {
        var resolvedHotelId = _accessGuard.ResolveRequestedHotel(hotelId);
        if (!resolvedHotelId.HasValue)
        {
            return Ok(Array.Empty<RoomRateDto>());
        }

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var start = from ?? DateOnly.FromDateTime(new DateTime(today.Year, today.Month, 1));
        var end = to ?? start.AddMonths(1).AddDays(-1);

        if (end.DayNumber - start.DayNumber > 731)
        {
            return BadRequest("El rango máximo es de 24 meses.");
        }

        var rates = await _rateService.GetRatesAsync(resolvedHotelId.Value, roomTypeId, start, end);
        return Ok(rates);
    }

    /// <summary>
    /// Aplica una tarifa (o la actualiza) a un rango de fechas para un tipo de habitación.
    /// </summary>
    [HttpPut]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> SetRateRange([FromBody] SetRateRangeCommand command)
    {
        _accessGuard.EnsureCanAccessHotel(command.HotelId);
        if (!await _accessGuard.IsPropertyOperatorAsync(command.HotelId))
        {
            return Forbid();
        }

        try
        {
            await _rateService.SetRateRangeAsync(command);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }

        return NoContent();
    }

    /// <summary>
    /// Elimina las tarifas override de un rango de fechas (vuelve a precio base).
    /// </summary>
    [HttpDelete]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> ClearRateRange(
        [FromQuery] Guid hotelId,
        [FromQuery] Guid roomTypeId,
        [FromQuery] DateOnly from,
        [FromQuery] DateOnly to)
    {
        _accessGuard.EnsureCanAccessHotel(hotelId);
        if (!await _accessGuard.IsPropertyOperatorAsync(hotelId))
        {
            return Forbid();
        }

        await _rateService.ClearRateRangeAsync(hotelId, roomTypeId, from, to);
        return NoContent();
    }
}