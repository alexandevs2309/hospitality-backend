using Hospitality.Application.Payments.Gateways;

namespace Hospitality.Application.Payments.Commands;

public class GatewayInfoDto
{
    public string Name { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
}

public interface IPaymentGatewayService
{
    Task<PaymentGatewayResult> ChargeAsync(PaymentGatewayRequest request);
    Task<PaymentGatewayResult> RefundAsync(PaymentRefundRequest request);
    IEnumerable<GatewayInfoDto> GetAvailableGateways();
}