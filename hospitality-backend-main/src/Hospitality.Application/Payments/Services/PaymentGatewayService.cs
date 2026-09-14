using Hospitality.Application.Payments.Commands;
using Hospitality.Application.Payments.Gateways;

namespace Hospitality.Application.Payments.Services;

public class PaymentGatewayService : IPaymentGatewayService
{
    private readonly PaymentGatewayFactory _factory;

    public PaymentGatewayService(PaymentGatewayFactory factory)
    {
        _factory = factory;
    }

    public Task<PaymentGatewayResult> ChargeAsync(PaymentGatewayRequest request)
    {
        var gateway = _factory.GetGateway(request.Gateway);
        if (gateway == null)
        {
            var available = string.Join(", ", _factory.GetAvailableGateways().Select(g => g.Name));
            return Task.FromResult(new PaymentGatewayResult(
                false,
                $"Pasarela no soportada. Disponibles: {available}.",
                null,
                null,
                request.Gateway));
        }

        if (request.Amount <= 0)
        {
            return Task.FromResult(new PaymentGatewayResult(
                false,
                "El monto debe ser mayor que cero.",
                null,
                null,
                request.Gateway));
        }

        return gateway.ChargeAsync(request);
    }

    public Task<PaymentGatewayResult> RefundAsync(PaymentRefundRequest request)
    {
        var gateway = _factory.GetGateway(request.Gateway);
        if (gateway == null)
        {
            var available = string.Join(", ", _factory.GetAvailableGateways().Select(g => g.Name));
            return Task.FromResult(new PaymentGatewayResult(
                false,
                $"Pasarela no soportada. Disponibles: {available}.",
                null,
                null,
                request.Gateway));
        }

        return gateway.RefundAsync(request);
    }

    public IEnumerable<GatewayInfoDto> GetAvailableGateways()
    {
        return _factory.GetAvailableGateways().Select(g => new GatewayInfoDto
        {
            Name = g.Name,
            DisplayName = g.DisplayName
        });
    }
}