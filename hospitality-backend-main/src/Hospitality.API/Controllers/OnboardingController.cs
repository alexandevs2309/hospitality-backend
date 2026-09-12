using Hospitality.Application.Common.Interfaces;
using Hospitality.Application.Onboarding.Commands;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Hospitality.API.Controllers;

[ApiController]
[Route("api/v1/onboarding")]
[Authorize]
public class OnboardingController : ControllerBase
{
    private readonly IOnboardingService _onboardingService;
    private readonly IHotelAccessGuard _accessGuard;

    public OnboardingController(IOnboardingService onboardingService, IHotelAccessGuard accessGuard)
    {
        _onboardingService = onboardingService;
        _accessGuard = accessGuard;
    }

    /// <summary>
    /// Estado del registro de la propiedad para el wizard de puesta en marcha.
    /// </summary>
    [HttpGet("status")]
    [ProducesResponseType(typeof(OnboardingStatusDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<OnboardingStatusDto>> GetStatus([FromQuery] Guid? hotelId)
    {
        var resolvedHotelId = _accessGuard.ResolveRequestedHotel(hotelId);
        if (!resolvedHotelId.HasValue)
        {
            return Ok(new OnboardingStatusDto());
        }

        try
        {
            var status = await _onboardingService.GetStatusAsync(resolvedHotelId.Value);
            return Ok(status);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }
}