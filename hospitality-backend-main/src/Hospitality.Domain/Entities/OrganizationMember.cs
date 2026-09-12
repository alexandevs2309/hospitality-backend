namespace Hospitality.Domain.Entities;

public class OrganizationMember : BaseEntity, ISoftDelete
{
    public Guid OrganizationId { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string OrganizationRole { get; set; } = "Owner";

    public bool IsActive { get; set; } = true;

    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }

    public Organization Organization { get; set; } = null!;
}