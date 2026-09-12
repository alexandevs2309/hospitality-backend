using System.Security.Claims;
using Hospitality.Application.Common.Interfaces;
using Microsoft.AspNetCore.Http;

namespace Hospitality.Infrastructure.Services;

public class CurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUserService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public string? UserId => _httpContextAccessor.HttpContext?.User?.FindFirstValue(ClaimTypes.NameIdentifier);
    
    public string? Email => _httpContextAccessor.HttpContext?.User?.FindFirstValue(ClaimTypes.Email);

    public Guid? SessionId
    {
        get
        {
            var sid = _httpContextAccessor.HttpContext?.User?.FindFirstValue("sid");
            return Guid.TryParse(sid, out var sessionId) ? sessionId : null;
        }
    }

    public string? IpAddress => _httpContextAccessor.HttpContext?.Connection.RemoteIpAddress?.ToString();

    public DeviceInfo DeviceInfo
    {
        get
        {
            var http = _httpContextAccessor.HttpContext;
            var header = http?.Request.Headers["X-Device-Info"].FirstOrDefault();
            var deviceName = string.Empty;
            string? fingerprint = null;
            string? userAgent = null;

            if (!string.IsNullOrWhiteSpace(header))
            {
                try
                {
                    var parsed = System.Text.Json.JsonSerializer.Deserialize<DeviceInfo>(header,
                        new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    if (parsed is not null)
                    {
                        deviceName = parsed.Name ?? string.Empty;
                        fingerprint = string.IsNullOrWhiteSpace(parsed.Fingerprint) ? null : parsed.Fingerprint;
                        userAgent = parsed.UserAgent;
                    }
                }
                catch
                {
                    // Encabezado inválido: se ignora.
                }
            }

            userAgent ??= http?.Request.Headers["User-Agent"].FirstOrDefault();
            if (string.IsNullOrWhiteSpace(deviceName))
            {
                deviceName = string.IsNullOrWhiteSpace(userAgent)
                    ? "Navegador"
                    : userAgent.Split(['(', ')']).Skip(1).FirstOrDefault()?.Trim() ?? "Navegador";
            }

            return new DeviceInfo { Name = deviceName, Fingerprint = fingerprint, UserAgent = userAgent };
        }
    }

    public bool IsAuthenticated => UserId != null;
    
    public string? Role => _httpContextAccessor.HttpContext?.User?.FindFirstValue(ClaimTypes.Role);

    public IReadOnlyList<string> Roles => _httpContextAccessor.HttpContext?.User?
        .FindAll(ClaimTypes.Role)
        .Select(c => c.Value)
        .Distinct()
        .ToArray() ?? Array.Empty<string>();
    
    public Guid? HotelId
    {
        get
        {
            var hotelIdString = _httpContextAccessor.HttpContext?.User?.FindFirstValue("hotel_id");
            if (Guid.TryParse(hotelIdString, out var hotelId))
                return hotelId;
            return null;
        }
    }

    public IReadOnlyList<Guid> PropertyIds
    {
        get
        {
            var raw = _httpContextAccessor.HttpContext?.User?.FindFirstValue("property_ids");
            if (string.IsNullOrWhiteSpace(raw)) return Array.Empty<Guid>();
            return raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(s => Guid.TryParse(s, out var g) ? g : Guid.Empty)
                .Where(g => g != Guid.Empty)
                .Distinct()
                .ToArray();
        }
    }
    
    public bool IsInRole(string role) => Role == role;
    
    public bool HasPermission(string permission) =>
        _httpContextAccessor.HttpContext?.User?.Claims
            .Any(c => c.Type == "permission" && c.Value == permission) == true;
}