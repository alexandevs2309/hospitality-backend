namespace Hospitality.Domain.Entities;

public class OutboxMessage : BaseEntity
{
    public Guid? DomainEventId { get; set; }
    public string Topic { get; set; } = string.Empty;
    public string? Payload { get; set; }
    public string Status { get; set; } = "Pending";
    public int Attempts { get; set; }
    public DateTime? NextAttemptAt { get; set; }
    public DateTime? ProcessedAt { get; set; }
}