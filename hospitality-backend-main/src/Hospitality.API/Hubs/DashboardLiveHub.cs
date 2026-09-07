using Hospitality.Application.Common.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Hospitality.API.Hubs;

/// <summary>
/// Hub de tiempo real del dashboard. El cliente autenticado se une al grupo de
/// su hotel (o hoteles) y recibe actualizaciones de métricas vía el broadcaster.
/// El token se envía por query string (?access_token=...) y se valida en JwtBearer.
/// </summary>
[Authorize]
public class DashboardLiveHub : Hub
{
    private readonly IHotelAccessGuard _accessGuard;
    private readonly HotelSubscriptionRegistry _subscriptions;

    public DashboardLiveHub(IHotelAccessGuard accessGuard, HotelSubscriptionRegistry subscriptions)
    {
        _accessGuard = accessGuard;
        _subscriptions = subscriptions;
    }

    public static string GroupFor(Guid hotelId) => $"hotel-{hotelId}";

    public async Task JoinHotel(Guid hotelId)
    {
        _accessGuard.EnsureCanAccessHotel(hotelId);
        _subscriptions.Subscribe(hotelId);
        await Groups.AddToGroupAsync(Context.ConnectionId, GroupFor(hotelId));
    }

    public async Task LeaveHotel(Guid hotelId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, GroupFor(hotelId));
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        await base.OnDisconnectedAsync(exception);
    }
}