using Hospitality.Application.Common.DTOs;
using Hospitality.Application.Common.Interfaces;
using Hospitality.Application.Hotels.Queries;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Hospitality.API.Controllers;

[ApiController]
[Route("api/v1/dashboard")]
[Authorize]
public class DashboardController : ControllerBase
{
    private readonly IDashboardService _dashboardService;
    private readonly IHotelAccessGuard _accessGuard;
    private const int MaxLimit = 100;

    public DashboardController(IDashboardService dashboardService, IHotelAccessGuard accessGuard)
    {
        _dashboardService = dashboardService;
        _accessGuard = accessGuard;
    }

    /// <summary>
    /// Obtiene métricas en tiempo real del hotel activo
    /// </summary>
    /// <param name="hotelId">ID del hotel (opcional, usa el del usuario por defecto)</param>
    /// <returns>Métricas del hotel</returns>
    [HttpGet("metrics")]
    [ProducesResponseType(typeof(Dictionary<string, decimal>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<Dictionary<string, decimal>>> GetHotelMetrics(Guid? hotelId = null)
    {
        var resolved = _accessGuard.ResolveRequestedHotel(hotelId);
        var metrics = await _dashboardService.GetHotelMetricsAsync(resolved);
        return Ok(metrics);
    }

    /// <summary>
    /// Obtiene métricas (misma fuente que /metrics).
    /// Preparado para integrar streaming en tiempo real (SignalR/SSE) en el futuro.
    /// </summary>
    /// <param name="hotelId">ID del hotel</param>
    /// <returns>Métricas del hotel</returns>
    [HttpGet("metrics/live")]
    [ProducesResponseType(typeof(Dictionary<string, decimal>), StatusCodes.Status200OK)]
    public async Task<ActionResult<Dictionary<string, decimal>>> GetLiveHotelMetrics(Guid? hotelId = null)
    {
        var resolved = _accessGuard.ResolveRequestedHotel(hotelId);
        var metrics = await _dashboardService.GetLiveHotelMetricsAsync(resolved);
        return Ok(metrics);
    }

    /// <summary>
    /// Obtiene las reservaciones del día para el dashboard
    /// </summary>
    /// <param name="hotelId">ID del hotel</param>
    /// <param name="limit">Límite de resultados (default: 10, máx: 100)</param>
    /// <returns>Lista de reservaciones del día</returns>
    [HttpGet("bookings/today")]
    [ProducesResponseType(typeof(List<BookingRowDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<BookingRowDto>>> GetTodayBookings(
        Guid? hotelId = null,
        [FromQuery] int limit = 10)
    {
        var resolved = _accessGuard.ResolveRequestedHotel(hotelId);
        var bookings = await _dashboardService.GetTodayBookingsAsync(resolved, Math.Clamp(limit, 1, MaxLimit));
        return Ok(bookings);
    }

    /// <summary>
    /// Obtiene el estado de housekeeping para el dashboard
    /// </summary>
    /// <param name="hotelId">ID del hotel</param>
    /// <returns>Estado de housekeeping</returns>
    [HttpGet("housekeeping/status")]
    [ProducesResponseType(typeof(HousekeepingStatusDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<HousekeepingStatusDto>> GetHousekeepingStatus(Guid? hotelId = null)
    {
        var resolved = _accessGuard.ResolveRequestedHotel(hotelId);
        var status = await _dashboardService.GetHousekeepingStatusAsync(resolved);
        return Ok(status);
    }

    /// <summary>
    /// Obtiene tickets de mantenimiento abiertos para el dashboard
    /// </summary>
    /// <param name="hotelId">ID del hotel</param>
    /// <param name="status">Estado del ticket (opcional)</param>
    /// <param name="limit">Límite de resultados (default: 10, máx: 100)</param>
    /// <returns>Lista de tickets de mantenimiento</returns>
    [HttpGet("maintenance/tickets")]
    [ProducesResponseType(typeof(List<MaintenanceTicketDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<MaintenanceTicketDto>>> GetMaintenanceTickets(
        Guid? hotelId = null,
        [FromQuery] string? status = "open",
        [FromQuery] int limit = 10)
    {
        var resolved = _accessGuard.ResolveRequestedHotel(hotelId);
        var tickets = await _dashboardService.GetMaintenanceTicketsAsync(resolved, status, Math.Clamp(limit, 1, MaxLimit));
        return Ok(tickets);
    }

    /// <summary>
    /// Obtiene tendencia de ocupación semanal para gráficos
    /// </summary>
    /// <param name="hotelId">ID del hotel</param>
    /// <param name="period">Período: week, month, year (default: week)</param>
    /// <returns>Tendencia de ocupación</returns>
    [HttpGet("analytics/occupancy")]
    [ProducesResponseType(typeof(List<ChartPointDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<ChartPointDto>>> GetOccupancyTrend(
        Guid? hotelId = null,
        [FromQuery] string period = "week")
    {
        var resolved = _accessGuard.ResolveRequestedHotel(hotelId);
        var trend = await _dashboardService.GetOccupancyTrendAsync(resolved, period);
        return Ok(trend);
    }

    /// <summary>
    /// Obtiene tendencia de ingresos para gráficos
    /// </summary>
    /// <param name="hotelId">ID del hotel</param>
    /// <param name="period">Período: week, month, year (default: year)</param>
    /// <returns>Tendencia de ingresos</returns>
    [HttpGet("analytics/revenue")]
    [ProducesResponseType(typeof(List<ChartPointDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<ChartPointDto>>> GetRevenueTrend(
        Guid? hotelId = null,
        [FromQuery] string period = "year")
    {
        var resolved = _accessGuard.ResolveRequestedHotel(hotelId);
        var trend = await _dashboardService.GetRevenueTrendAsync(resolved, period);
        return Ok(trend);
    }

    /// <summary>
    /// Obtiene KPIs del dashboard
    /// </summary>
    /// <param name="hotelId">ID del hotel</param>
    /// <returns>KPIs principales</returns>
    [HttpGet("kpis")]
    [ProducesResponseType(typeof(Dictionary<string, decimal>), StatusCodes.Status200OK)]
    public async Task<ActionResult<Dictionary<string, decimal>>> GetDashboardKpis(Guid? hotelId = null)
    {
        var resolved = _accessGuard.ResolveRequestedHotel(hotelId);
        var kpis = await _dashboardService.GetDashboardKpisAsync(resolved);
        return Ok(kpis);
    }

    /// <summary>
    /// Obtiene datos para widgets del dashboard
    /// </summary>
    /// <param name="hotelId">ID del hotel</param>
    /// <returns>Datos consolidados para widgets</returns>
    [HttpGet("widgets")]
    [ProducesResponseType(typeof(DashboardWidgetsDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<DashboardWidgetsDto>> GetDashboardWidgets(Guid? hotelId = null)
    {
        var resolved = _accessGuard.ResolveRequestedHotel(hotelId);
        var widgets = await _dashboardService.GetDashboardWidgetsAsync(resolved);
        return Ok(widgets);
    }
}