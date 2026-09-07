using Hospitality.Application.Common.DTOs;
using Hospitality.Application.Common.Interfaces;
using Hospitality.Application.Rooms.Commands;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Hospitality.API.Controllers;

[ApiController]
[Route("api/v1/rooms")]
[Authorize]
public class RoomsController : ControllerBase
{
    private readonly IRoomService _roomService;
    private readonly IHotelAccessGuard _accessGuard;

    public RoomsController(IRoomService roomService, IHotelAccessGuard accessGuard)
    {
        _roomService = roomService;
        _accessGuard = accessGuard;
    }

    /// <summary>
    /// Obtiene lista de habitaciones con paginación
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(PaginatedResult<RoomDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PaginatedResult<RoomDto>>> GetRooms([FromQuery] PaginatedQuery query)
    {
        var hotelScope = _accessGuard.IsAdmin ? null : _accessGuard.CurrentHotelId;
        var rooms = await _roomService.GetRoomsAsync(query, hotelScope);
        return Ok(rooms);
    }

    /// <summary>
    /// Obtiene una habitación por ID
    /// </summary>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(RoomDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<RoomDto>> GetRoom(Guid id)
    {
        var room = await _roomService.GetRoomByIdAsync(id);
        if (room == null)
        {
            return NotFound($"Habitación con ID {id} no encontrada");
        }
        _accessGuard.EnsureCanAccessHotel(room.HotelId);
        return Ok(room);
    }

    /// <summary>
    /// Obtiene habitaciones por hotel
    /// </summary>
    [HttpGet("hotel/{hotelId}")]
    [ProducesResponseType(typeof(List<RoomDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<RoomDto>>> GetRoomsByHotel(Guid hotelId)
    {
        _accessGuard.EnsureCanAccessHotel(hotelId);
        var rooms = await _roomService.GetRoomsByHotelAsync(hotelId);
        return Ok(rooms);
    }

    /// <summary>
    /// Obtiene habitaciones disponibles para un rango de fechas
    /// </summary>
    [HttpGet("available")]
    [ProducesResponseType(typeof(List<RoomDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<RoomDto>>> GetAvailableRooms(
        [FromQuery] Guid hotelId,
        [FromQuery] DateTime checkIn,
        [FromQuery] DateTime checkOut,
        [FromQuery] Guid? roomTypeId = null)
    {
        _accessGuard.EnsureCanAccessHotel(hotelId);
        var rooms = await _roomService.GetAvailableRoomsAsync(hotelId, checkIn, checkOut, roomTypeId);
        return Ok(rooms);
    }

    /// <summary>
    /// Crea una nueva habitación. Los usuarios no-admin solo pueden crear habitaciones en su propio hotel.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(RoomDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<RoomDto>> CreateRoom([FromBody] CreateRoomCommand command)
    {
        _accessGuard.EnsureCanAccessHotel(command.HotelId);
        var room = await _roomService.CreateRoomAsync(command);
        return CreatedAtAction(nameof(GetRoom), new { id = room.Id }, room);
    }

    /// <summary>
    /// Actualiza una habitación existente
    /// </summary>
    [HttpPut("{id}")]
    [ProducesResponseType(typeof(RoomDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<RoomDto>> UpdateRoom(Guid id, [FromBody] UpdateRoomCommand command)
    {
        if (id != command.Id)
        {
            return BadRequest("ID en la URL no coincide con el ID en el cuerpo");
        }

        var existing = await _roomService.GetRoomByIdAsync(id);
        if (existing == null)
        {
            return NotFound($"Habitación con ID {id} no encontrada");
        }
        _accessGuard.EnsureCanAccessHotel(existing.HotelId);

        var room = await _roomService.UpdateRoomAsync(command);
        return Ok(room);
    }

    /// <summary>
    /// Actualiza estado de una habitación
    /// </summary>
    [HttpPatch("{id}/status")]
    [ProducesResponseType(typeof(RoomDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<RoomDto>> UpdateRoomStatus(Guid id, [FromBody] UpdateRoomStatusCommand command)
    {
        var existing = await _roomService.GetRoomByIdAsync(id);
        if (existing == null)
        {
            return NotFound($"Habitación con ID {id} no encontrada");
        }
        _accessGuard.EnsureCanAccessHotel(existing.HotelId);

        var room = await _roomService.UpdateRoomStatusAsync(id, command);
        return Ok(room);
    }

    /// <summary>
    /// Marca habitación como sucia (necesita limpieza)
    /// </summary>
    [HttpPost("{id}/mark-dirty")]
    [ProducesResponseType(typeof(RoomDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<RoomDto>> MarkRoomAsDirty(Guid id, [FromBody] string? reason = null)
    {
        var existing = await _roomService.GetRoomByIdAsync(id);
        if (existing == null)
        {
            return NotFound($"Habitación con ID {id} no encontrada");
        }
        _accessGuard.EnsureCanAccessHotel(existing.HotelId);

        var room = await _roomService.MarkRoomAsDirtyAsync(id, reason);
        return Ok(room);
    }

    /// <summary>
    /// Marca habitación como limpia
    /// </summary>
    [HttpPost("{id}/mark-clean")]
    [ProducesResponseType(typeof(RoomDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<RoomDto>> MarkRoomAsClean(Guid id)
    {
        var existing = await _roomService.GetRoomByIdAsync(id);
        if (existing == null)
        {
            return NotFound($"Habitación con ID {id} no encontrada");
        }
        _accessGuard.EnsureCanAccessHotel(existing.HotelId);

        var room = await _roomService.MarkRoomAsCleanAsync(id);
        return Ok(room);
    }

    /// <summary>
    /// Solicita mantenimiento para una habitación
    /// </summary>
    [HttpPost("{id}/request-maintenance")]
    [ProducesResponseType(typeof(RoomDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<RoomDto>> RequestMaintenance(Guid id, [FromBody] RequestMaintenanceCommand command)
    {
        var existing = await _roomService.GetRoomByIdAsync(id);
        if (existing == null)
        {
            return NotFound($"Habitación con ID {id} no encontrada");
        }
        _accessGuard.EnsureCanAccessHotel(existing.HotelId);

        var room = await _roomService.RequestMaintenanceAsync(id, command);
        return Ok(room);
    }

    /// <summary>
    /// Completa mantenimiento de una habitación
    /// </summary>
    [HttpPost("{id}/complete-maintenance")]
    [ProducesResponseType(typeof(RoomDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<RoomDto>> CompleteMaintenance(Guid id)
    {
        var existing = await _roomService.GetRoomByIdAsync(id);
        if (existing == null)
        {
            return NotFound($"Habitación con ID {id} no encontrada");
        }
        _accessGuard.EnsureCanAccessHotel(existing.HotelId);

        var room = await _roomService.CompleteMaintenanceAsync(id);
        return Ok(room);
    }

    /// <summary>
    /// Obtiene historial de una habitación
    /// </summary>
    [HttpGet("{id}/history")]
    [Authorize(Roles = "Admin,Manager,Receptionist")]
    [ProducesResponseType(typeof(List<RoomHistoryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<List<RoomHistoryDto>>> GetRoomHistory(Guid id, [FromQuery] DateTime? from = null, [FromQuery] DateTime? to = null)
    {
        var existing = await _roomService.GetRoomByIdAsync(id);
        if (existing == null)
        {
            return NotFound($"Habitación con ID {id} no encontrada");
        }
        _accessGuard.EnsureCanAccessHotel(existing.HotelId);

        var history = await _roomService.GetRoomHistoryAsync(id, from, to);
        return Ok(history);
    }

    /// <summary>
    /// Obtiene estadísticas de habitaciones por hotel
    /// </summary>
    [HttpGet("stats/{hotelId}")]
    [ProducesResponseType(typeof(RoomStatsDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<RoomStatsDto>> GetRoomStats(Guid hotelId)
    {
        _accessGuard.EnsureCanAccessHotel(hotelId);
        var stats = await _roomService.GetRoomStatsAsync(hotelId);
        return Ok(stats);
    }

    /// <summary>
    /// Busca habitaciones por número o características
    /// </summary>
    [HttpGet("search")]
    [ProducesResponseType(typeof(List<RoomDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<RoomDto>>> SearchRooms(
        [FromQuery] Guid hotelId,
        [FromQuery] string? roomNumber,
        [FromQuery] string? status,
        [FromQuery] int? floor,
        [FromQuery] Guid? roomTypeId)
    {
        _accessGuard.EnsureCanAccessHotel(hotelId);
        var rooms = await _roomService.SearchRoomsAsync(hotelId, roomNumber, status, floor, roomTypeId);
        return Ok(rooms);
    }
}