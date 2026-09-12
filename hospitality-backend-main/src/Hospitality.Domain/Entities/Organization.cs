namespace Hospitality.Domain.Entities;

public class Organization : BaseEntity, ISoftDelete
{
    public string Name { get; set; } = string.Empty;
    public string? BusinessName { get; set; }
    public string? TaxId { get; set; }
    public string? Email { get; set; }
    public string? PhoneNumber { get; set; }
    public string Plan { get; set; } = "small";
    public string? SelectedModules { get; set; }
    public string DefaultCurrency { get; set; } = "USD";
    public string TimeZone { get; set; } = "UTC";

    public bool IsActive { get; set; } = true;

    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }

    public ICollection<Hotel> Properties { get; set; } = new List<Hotel>();
    public ICollection<OrganizationMember> Members { get; set; } = new List<OrganizationMember>();
}