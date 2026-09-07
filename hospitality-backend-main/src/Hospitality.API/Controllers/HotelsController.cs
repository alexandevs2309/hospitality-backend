using Hospitality.Application.Common.DTOs;
using Hospitality.Application.Common.Interfaces;
using Hospitality.Application.Hotels.Commands;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Hospitality.API.Controllers;

[ApiController]
[Route("api/v1/hotels")]
[Authorize]
public class HotelsController : ControllerBase
{
    private readonly IHotelService _hotelService;
    private readonly IHotelAccessGuard _accessGuard;
    private readonly ILogger<HotelsController> _logger;

    public HotelsController(
        IHotelService hotelService,
        IHotelAccessGuard accessGuard,
        ILogger<HotelsController> logger)
    {
        _hotelService = hotelService;
        _accessGuard = accessGuard;
        _logger = logger;
    }

    /// <summary>
    /// Obtiene lista de hoteles con paginación. Los usuarios no-admin solo ven su propio hotel.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(PaginatedResult<HotelDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PaginatedResult<HotelDto>>> GetHotels([FromQuery] PaginatedQuery query)
    {
        var hotelScope = _accessGuard.IsAdmin ? null : _accessGuard.CurrentHotelId;
        var hotels = await _hotelService.GetHotelsAsync(query, hotelScope);
        return Ok(hotels);
    }

    /// <summary>
    /// Obtiene un hotel por ID
    /// </summary>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(HotelDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<HotelDto>> GetHotel(Guid id)
    {
        _accessGuard.EnsureCanAccessHotel(id);
        var hotel = await _hotelService.GetHotelByIdAsync(id);
        if (hotel == null)
        {
            return NotFound($"Hotel con ID {id} no encontrado");
        }
        return Ok(hotel);
    }

    /// <summary>
    /// Crea un hotel. Un usuario no-admin solo puede crear un hotel si aún no tiene uno asignado.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(HotelDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<HotelDto>> CreateHotel([FromBody] CreateHotelCommand command)
    {
        if (!_accessGuard.IsAdmin && _accessGuard.CurrentHotelId.HasValue)
        {
            return Forbid();
        }

        var hotel = await _hotelService.CreateHotelAsync(command);
        return CreatedAtAction(nameof(GetHotel), new { id = hotel.Id }, hotel);
    }

    /// <summary>
    /// Actualiza un hotel existente
    /// </summary>
    [HttpPut("{id}")]
    [Authorize(Roles = "Admin,Manager")]
    [ProducesResponseType(typeof(HotelDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<HotelDto>> UpdateHotel(Guid id, [FromBody] UpdateHotelCommand command)
    {
        _accessGuard.EnsureCanAccessHotel(id);
        if (id != command.Id)
        {
            return BadRequest("ID en la URL no coincide con el ID en el cuerpo");
        }

        var hotel = await _hotelService.UpdateHotelAsync(command);
        return Ok(hotel);
    }

    /// <summary>
    /// Elimina un hotel (soft delete)
    /// </summary>
    [HttpDelete("{id}")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteHotel(Guid id)
    {
        _accessGuard.EnsureCanAccessHotel(id);
        await _hotelService.DeleteHotelAsync(id);
        return NoContent();
    }

    /// <summary>
    /// Cambia el estado activo/inactivo de un hotel
    /// </summary>
    [HttpPatch("{id}/status")]
    [Authorize(Roles = "Admin,Manager")]
    [ProducesResponseType(typeof(HotelDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<HotelDto>> UpdateHotelStatus(Guid id, [FromBody] UpdateHotelStatusCommand command)
    {
        _accessGuard.EnsureCanAccessHotel(id);
        var hotel = await _hotelService.UpdateHotelStatusAsync(id, command);
        return Ok(hotel);
    }

    /// <summary>
    /// Obtiene estadísticas del hotel
    /// </summary>
    [HttpGet("{id}/stats")]
    [ProducesResponseType(typeof(HotelStatsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<HotelStatsDto>> GetHotelStats(Guid id)
    {
        _accessGuard.EnsureCanAccessHotel(id);
        var stats = await _hotelService.GetHotelStatsAsync(id);
        return Ok(stats);
    }

    /// <summary>
    /// Obtiene tipos de habitación del hotel
    /// </summary>
    [HttpGet("{id}/room-types")]
    [ProducesResponseType(typeof(List<RoomTypeDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<RoomTypeDto>>> GetHotelRoomTypes(Guid id)
    {
        _accessGuard.EnsureCanAccessHotel(id);
        var roomTypes = await _hotelService.GetHotelRoomTypesAsync(id);
        return Ok(roomTypes);
    }

    /// <summary>
    /// Obtiene nombres de hoteles para dropdowns
    /// </summary>
    [HttpGet("names")]
    [ProducesResponseType(typeof(List<HotelNameDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<HotelNameDto>>> GetHotelNames()
    {
        var hotelScope = _accessGuard.IsAdmin ? null : _accessGuard.CurrentHotelId;
        var hotelNames = await _hotelService.GetHotelNamesAsync(hotelScope);
        return Ok(hotelNames);
    }

    /// <summary>
    /// Busca hoteles por criterios
    /// </summary>
    [HttpGet("search")]
    [ProducesResponseType(typeof(List<HotelDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<HotelDto>>> SearchHotels(
        [FromQuery] string? name,
        [FromQuery] string? city,
        [FromQuery] int? minStars,
        [FromQuery] bool? isActive)
    {
        var hotelScope = _accessGuard.IsAdmin ? null : _accessGuard.CurrentHotelId;
        var hotels = await _hotelService.SearchHotelsAsync(name, city, minStars, isActive, hotelScope);
        return Ok(hotels);
    }
}