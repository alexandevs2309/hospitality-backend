namespace Hospitality.Application.Payments.Gateways;

public record PaymentGatewayRequest(
    string Gateway,
    decimal Amount,
    string Currency,
    string CardToken,
    string? Description);

public record PaymentGatewayResult(
    bool Success,
    string Message,
    string? AuthorizationCode,
    string? Reference,
    string Gateway);

public record PaymentRefundRequest(string Gateway, decimal Amount, string Reference, string? Reason);

public interface IPaymentGateway
{
    string GatewayName { get; }
    string DisplayName { get; }
    Task<PaymentGatewayResult> ChargeAsync(PaymentGatewayRequest request);
    Task<PaymentGatewayResult> RefundAsync(PaymentRefundRequest request);
}