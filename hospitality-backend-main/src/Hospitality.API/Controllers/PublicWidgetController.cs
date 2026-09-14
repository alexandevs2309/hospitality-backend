using Hospitality.Application.PublicWidget.Commands;
using Hospitality.Application.PublicWidget.Services;
using Hospitality.Domain.Exceptions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;

namespace Hospitality.API.Controllers;

[ApiController]
[Route("api/v1/public/widget")]
[AllowAnonymous]
[EnableCors("PublicWidget")]
public class PublicWidgetController : ControllerBase
{
    private readonly IWidgetService _widgetService;

    public PublicWidgetController(IWidgetService widgetService)
    {
        _widgetService = widgetService;
    }

    /// <summary>
    /// Configuración pública del hotel para el motor de reservas embebible.
    /// </summary>
    [HttpGet("config")]
    [ProducesResponseType(typeof(WidgetConfigDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<WidgetConfigDto>> GetConfig([FromQuery] Guid hotelId)
    {
        return Ok(await _widgetService.GetConfigAsync(hotelId));
    }

    /// <summary>
    /// Disponibilidad por categoría y noche para el motor de reservas embebible.
    /// </summary>
    [HttpGet("availability")]
    [ProducesResponseType(typeof(WidgetAvailabilityDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<WidgetAvailabilityDto>> GetAvailability(
        [FromQuery] Guid hotelId,
        [FromQuery] DateTime from,
        [FromQuery] DateTime to)
    {
return Ok(await _widgetService.GetAvailabilityAsync(hotelId, from, to));
    }

    /// <summary>
    /// Crea una reserva pública (booking engine embebible).
    /// </summary>
    [HttpPost("bookings")]
    [ProducesResponseType(typeof(PublicBookingResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PublicBookingResponse>> CreateBooking([FromBody] PublicBookingRequest request)
    {
        try
        {
            return Ok(await _widgetService.CreatePublicBookingAsync(request));
        }
        catch (ValidationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ex.Message);
        }
    }

    /// <summary>
    /// Script JS embebible. Uso: <script src="{host}/api/v1/public/widget/script?hotelId=..."></script>
    /// </summary>
    [HttpGet("script")]
    public IActionResult GetScript([FromQuery] Guid hotelId)
    {
        var script = BuildScript(hotelId);
        return Content(script, "text/javascript; charset=utf-8");
    }

    private static string BuildScript(Guid hotelId)
    {
        return $$"""
            (function () {
              'use strict';
              var apiBase = (window.AURON_WIDGET_API_BASE || new URL(document.currentScript && document.currentScript.src || location.href).origin).replace(/\/$/, '');
              var hotelId = '{{hotelId}}';
              var mount = document.querySelector('[data-auron-widget]');
              var payUrl = apiBase + '/api/v1/public/payments/charge';
              var gatewayName = 'Azul';

              function fmt(n) { return Number(n).toLocaleString('en-US', { minimumFractionDigits: 2, maximumFractionDigits: 2 }); }
              function iso(d) { return d.toISOString().slice(0, 10); }
              function addDays(d, n) { var x = new Date(d); x.setDate(x.getDate() + n); return x; }
              function esc(s) { return String(s == null ? '' : s).replace(/[&<>"']/g, function (c) { return { '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]; }); }

              function render(html) { mount.innerHTML = html; }
              function error(msg) {
                render('<div style="font-family:sans-serif;color:#b91c1c;padding:12px;border:1px solid #fecaca;border-radius:8px;background:#fef2f2">' + esc(msg) + '</div>');
              }

              function url(path, params) {
                var q = Object.keys(params).map(function (k) { return encodeURIComponent(k) + '=' + encodeURIComponent(params[k]); }).join('&');
                return apiBase + path + '?' + q;
              }

              function customers(currency, rooms) {
                var cards = rooms.map(function (rt) {
                  return '<div style="flex:1 1 220px;border:1px solid #e2e8f0;border-radius:10px;overflow:hidden;background:#fff">' +
                    '<div style="padding:12px 14px"><div style="font-weight:600;">' + esc(rt.name) + '</div>' +
                    '<div style="color:#64748b;font-size:13px;">' + esc(rt.description || '') + ' · Hasta ' + rt.capacity + ' huéspedes</div>' +
                    '<div style="margin-top:8px;font-size:20px;color:#0f172a">' + fmt(rt.pricePerNight) + ' <small style="font-size:12px;color:#64748b">' + esc(currency) + '/noche</small></div>' +
                    '<div style="margin-top:8px;color:#059669;font-size:13px;">' + (rt.availableNights > 0 ? rt.availableNights + ' noches disponibles' : 'Sin disponibilidad') + '</div>' +
                    '<button type="button" data-reserve="' + rt.roomTypeId + '" title="' + esc(rt.name) + '" style="margin-top:12px;width:100%;padding:10px;border:0;border-radius:8px;background:#059669;color:#fff;font-size:14px;cursor:pointer" ' + (rt.availableNights > 0 ? '' : 'disabled style="display:none"') + '>Reservar</button>' +
                    '</div></div>';
                }).join('');

                return '<div style="font-family:sans-serif;display:flex;flex-wrap:wrap;gap:12px">' + cards + '</div>';
              }

              function bindDates() {
                var fromEl = mount.querySelector('[data-a-widget-from]');
                var toEl = mount.querySelector('[data-a-widget-to]');
                var body = mount.querySelector('[data-a-widget-body]');
                if (!fromEl || !toEl || !body) return;

                function refresh() {
                  var from = fromEl.value, to = toEl.value;
                  if (!from || !to) return;
                  fetch(url('/api/v1/public/widget/availability', { hotelId: hotelId, from: from, to: to }))
                    .then(function (r) { return r.json(); })
                    .then(function (av) {
                      if (body) body.innerHTML = customers(av.currency, av.roomTypes);
                      bindReserve(av);
                    })
                    .catch(function () { error('No se pudo consultar disponibilidad'); });
                }

                fromEl.addEventListener('change', refresh);
                toEl.addEventListener('change', refresh);
              }

              function bindReserve(av) {
                mount.querySelectorAll('[data-reserve]').forEach(function (btn) {
                  btn.addEventListener('click', function () {
                    var rt = av.roomTypes.find(function (x) { return x.roomTypeId === btn.getAttribute('data-reserve'); });
                    if (!rt) return;
                    var nights = Math.max(0, Math.round((new Date(av.to) - new Date(av.from)) / 86400000));
                    var total = rt.pricePerNight * nights;
                    if (!confirm('Reservar ' + rt.name + '\n' + av.from.slice(0, 10) + ' → ' + av.to.slice(0, 10) + ' (' + nights + ' noches)\nTotal: ' + fmt(total) + ' ' + av.currency + '\n\nProcesar pago de prueba por ' + gatewayName + '?')) return;
                    fetch(payUrl, {
                      method: 'POST',
                      headers: { 'Content-Type': 'application/json' },
                      body: JSON.stringify({ gateway: gatewayName, amount: total, currency: av.currency, cardToken: 'tok-mock-' + apiBase, description: rt.name })
                    }).then(function (r) { return r.json(); })
                      .then(function (res) {
                        alert(res.success ? ('Pago aprobado\nAutorización: ' + res.authorizationCode + '\nRef: ' + res.reference) : ('Pago rechazado: ' + res.message));
                      });
                  });
                });
              }

              function loadConfig() {
                fetch(url('/api/v1/public/widget/config', { hotelId: hotelId }))
                  .then(function (r) { return r.json(); })
                  .then(function (config) {
                    if (!config.roomTypes || !config.roomTypes.length) { error('Este hotel aún no publica disponibilidad.'); return; }
                    var today = new Date();
                    var from = addDays(today, 1);
                    var to = addDays(from, 7);
                    render(
                      '<div style="font-family:sans-serif;border:1px solid #e2e8f0;border-radius:12px;padding:16px;background:#f8fafc">' +
                        '<div style="display:flex;justify-content:space-between;align-items:center;margin-bottom:12px;flex-wrap:wrap;gap:8px">' +
                          '<div style="font-size:17px;font-weight:700;color:#0f172a">' + esc(config.hotelName) + '</div>' +
                          '<div style="display:flex;gap:8px;align-items:center">' +
                            '<label style="font-size:13px;color:#475569">Entrada <input type="date" data-a-widget-from value="' + iso(from) + '" style="padding:6px;border:1px solid #cbd5e1;border-radius:6px"></label>' +
                            '<label style="font-size:13px;color:#475569">Salida <input type="date" data-a-widget-to value="' + iso(to) + '" style="padding:6px;border:1px solid #cbd5e1;border-radius:6px"></label>' +
                          '</div>' +
                        '</div>' +
                        '<div data-a-widget-body></div>' +
                      '</div>'
                    );
                    bindDates();
                    var ev = new Event('change');
                    mount.querySelector('[data-a-widget-from]').dispatchEvent(ev);
                  })
                  .catch(function () { error('No se pudo cargar el motor de reservas'); });
              }

              if (!mount) {
                if (window.console) console.warn('Auron widget: falta el contenedor [data-auron-widget]');
                return;
              }
              loadConfig();
            })();
            """;
    }
}