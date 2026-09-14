namespace Hospitality.Application.Payments.Gateways;

public class AzulGateway : IPaymentGateway
{
    public string GatewayName => "Azul";
    public string DisplayName => "Azul (República Dominicana)";

    public Task<PaymentGatewayResult> ChargeAsync(PaymentGatewayRequest request)
    {
        var authorization = "AX" + Random.Shared.Next(100000, 999999).ToString();
        var reference = "AZUL-" + DateTime.UtcNow.ToString("yyyyMMddHHmmss");
        return Task.FromResult(new PaymentGatewayResult(
            true,
            $"Cargo aprobado por {DisplayName}. Monto {request.Amount:0.00} {request.Currency}.",
            authorization,
            reference,
            GatewayName));
    }

    public Task<PaymentGatewayResult> RefundAsync(PaymentRefundRequest request)
    {
        return Task.FromResult(new PaymentGatewayResult(
            true,
            $"Reembolso de {request.Amount:0.00} procesado por {DisplayName}.",
            "RF" + Random.Shared.Next(100000, 999999).ToString(),
            "AZUL-REF-" + request.Reference,
            GatewayName));
    }
}