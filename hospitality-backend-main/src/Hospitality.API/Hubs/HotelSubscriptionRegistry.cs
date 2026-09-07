using System.Collections.Concurrent;

namespace Hospitality.API.Hubs;

/// <summary>
/// Registro de hoteles con suscriptores conectados al hub de dashboard.
/// Permite al broadcaster solo recalcular métricas para hoteles con audiencia.
/// (Un elemento por hotel; el crecimiento es acotado al número de hoteles del negocio.)
/// </summary>
public class HotelSubscriptionRegistry
{
    private readonly ConcurrentDictionary<Guid, byte> _hotels = new();

    public void Subscribe(Guid hotelId) => _hotels.TryAdd(hotelId, 0);

    public IReadOnlyList<Guid> Snapshot() => _hotels.Keys.ToList();
}