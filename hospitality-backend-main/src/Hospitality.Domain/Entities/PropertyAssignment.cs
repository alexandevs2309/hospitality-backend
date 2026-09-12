namespace Hospitality.Domain.Entities;

public class PropertyAssignment : BaseEntity, ISoftDelete
{
    public Guid PropertyId { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string PropertyRole { get; set; } = "Owner";

    public bool IsActive { get; set; } = true;

    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }

    public Hotel Property { get; set; } = null!;
}