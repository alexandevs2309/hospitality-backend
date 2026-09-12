using System.Security.Claims;
using Hospitality.Application.Audit;
using Hospitality.Domain.Entities;
using Hospitality.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Hospitality.Infrastructure.Services;

public class AuditService : IAuditService
{
    private readonly ApplicationDbContext _context;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public AuditService(ApplicationDbContext context, IHttpContextAccessor httpContextAccessor)
    {
        _context = context;
        _httpContextAccessor = httpContextAccessor;
    }

    public async Task LogAsync(
        string action,
        string? module = null,
        string? entity = null,
        string? entityId = null,
        string? details = null,
        string? userName = null,
        string? userId = null,
        CancellationToken cancellationToken = default)
    {
        var http = _httpContextAccessor.HttpContext;
        var principal = http?.User;

        var entry = new AuditLog
        {
            Action = action,
            Module = module,
            Entity = entity,
            EntityId = entityId,
            Details = details,
            UserId = userId ?? principal?.FindFirstValue(ClaimTypes.NameIdentifier),
            UserName = userName ?? principal?.FindFirstValue(ClaimTypes.Email),
            IpAddress = http?.Connection.RemoteIpAddress?.ToString(),
            CreatedAt = DateTime.UtcNow
        };

        _context.AuditLogs.Add(entry);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<AuditLogPage> GetLogsAsync(
        string? action = null,
        string? search = null,
        DateTime? from = null,
        DateTime? to = null,
        int pageNumber = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var query = _context.AuditLogs.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(action))
        {
            query = query.Where(e => e.Action == action);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(e =>
                (e.UserName != null && e.UserName.Contains(search)) ||
                (e.Entity != null && e.Entity.Contains(search)) ||
                (e.Details != null && e.Details.Contains(search)));
        }

        var fromUtc = from.HasValue ? DateTime.SpecifyKind(from.Value, DateTimeKind.Utc) : (DateTime?)null;
        var toUtc = to.HasValue ? DateTime.SpecifyKind(to.Value, DateTimeKind.Utc) : (DateTime?)null;

        if (fromUtc.HasValue)
        {
            query = query.Where(e => e.CreatedAt >= fromUtc.Value);
        }
        if (toUtc.HasValue)
        {
            query = query.Where(e => e.CreatedAt <= toUtc.Value);
        }

        var total = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(e => e.CreatedAt)
            .Skip((Math.Max(pageNumber, 1) - 1) * pageSize)
            .Take(pageSize)
            .Select(e => new AuditLogDto
            {
                Id = e.Id,
                UserId = e.UserId,
                UserName = e.UserName,
                Action = e.Action,
                Module = e.Module,
                Entity = e.Entity,
                EntityId = e.EntityId,
                Details = e.Details,
                IpAddress = e.IpAddress,
                CreatedAt = e.CreatedAt
            })
            .ToListAsync(cancellationToken);

        return new AuditLogPage { Items = items, Total = total };
    }
}