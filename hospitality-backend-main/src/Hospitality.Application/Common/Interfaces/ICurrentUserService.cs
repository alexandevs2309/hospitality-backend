namespace Hospitality.Application.Common.Interfaces;

public interface ICurrentUserService
{
    string? UserId { get; }
    string? Email { get; }
    bool IsAuthenticated { get; }
    string? Role { get; }
    IReadOnlyList<string> Roles { get; }
    Guid? HotelId { get; }
    
    bool IsInRole(string role);
    bool HasPermission(string permission);
}