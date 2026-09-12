using System.Text.Json;
using Hospitality.Application.Common.Interfaces;
using Hospitality.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Hospitality.Application.Common;

/// <summary>
/// Event sourcing-lite (Fase 0): construye el evento de dominio de un agregado
/// (reservación / folio) con versionado estricto. El evento se añade al contexto
/// y se guarda en la MISMA transacción que la proyección del estado transaccional
/// (tablas actuales Reservations/Invoices/Payments).
/// </summary>
public static class DomainEventLog
{
    public static async Task<DomainEvent> NewAsync(
        IApplicationDbContext context,
        string aggregateType,
        Guid aggregateId,
        string eventType,
        object payload,
        string? actorUserId = null,
        Guid? correlationId = null,
        CancellationToken cancellationToken = default)
    {
        var version = await context.DomainEvents
            .Where(e => e.AggregateType == aggregateType && e.AggregateId == aggregateId)
            .CountAsync(cancellationToken) + 1;

        var now = DateTime.UtcNow;
        return new DomainEvent
        {
            AggregateType = aggregateType,
            AggregateId = aggregateId,
            EventType = eventType,
            Version = version,
            Payload = JsonSerializer.Serialize(payload),
            OccurredOn = now,
            ActorUserId = actorUserId,
            CorrelationId = correlationId,
            CreatedAt = now,
            UpdatedAt = now
        };
    }
}