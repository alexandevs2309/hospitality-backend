using Hospitality.Application.Common.DTOs;
using Hospitality.Application.Common.Interfaces;
using Hospitality.Application.Reservations.Commands;
using Hospitality.Domain.Exceptions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Hospitality.API.Controllers;

[ApiController]
[Route("api/v1/reservations")]
[Authorize]
public class ReservationsController : ControllerBase
{
    private readonly IReservationService _reservationService;
    private readonly IHotelAccessGuard _accessGuard;

    public ReservationsController(IReservationService reservationService, IHotelAccessGuard accessGuard)
    {
        _reservationService = reservationService;
        _accessGuard = accessGuard;
    }

    /// <summary>
    /// Obtiene la lista de reservas con filtros y paginación
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(PaginatedResult<ReservationDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PaginatedResult<ReservationDto>>> GetReservations(
        [FromQuery] PaginatedQuery query,
        [FromQuery] string? hotelId = null,
        [FromQuery] string? status = null,
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null,
        [FromQuery] string? search = null)
    {
        var hotelScope = _accessGuard.ResolveRequestedHotel(
            string.IsNullOrWhiteSpace(hotelId) ? null : Guid.Parse(hotelId));

        var result = await _reservationService.GetReservationsAsync(
            query, hotelScope, status, from, to, search);
        return Ok(result);
    }

    /// <summary>
    /// Obtiene una reserva por ID
    /// </summary>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(ReservationDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ReservationDto>> GetReservation(Guid id)
    {
        var reservation = await _reservationService.GetReservationByIdAsync(id);
        if (reservation == null)
        {
            return NotFound($"Reserva con ID {id} no encontrada.");
        }
        _accessGuard.EnsureCanAccessHotel(reservation.HotelId);
        return Ok(reservation);
    }

    /// <summary>
    /// Obtiene las reservas de un huésped
    /// </summary>
    [HttpGet("guest/{guestId}")]
    [ProducesResponseType(typeof(List<ReservationDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<ReservationDto>>> GetGuestReservations(
        Guid guestId,
        [FromQuery] string? hotelId = null)
    {
        var hotelScope = _accessGuard.ResolveRequestedHotel(
            string.IsNullOrWhiteSpace(hotelId) ? null : Guid.Parse(hotelId));
        var reservations = await _reservationService.GetReservationsByGuestAsync(guestId, hotelScope);
        return Ok(reservations);
    }

    /// <summary>
    /// Crea una nueva reserva. Crea o actualiza el huésped automáticamente.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(ReservationDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ReservationDto>> CreateReservation(
        [FromBody] CreateReservationCommand command)
    {
        _accessGuard.EnsureCanAccessHotel(command.HotelId);

        try
        {
            var reservation = await _reservationService.CreateReservationAsync(command);
            return CreatedAtAction(nameof(GetReservation), new { id = reservation.Id }, reservation);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ex.Message);
        }
        catch (ValidationException ex)
        {
            return Conflict(ex.Message);
        }
    }

    /// <summary>
    /// Confirma una reserva pendiente
    /// </summary>
    [HttpPatch("{id}/confirm")]
    [ProducesResponseType(typeof(ReservationDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<ReservationDto>> Confirm(Guid id)
    {
        var reservation = await _reservationService.GetReservationByIdAsync(id);
        if (reservation == null) return NotFound();
        _accessGuard.EnsureCanAccessHotel(reservation.HotelId);

        try
        {
            return Ok(await _reservationService.ConfirmReservationAsync(id));
        }
        catch (ValidationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    /// <summary>
    /// Registra el check-in del huésped
    /// </summary>
    [HttpPatch("{id}/check-in")]
    [ProducesResponseType(typeof(ReservationDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<ReservationDto>> CheckIn(Guid id)
    {
        var reservation = await _reservationService.GetReservationByIdAsync(id);
        if (reservation == null) return NotFound();
        _accessGuard.EnsureCanAccessHotel(reservation.HotelId);

        try
        {
            return Ok(await _reservationService.CheckInReservationAsync(id));
        }
        catch (ValidationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    /// <summary>
    /// Registra el check-out del huésped
    /// </summary>
    [HttpPatch("{id}/check-out")]
    [ProducesResponseType(typeof(ReservationDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<ReservationDto>> CheckOut(Guid id)
    {
        var reservation = await _reservationService.GetReservationByIdAsync(id);
        if (reservation == null) return NotFound();
        _accessGuard.EnsureCanAccessHotel(reservation.HotelId);

        try
        {
            return Ok(await _reservationService.CheckOutReservationAsync(id));
        }
        catch (ValidationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    /// <summary>
    /// Cancela una reserva activa. Marca la habitación como disponible si estaba ocupada.
    /// </summary>
    [HttpPatch("{id}/cancel")]
    [ProducesResponseType(typeof(ReservationDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<ReservationDto>> Cancel(
        Guid id,
        [FromBody] CancelReservationCommand? body = null)
    {
        var reservation = await _reservationService.GetReservationByIdAsync(id);
        if (reservation == null) return NotFound();
        _accessGuard.EnsureCanAccessHotel(reservation.HotelId);

        try
        {
            return Ok(await _reservationService.CancelReservationAsync(id, body?.Reason));
        }
        catch (ValidationException ex)
        {
            return BadRequest(ex.Message);
        }
    }
}