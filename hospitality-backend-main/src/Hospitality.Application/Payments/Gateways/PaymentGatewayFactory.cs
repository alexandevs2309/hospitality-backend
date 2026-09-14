namespace Hospitality.Application.Payments.Gateways;

public class PaymentGatewayFactory
{
    private readonly Dictionary<string, IPaymentGateway> _gateways;

    public PaymentGatewayFactory(IEnumerable<IPaymentGateway> gateways)
    {
        _gateways = gateways.ToDictionary(g => g.GatewayName, g => g, StringComparer.OrdinalIgnoreCase);
    }

    public IPaymentGateway? GetGateway(string gatewayName)
    {
        if (string.IsNullOrWhiteSpace(gatewayName))
        {
            return null;
        }

        return _gateways.TryGetValue(gatewayName, out var gateway) ? gateway : null;
    }

    public IEnumerable<(string Name, string DisplayName)> GetAvailableGateways()
    {
        return _gateways.Values.Select(g => (g.GatewayName, g.DisplayName));
    }
}