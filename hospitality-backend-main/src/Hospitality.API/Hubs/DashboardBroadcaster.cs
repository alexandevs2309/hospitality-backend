using Hospitality.Application.Hotels.Queries;
using Microsoft.AspNetCore.SignalR;

namespace Hospitality.API.Hubs;

/// <summary>
/// Emite las métricas del dashboard a los clientes conectados al hub cada 5 segundos.
/// Esto convierte el "live" del dashboard en un flujo real (push) en lugar de un
/// recálculo on-demand de /metrics/live.
/// </summary>
public class DashboardBroadcaster : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(5);

    private readonly IHubContext<DashboardLiveHub> _hub;
    private readonly HotelSubscriptionRegistry _subscriptions;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<DashboardBroadcaster> _logger;

    public DashboardBroadcaster(
        IHubContext<DashboardLiveHub> hub,
        HotelSubscriptionRegistry subscriptions,
        IServiceScopeFactory scopeFactory,
        ILogger<DashboardBroadcaster> logger)
    {
        _hub = hub;
        _subscriptions = subscriptions;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            foreach (var hotelId in _subscriptions.Snapshot())
            {
                try
                {
                    using var scope = _scopeFactory.CreateScope();
                    var dashboard = scope.ServiceProvider.GetRequiredService<IDashboardService>();
                    var metrics = await dashboard.GetHotelMetricsAsync(hotelId);
                    await _hub.Clients
                        .Group(DashboardLiveHub.GroupFor(hotelId))
                        .SendAsync("metrics", hotelId, metrics, stoppingToken);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _logger.LogWarning(ex, "No se pudo transmitir métricas del hotel {HotelId}.", hotelId);
                }
            }
        }
    }
}