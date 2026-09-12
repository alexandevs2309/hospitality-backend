namespace Hospitality.Domain.Entities;

public class DomainEvent : BaseEntity
{
    public string AggregateType { get; set; } = string.Empty;
    public Guid AggregateId { get; set; }
    public string EventType { get; set; } = string.Empty;
    public int Version { get; set; }
    public string? Payload { get; set; }
    public DateTime OccurredOn { get; set; }
    public string? ActorUserId { get; set; }
    public Guid? CorrelationId { get; set; }
}