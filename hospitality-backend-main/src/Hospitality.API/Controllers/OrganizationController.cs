using Hospitality.Application.Common.Interfaces;
using Hospitality.Application.Organizations.Commands;
using Hospitality.Domain.Exceptions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Hospitality.API.Controllers;

[ApiController]
[Route("api/v1/organization")]
[Authorize]
public class OrganizationController : ControllerBase
{
    private readonly IOrganizationService _organizationService;
    private readonly IHotelAccessGuard _accessGuard;

    public OrganizationController(
        IOrganizationService organizationService,
        IHotelAccessGuard accessGuard)
    {
        _organizationService = organizationService;
        _accessGuard = accessGuard;
    }

    /// <summary>
    /// Resumen de la organización del usuario (plan, propiedades y mi rol).
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(OrganizationSummaryDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<OrganizationSummaryDto>> GetMyOrganization()
    {
        try
        {
            return Ok(await _organizationService.GetMyOrganizationAsync());
        }
        catch (KeyNotFoundException)
        {
            return NotFound("El usuario no pertenece a ninguna organización.");
        }
    }

    /// <summary>
    /// Propiedades asignadas al usuario (para el selector de propiedad).
    /// </summary>
    [HttpGet("properties")]
    [ProducesResponseType(typeof(List<UserPropertyDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<UserPropertyDto>>> GetMyProperties()
    {
        return Ok(await _organizationService.GetMyPropertiesAsync());
    }

    /// <summary>
    /// Miembros de la organización y sus asignaciones de propiedad.
    /// </summary>
    [HttpGet("members")]
    [ProducesResponseType(typeof(List<OrganizationMemberDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<OrganizationMemberDto>>> GetMembers()
    {
        if (!await _accessGuard.IsOrganizationAdminAsync())
        {
            return Forbid();
        }
        return Ok(await _organizationService.GetMembersAsync());
    }

    /// <summary>
    /// Invita un miembro: crea la cuenta, el rol y las asignaciones de propiedad.
    /// Devuelve la contraseña temporal UNA sola vez.
    /// </summary>
    [HttpPost("members")]
    [ProducesResponseType(typeof(InviteMemberResultDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<InviteMemberResultDto>> InviteMember([FromBody] InviteMemberCommand command)
    {
        if (!await _accessGuard.IsOrganizationAdminAsync())
        {
            return Forbid();
        }

        try
        {
            return Ok(await _organizationService.InviteMemberAsync(command));
        }
        catch (ValidationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (ConflictException ex)
        {
            return Conflict(ex.Message);
        }
        catch (KeyNotFoundException)
        {
            return NotFound("Solo un miembro de una organización puede invitar.");
        }
    }

    /// <summary>
    /// Actualiza rol, estado y asignaciones de propiedad de un miembro.
    /// </summary>
    [HttpPut("members/{userId}")]
    [ProducesResponseType(typeof(OrganizationMemberDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<OrganizationMemberDto>> UpdateMember(string userId, [FromBody] UpdateMemberCommand command)
    {
        if (!await _accessGuard.IsOrganizationAdminAsync())
        {
            return Forbid();
        }

        try
        {
            return Ok(await _organizationService.UpdateMemberAsync(userId, command));
        }
        catch (ValidationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (KeyNotFoundException)
        {
            return NotFound("El miembro no fue encontrado.");
        }
    }

    /// <summary>
    /// Elimina (desactiva) un miembro de la organización.
    /// </summary>
    [HttpDelete("members/{userId}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> RemoveMember(string userId)
    {
        if (!await _accessGuard.IsOrganizationAdminAsync())
        {
            return Forbid();
        }

        await _organizationService.RemoveMemberAsync(userId);
        return NoContent();
    }
}