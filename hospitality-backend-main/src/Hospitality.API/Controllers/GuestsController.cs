using Hospitality.Application.Common.DTOs;
using Hospitality.Application.Common.Interfaces;
using Hospitality.Application.Guests.Commands;
using Hospitality.Application.Guests.DTOs;
using Hospitality.Domain.Exceptions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Hospitality.API.Controllers;

[ApiController]
[Route("api/v1/guests")]
[Authorize]
public class GuestsController : ControllerBase
{
    private readonly IGuestService _guestService;
    private readonly IHotelAccessGuard _accessGuard;

    public GuestsController(IGuestService guestService, IHotelAccessGuard accessGuard)
    {
        _guestService = guestService;
        _accessGuard = accessGuard;
    }

    /// <summary>
    /// Obtiene la lista de huéspedes con búsqueda y paginación
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(PaginatedResult<GuestDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PaginatedResult<GuestDto>>> GetGuests(
        [FromQuery] PaginatedQuery query,
        [FromQuery] string? hotelId = null,
        [FromQuery] string? search = null)
    {
        var hotelScope = _accessGuard.ResolveRequestedHotel(
            string.IsNullOrWhiteSpace(hotelId) ? null : Guid.Parse(hotelId));

        var result = await _guestService.GetGuestsAsync(query, hotelScope, search);
        return Ok(result);
    }

    /// <summary>
    /// Obtiene un huésped por ID (con stats calculados sobre el hotel del usuario)
    /// </summary>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(GuestDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<GuestDto>> GetGuest(Guid id)
    {
        var hotelScope = _accessGuard.CurrentHotelId;
        var guest = await _guestService.GetGuestByIdAsync(id, hotelScope);
        if (guest == null)
        {
            return NotFound($"Huésped con ID {id} no encontrado.");
        }
        return Ok(guest);
    }

    /// <summary>
    /// Obtiene el historial de reservas de un huésped
    /// </summary>
    [HttpGet("{id}/reservations")]
    [ProducesResponseType(typeof(List<GuestReservationDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<GuestReservationDto>>> GetGuestReservations(Guid id)
    {
        var hotelScope = _accessGuard.CurrentHotelId;
        var reservations = await _guestService.GetGuestReservationsAsync(id, hotelScope);
        return Ok(reservations);
    }

    /// <summary>
    /// Crea un huésped manualmente
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(GuestDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<GuestDto>> CreateGuest([FromBody] CreateGuestCommand command)
    {
        try
        {
            var guest = await _guestService.CreateGuestAsync(command);
            return CreatedAtAction(nameof(GetGuest), new { id = guest.Id }, guest);
        }
        catch (ValidationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    /// <summary>
    /// Actualiza los datos de un huésped
    /// </summary>
    [HttpPut("{id}")]
    [ProducesResponseType(typeof(GuestDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<GuestDto>> UpdateGuest(Guid id, [FromBody] UpdateGuestCommand command)
    {
        if (id != command.Id)
        {
            return BadRequest("ID en la URL no coincide con el ID en el cuerpo.");
        }

        try
        {
            var guest = await _guestService.UpdateGuestAsync(command);
            return Ok(guest);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ex.Message);
        }
    }
}