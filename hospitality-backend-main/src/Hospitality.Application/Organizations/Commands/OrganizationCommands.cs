namespace Hospitality.Application.Organizations.Commands;

public class UserPropertyDto
{
    public Guid PropertyId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string PropertyRole { get; set; } = string.Empty;
    public string Currency { get; set; } = "USD";
}

public class OrganizationSummaryDto
{
    public Guid OrganizationId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? BusinessName { get; set; }
    public string? TaxId { get; set; }
    public string Plan { get; set; } = "small";
    public string DefaultCurrency { get; set; } = "USD";
    public string TimeZone { get; set; } = "UTC";
    public string MyOrganizationRole { get; set; } = "Member";
    public int MemberCount { get; set; }
    public List<UserPropertyDto> Properties { get; set; } = new();
}

public class PropertyAssignmentDto
{
    public Guid PropertyId { get; set; }
    public string PropertyName { get; set; } = string.Empty;
    public string PropertyRole { get; set; } = string.Empty;
}

public class OrganizationMemberDto
{
    public string UserId { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string OrganizationRole { get; set; } = "Member";
    public bool IsActive { get; set; }
    public List<PropertyAssignmentDto> Properties { get; set; } = new();
}

public class InviteMemberCommand
{
    public string Email { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string OrganizationRole { get; set; } = "Member";
    public List<PropertyAssignmentCommand> Properties { get; set; } = new();
}

public class PropertyAssignmentCommand
{
    public Guid PropertyId { get; set; }
    public string PropertyRole { get; set; } = "Manager";
}

public class UpdateMemberCommand
{
    public string? OrganizationRole { get; set; }
    public bool? IsActive { get; set; }
    public List<PropertyAssignmentCommand>? Properties { get; set; }
}

public class InviteMemberResultDto
{
    public OrganizationMemberDto Member { get; set; } = new();
    public string TemporaryPassword { get; set; } = string.Empty;
}

public interface IOrganizationService
{
    Task<OrganizationSummaryDto> GetMyOrganizationAsync();
    Task<List<UserPropertyDto>> GetMyPropertiesAsync();
    Task<bool> IsOrganizationMemberAsync();
    Task<List<OrganizationMemberDto>> GetMembersAsync();
    Task<InviteMemberResultDto> InviteMemberAsync(InviteMemberCommand command);
    Task<OrganizationMemberDto> UpdateMemberAsync(string userId, UpdateMemberCommand command);
    Task RemoveMemberAsync(string userId);
}