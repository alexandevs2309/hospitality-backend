namespace Hospitality.Application.Audit;

public class AuditLogDto
{
    public Guid Id { get; set; }
    public string? UserId { get; set; }
    public string? UserName { get; set; }
    public string Action { get; set; } = string.Empty;
    public string? Module { get; set; }
    public string? Entity { get; set; }
    public string? EntityId { get; set; }
    public string? Details { get; set; }
    public string? IpAddress { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class AuditLogPage
{
    public IReadOnlyList<AuditLogDto> Items { get; set; } = Array.Empty<AuditLogDto>();
    public int Total { get; set; }
}

public interface IAuditService
{
    Task LogAsync(
        string action,
        string? module = null,
        string? entity = null,
        string? entityId = null,
        string? details = null,
        string? userName = null,
        string? userId = null,
        CancellationToken cancellationToken = default);

    Task<AuditLogPage> GetLogsAsync(
        string? action = null,
        string? search = null,
        DateTime? from = null,
        DateTime? to = null,
        int pageNumber = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default);
}