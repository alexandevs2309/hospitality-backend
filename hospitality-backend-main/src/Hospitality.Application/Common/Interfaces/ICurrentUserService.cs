namespace Hospitality.Application.Common.Interfaces;

public class DeviceInfo
{
    public string Name { get; set; } = string.Empty;
    public string? Fingerprint { get; set; }
    public string? UserAgent { get; set; }
}

public interface ICurrentUserService
{
    string? UserId { get; }
    string? Email { get; }
    Guid? SessionId { get; }
    string? IpAddress { get; }
    DeviceInfo DeviceInfo { get; }
    bool IsAuthenticated { get; }
    string? Role { get; }
    IReadOnlyList<string> Roles { get; }
    Guid? HotelId { get; }
    
    bool IsInRole(string role);
    bool HasPermission(string permission);
    IReadOnlyList<Guid> PropertyIds { get; }
}