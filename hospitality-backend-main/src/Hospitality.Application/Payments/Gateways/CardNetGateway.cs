namespace Hospitality.Application.Payments.Gateways;

public class CardNetGateway : IPaymentGateway
{
    public string GatewayName => "CardNet";
    public string DisplayName => "CardNet (República Dominicana)";

    public Task<PaymentGatewayResult> ChargeAsync(PaymentGatewayRequest request)
    {
        var nsu = "NSU" + Random.Shared.Next(1000000, 9999999).ToString();
        return Task.FromResult(new PaymentGatewayResult(
            true,
            $"Cargo aprobado por {DisplayName}. Monto {request.Amount:0.00} {request.Currency}.",
            nsu,
            "CARDNET-" + DateTime.UtcNow.ToString("yyyyMMddHHmmss"),
            GatewayName));
    }

    public Task<PaymentGatewayResult> RefundAsync(PaymentRefundRequest request)
    {
        return Task.FromResult(new PaymentGatewayResult(
            true,
            $"Reembolso de {request.Amount:0.00} procesado por {DisplayName}.",
            "NSU-REF" + Random.Shared.Next(1000000, 9999999).ToString(),
            "CARDNET-REF-" + request.Reference,
            GatewayName));
    }
}