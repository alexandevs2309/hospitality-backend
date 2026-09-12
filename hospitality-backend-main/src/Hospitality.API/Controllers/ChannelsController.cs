using Hospitality.Application.Channels.Commands;
using Hospitality.Application.ChannelManager.Commands;
using Hospitality.Application.ChannelManager.Adapters;
using Hospitality.Application.ChannelManager.Services;
using Hospitality.Application.Common.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Hospitality.API.Controllers;

[ApiController]
[Route("api/v1/channels")]
[Authorize]
public class ChannelsController : ControllerBase
{
    private readonly IChannelService _channelService;
    private readonly IChannelManagerService _channelManagerService;
    private readonly IHotelAccessGuard _accessGuard;

    public ChannelsController(IChannelService channelService, IChannelManagerService channelManagerService, IHotelAccessGuard accessGuard)
    {
        _channelService = channelService;
        _channelManagerService = channelManagerService;
        _accessGuard = accessGuard;
    }

    /// <summary>
    /// Lista los canales de venta de la propiedad.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<ChannelDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ChannelDto>>> GetChannels([FromQuery] Guid? hotelId)
    {
        var resolvedHotelId = _accessGuard.ResolveRequestedHotel(hotelId);
        if (!resolvedHotelId.HasValue)
        {
            return Ok(Array.Empty<ChannelDto>());
        }

        var channels = await _channelService.GetChannelsAsync(resolvedHotelId.Value);
        return Ok(channels);
    }

    /// <summary>
    /// Crea un canal de venta.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(ChannelDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ChannelDto>> CreateChannel([FromBody] UpsertChannelCommand command)
    {
        _accessGuard.EnsureCanAccessHotel(command.HotelId);
        if (!await _accessGuard.IsPropertyOperatorAsync(command.HotelId))
        {
            return Forbid();
        }

        try
        {
            var channel = await _channelService.CreateChannelAsync(command);
            return Ok(channel);
        }
        catch (Hospitality.Domain.Exceptions.ValidationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    /// <summary>
    /// Actualiza un canal de venta.
    /// </summary>
    [HttpPut("{id}")]
    [ProducesResponseType(typeof(ChannelDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<ChannelDto>> UpdateChannel(Guid id, [FromBody] UpsertChannelCommand command)
    {
        _accessGuard.EnsureCanAccessHotel(command.HotelId);
        if (!await _accessGuard.IsPropertyOperatorAsync(command.HotelId))
        {
            return Forbid();
        }

        var channel = await _channelService.UpdateChannelAsync(id, command);
        return Ok(channel);
    }

    /// <summary>
    /// Elimina un canal de venta.
    /// </summary>
    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> DeleteChannel(Guid id, [FromQuery] Guid hotelId)
    {
        _accessGuard.EnsureCanAccessHotel(hotelId);
        if (!await _accessGuard.IsPropertyOperatorAsync(hotelId))
        {
            return Forbid();
        }

        await _channelService.DeleteChannelAsync(id);
        return NoContent();
    }

    // ────────────────── Channel Manager ──────────────────

    /// <summary>
    /// Prueba la conexión con el canal (OTA).
    /// </summary>
    [HttpGet("{id}/test-connection")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    public async Task<IActionResult> TestConnection(Guid id)
    {
        var success = await _channelManagerService.TestConnectionAsync(id);
        return Ok(new { success });
    }

    /// <summary>
    /// Obtiene los mapeos de tipos de habitación desde el canal (fetch automático).
    /// </summary>
    [HttpGet("{id}/room-type-maps")]
    [ProducesResponseType(typeof(Dictionary<string, ChannelRoomTypeMap>), StatusCodes.Status200OK)]
    public async Task<ActionResult<Dictionary<string, ChannelRoomTypeMap>>> GetRoomTypeMaps(Guid id)
    {
        var maps = await _channelManagerService.FetchRoomTypeMapsAsync(id);
        return Ok(maps);
    }

    /// <summary>
    /// Crea mapeos automáticos desde los datos fetchados del canal.
    /// </summary>
    [HttpPost("{id}/create-mappings")]
    [ProducesResponseType(typeof(PushResultDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<PushResultDto>> CreateMappings(Guid id)
    {
        var result = await _channelManagerService.CreateMappingsAsync(id);
        return Ok(result);
    }

    /// <summary>
    /// Envía disponibilidad al canal para un rango de fechas.
    /// </summary>
    [HttpPost("{id}/push-availability")]
    [ProducesResponseType(typeof(PushResultDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<PushResultDto>> PushAvailability(Guid id, [FromBody] PushRangeCommand command)
    {
        var result = await _channelManagerService.PushAvailabilityAsync(id, command.From, command.To);
        return Ok(result);
    }

    /// <summary>
    /// Envía tarifas al canal para un rango de fechas.
    /// </summary>
    [HttpPost("{id}/push-rates")]
    [ProducesResponseType(typeof(PushResultDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<PushResultDto>> PushRates(Guid id, [FromBody] PushRangeCommand command)
    {
        var result = await _channelManagerService.PushRatesAsync(id, command.From, command.To);
        return Ok(result);
    }

    /// <summary>
    /// Obtiene reservas desde el canal (pull).
    /// </summary>
    [HttpGet("{id}/pull-bookings")]
    [ProducesResponseType(typeof(List<BookingPullDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<BookingPullDto>>> PullBookings(Guid id, [FromQuery] DateTime from, [FromQuery] DateTime to)
    {
        var bookings = await _channelManagerService.PullBookingsAsync(id, from, to);
        return Ok(bookings);
    }

    /// <summary>
    /// Lista mapeos configurados para el canal.
    /// </summary>
    [HttpGet("{id}/mappings")]
    [ProducesResponseType(typeof(List<ChannelMappingDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<ChannelMappingDto>>> GetMappings(Guid id)
    {
        var mappings = await _channelManagerService.GetMappingsAsync(id);
        return Ok(mappings);
    }

    /// <summary>
    /// Crea o actualiza un mapeo de tipo de habitación.
    /// </summary>
    [HttpPost("{id}/mappings")]
    [ProducesResponseType(typeof(ChannelMappingDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<ChannelMappingDto>> UpsertMapping(Guid id, [FromBody] UpsertChannelMappingCommand command)
    {
        command.ChannelId = id;
        var mapping = await _channelManagerService.UpsertMappingAsync(command);
        return Ok(mapping);
    }

    /// <summary>
    /// Elimina un mapeo.
    /// </summary>
    [HttpDelete("mappings/{mappingId}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> DeleteMapping(Guid mappingId)
    {
        await _channelManagerService.DeleteMappingAsync(mappingId);
        return NoContent();
    }

    /// <summary>
    /// Obtiene credenciales del canal.
    /// </summary>
    [HttpGet("{id}/credentials")]
    [ProducesResponseType(typeof(ChannelCredentialsDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<ChannelCredentialsDto>> GetCredentials(Guid id)
    {
        var creds = await _channelManagerService.GetCredentialsAsync(id);
        return Ok(creds);
    }

    /// <summary>
    /// Actualiza credenciales del canal.
    /// </summary>
    [HttpPut("{id}/credentials")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> UpdateCredentials(Guid id, [FromBody] ChannelCredentialsDto command)
    {
        command.ChannelId = id;
        await _channelManagerService.UpdateCredentialsAsync(command);
        return NoContent();
    }
}

public record PushRangeCommand(DateTime From, DateTime To);