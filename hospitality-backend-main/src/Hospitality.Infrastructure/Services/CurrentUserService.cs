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
    
    public bool IsInRole(string role) => Role == role;
    
    public bool HasPermission(string permission) =>
        _httpContextAccessor.HttpContext?.User?.Claims
            .Any(c => c.Type == "permission" && c.Value == permission) == true;
}