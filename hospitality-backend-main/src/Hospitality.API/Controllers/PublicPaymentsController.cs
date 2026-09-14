using Hospitality.Application.Payments.Commands;
using Hospitality.Application.Payments.Gateways;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;

namespace Hospitality.API.Controllers;

[ApiController]
[Route("api/v1/public/payments")]
[AllowAnonymous]
[EnableCors("PublicWidget")]
public class PublicPaymentsController : ControllerBase
{
    private readonly IPaymentGatewayService _paymentGatewayService;

    public PublicPaymentsController(IPaymentGatewayService paymentGatewayService)
    {
        _paymentGatewayService = paymentGatewayService;
    }

    /// <summary>
    /// Lista las pasarelas de pago disponibles (Azul, CardNet).
    /// </summary>
    [HttpGet("gateways")]
    [ProducesResponseType(typeof(IEnumerable<GatewayInfoDto>), StatusCodes.Status200OK)]
    public ActionResult<IEnumerable<GatewayInfoDto>> GetGateways()
    {
        return Ok(_paymentGatewayService.GetAvailableGateways());
    }

    /// <summary>
    /// Carga de prueba a través de una pasarela local (Azul/CardNet) — mock de integración.
    /// </summary>
    [HttpPost("charge")]
    [ProducesResponseType(typeof(PaymentGatewayResult), StatusCodes.Status200OK)]
    public async Task<ActionResult<PaymentGatewayResult>> Charge([FromBody] PaymentGatewayRequest request)
    {
        return Ok(await _paymentGatewayService.ChargeAsync(request));
    }

    /// <summary>
    /// Reembolso de prueba a través de una pasarela local — mock de integración.
    /// </summary>
    [HttpPost("refund")]
    [ProducesResponseType(typeof(PaymentGatewayResult), StatusCodes.Status200OK)]
    public async Task<ActionResult<PaymentGatewayResult>> Refund([FromBody] PaymentRefundRequest request)
    {
        return Ok(await _paymentGatewayService.RefundAsync(request));
    }
}