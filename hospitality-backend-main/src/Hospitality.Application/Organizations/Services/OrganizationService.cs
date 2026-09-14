using Hospitality.Application.Common.Interfaces;
using Hospitality.Application.Organizations.Commands;
using Hospitality.Domain.Entities;
using Hospitality.Domain.Exceptions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Text;

namespace Hospitality.Application.Organizations.Services;

public class OrganizationService : IOrganizationService
{
    private static readonly string[] AllowedOrganizationRoles =
        { "Owner", "Admin", "Member" };

    private static readonly string[] AllowedPropertyRoles =
        { "Owner", "Admin", "Manager", "Receptionist", "Housekeeping", "Maintenance", "User" };

    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<OrganizationService> _logger;

    public OrganizationService(
        IApplicationDbContext context,
        ICurrentUserService currentUserService,
        UserManager<ApplicationUser> userManager,
        ILogger<OrganizationService> logger)
    {
        _context = context;
        _currentUserService = currentUserService;
        _userManager = userManager;
        _logger = logger;
    }

    public async Task<bool> IsOrganizationMemberAsync()
        => await ResolveMyMembershipAsync() is not null;

    public async Task<OrganizationSummaryDto> GetMyOrganizationAsync()
    {
        var membership = await ResolveMyMembershipAsync()
            ?? throw new KeyNotFoundException("El usuario no pertenece a ninguna organización.");

        var organization = await _context.Organizations
            .FirstOrDefaultAsync(o => o.Id == membership.OrganizationId && o.IsActive && !o.IsDeleted)
            ?? throw new KeyNotFoundException("La organización no fue encontrada.");

        var properties = await BuildMyPropertiesAsync(organization.Id);
        var memberCount = await _context.OrganizationMembers.CountAsync(m =>
            m.OrganizationId == organization.Id && m.IsActive && !m.IsDeleted);

        return new OrganizationSummaryDto
        {
            OrganizationId = organization.Id,
            Name = organization.Name,
            BusinessName = organization.BusinessName,
            TaxId = organization.TaxId,
            Plan = organization.Plan,
            DefaultCurrency = organization.DefaultCurrency,
            TimeZone = organization.TimeZone,
            MyOrganizationRole = membership.OrganizationRole,
            MemberCount = memberCount,
            Properties = properties
        };
    }

    public async Task<List<UserPropertyDto>> GetMyPropertiesAsync()
    {
        var userId = RequireUserId();
        return await _context.PropertyAssignments
            .Where(pa => pa.UserId == userId && pa.IsActive && !pa.IsDeleted)
            .Join(_context.Hotels.Where(h => !h.IsDeleted),
                pa => pa.PropertyId, h => h.Id, (pa, h) => new { pa, h })
            .Where(x => x.h.IsActive)
            .OrderBy(x => x.h.Name)
            .Select(x => new UserPropertyDto
            {
                PropertyId = x.h.Id,
                Name = x.h.Name,
                PropertyRole = x.pa.PropertyRole,
                Currency = x.h.Currency ?? "USD"
            })
            .ToListAsync();
    }

    public async Task<List<OrganizationMemberDto>> GetMembersAsync()
    {
        var membership = await RequireMyMembershipAsync();

        var members = await _context.OrganizationMembers
            .Where(m => m.OrganizationId == membership.OrganizationId && m.IsActive && !m.IsDeleted)
            .OrderBy(m => m.CreatedAt)
            .ToListAsync();

        var userIds = members.Select(m => m.UserId).ToList();
        var users = await _context.Users.Where(u => userIds.Contains(u.Id)).ToListAsync();

        var assignments = await _context.PropertyAssignments
            .Where(pa => pa.IsActive && !pa.IsDeleted && userIds.Contains(pa.UserId))
            .Join(_context.Hotels.Where(h => !h.IsDeleted),
                pa => pa.PropertyId, h => h.Id, (pa, h) => new { pa, h })
            .Select(x => new
            {
                x.pa.UserId,
                x.pa.PropertyId,
                x.pa.PropertyRole,
                PropertyName = x.h.Name
            })
            .ToListAsync();

        return members.Select(m =>
        {
            var user = users.FirstOrDefault(u => u.Id == m.UserId);
            return new OrganizationMemberDto
            {
                UserId = m.UserId,
                FullName = user is null
                    ? string.Empty
                    : string.Join(' ', new[] { user.FirstName, user.LastName }.Where(s => !string.IsNullOrWhiteSpace(s))),
                Email = user?.Email ?? m.UserId,
                OrganizationRole = m.OrganizationRole,
                IsActive = m.IsActive,
                Properties = assignments
                    .Where(a => a.UserId == m.UserId)
                    .Select(a => new PropertyAssignmentDto
                    {
                        PropertyId = a.PropertyId,
                        PropertyName = a.PropertyName,
                        PropertyRole = a.PropertyRole
                    })
                    .ToList()
            };
        }).ToList();
    }

    public async Task<InviteMemberResultDto> InviteMemberAsync(InviteMemberCommand command)
    {
        var membership = await RequireMyMembershipAsync();

        if (string.IsNullOrWhiteSpace(command.Email))
        {
            throw new ValidationException("El correo del miembro es obligatorio.");
        }
        if (await _userManager.FindByEmailAsync(command.Email.Trim()) is not null)
        {
            throw new ConflictException("El correo ya pertenece a una cuenta registrada.");
        }
        if (!IsAllowedOrganizationRole(command.OrganizationRole))
        {
            throw new ValidationException($"Rol de organización no válido: {command.OrganizationRole}.");
        }

        var properties = await ValidatePropertiesAsync(membership.OrganizationId, command.Properties);

        var temporaryPassword = CreateTemporaryPassword();
        var user = new ApplicationUser
        {
            UserName = command.Email.Trim(),
            Email = command.Email.Trim(),
            EmailConfirmed = false,
            FirstName = command.FirstName?.Trim(),
            LastName = command.LastName?.Trim(),
            IsActive = true,
            MustChangePassword = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var createResult = await _userManager.CreateAsync(user, temporaryPassword);
        if (!createResult.Succeeded)
        {
            throw new ValidationException(string.Join(" ", createResult.Errors.Select(e => e.Description)));
        }

        var appRole = ResolveAppRole(command.Properties.FirstOrDefault()?.PropertyRole ?? command.OrganizationRole);
        await _userManager.AddToRoleAsync(user, appRole);

        _context.OrganizationMembers.Add(new OrganizationMember
        {
            OrganizationId = membership.OrganizationId,
            UserId = user.Id,
            OrganizationRole = command.OrganizationRole,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });

        foreach (var p in properties)
        {
            _context.PropertyAssignments.Add(new PropertyAssignment
            {
                PropertyId = p,
                UserId = user.Id,
                PropertyRole = command.Properties.First(c => c.PropertyId == p).PropertyRole,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
        }

        await _context.SaveChangesAsync();
        _logger.LogInformation("Miembro invitado {Email} a la organización {OrgId}", user.Email, membership.OrganizationId);

        var dto = await BuildMemberDtoAsync(user.Id);
        return new InviteMemberResultDto { Member = dto, TemporaryPassword = temporaryPassword };
    }

    public async Task<OrganizationMemberDto> UpdateMemberAsync(string userId, UpdateMemberCommand command)
    {
        var membership = await RequireMyMembershipAsync();

        var member = await _context.OrganizationMembers
            .FirstOrDefaultAsync(m =>
                m.OrganizationId == membership.OrganizationId &&
                m.UserId == userId && !m.IsDeleted)
            ?? throw new KeyNotFoundException("El miembro no pertenece a esta organización.");

        if (command.IsActive == false && userId == _currentUserService.UserId)
        {
            throw new ValidationException("No puedes desactivar tu propia cuenta de miembro.");
        }
        if (!string.IsNullOrWhiteSpace(command.OrganizationRole) &&
            !IsAllowedOrganizationRole(command.OrganizationRole))
        {
            throw new ValidationException($"Rol de organización no válido: {command.OrganizationRole}.");
        }

        if (!string.IsNullOrWhiteSpace(command.OrganizationRole))
        {
            member.OrganizationRole = command.OrganizationRole;
        }
        if (command.IsActive.HasValue)
        {
            member.IsActive = command.IsActive.Value;
        }
        member.UpdatedAt = DateTime.UtcNow;

        if (command.Properties is not null)
        {
            var propertyIds = await ValidatePropertiesAsync(membership.OrganizationId, command.Properties);

            var existing = await _context.PropertyAssignments
                .Where(pa => pa.UserId == userId && pa.IsActive && !pa.IsDeleted)
                .ToListAsync();

            foreach (var pa in existing)
            {
                var requested = command.Properties.FirstOrDefault(c => c.PropertyId == pa.PropertyId);
                if (requested is null)
                {
                    pa.IsActive = false;
                    pa.IsDeleted = true;
                    pa.UpdatedAt = DateTime.UtcNow;
                }
                else
                {
                    pa.PropertyRole = requested.PropertyRole;
                    pa.UpdatedAt = DateTime.UtcNow;
                }
            }

            foreach (var p in propertyIds.Where(id => !existing.Any(e => e.PropertyId == id)))
            {
                _context.PropertyAssignments.Add(new PropertyAssignment
                {
                    PropertyId = p,
                    UserId = userId,
                    PropertyRole = command.Properties.First(c => c.PropertyId == p).PropertyRole,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                });
            }
        }

        await _context.SaveChangesAsync();
        _logger.LogInformation("Miembro {UserId} actualizado en organización {OrgId}", userId, membership.OrganizationId);

        return await BuildMemberDtoAsync(userId);
    }

    public async Task RemoveMemberAsync(string userId)
    {
        var membership = await RequireMyMembershipAsync();

        if (userId == _currentUserService.UserId)
        {
            throw new ValidationException("No puedes eliminar tu propia cuenta de la organización.");
        }

        var member = await _context.OrganizationMembers
            .FirstOrDefaultAsync(m =>
                m.OrganizationId == membership.OrganizationId &&
                m.UserId == userId && !m.IsDeleted)
            ?? throw new KeyNotFoundException("El miembro no pertenece a esta organización.");

        member.IsActive = false;
        member.IsDeleted = true;
        member.UpdatedAt = DateTime.UtcNow;

        var assignments = await _context.PropertyAssignments
            .Where(pa => pa.UserId == userId && !pa.IsDeleted)
            .ToListAsync();
        foreach (var pa in assignments)
        {
            pa.IsActive = false;
            pa.IsDeleted = true;
            pa.UpdatedAt = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync();
        _logger.LogInformation("Miembro {UserId} removido de organización {OrgId}", userId, membership.OrganizationId);
    }

    // ── helpers ─────────────────────────────────────────────

    private async Task<OrganizationMember?> ResolveMyMembershipAsync()
    {
        var userId = _currentUserService.UserId;
        if (string.IsNullOrEmpty(userId)) return null;

        return await _context.OrganizationMembers
            .FirstOrDefaultAsync(m => m.UserId == userId && m.IsActive && !m.IsDeleted);
    }

    private async Task<OrganizationMember> RequireMyMembershipAsync()
        => await ResolveMyMembershipAsync()
            ?? throw new ForbiddenAccessException("El usuario no pertenece a ninguna organización.");

    private string RequireUserId()
        => _currentUserService.UserId
            ?? throw new ForbiddenAccessException("No se pudo identificar al usuario.");

    private async Task<List<UserPropertyDto>> BuildMyPropertiesAsync(Guid organizationId)
    {
        var userId = RequireUserId();

        return await _context.PropertyAssignments
            .Where(pa => pa.UserId == userId && pa.IsActive && !pa.IsDeleted)
            .Join(_context.Hotels.Where(h => !h.IsDeleted && h.OrganizationId == organizationId && h.IsActive),
                pa => pa.PropertyId, h => h.Id, (pa, h) => new UserPropertyDto
                {
                    PropertyId = h.Id,
                    Name = h.Name,
                    PropertyRole = pa.PropertyRole,
                    Currency = h.Currency ?? "USD"
                })
            .OrderBy(p => p.Name)
            .ToListAsync();
    }

    private async Task<List<Guid>> ValidatePropertiesAsync(Guid organizationId, List<PropertyAssignmentCommand> properties)
    {
        var result = new List<Guid>();
        if (properties is null || properties.Count == 0)
        {
            return result;
        }

        foreach (var command in properties)
        {
            if (!IsAllowedRole(command.PropertyRole))
            {
                throw new ValidationException($"Rol de propiedad no válido: {command.PropertyRole}.");
            }
            var belongs = await _context.Hotels.AnyAsync(h =>
                h.Id == command.PropertyId && h.OrganizationId == organizationId && !h.IsDeleted && h.IsActive);
            if (!belongs)
            {
                throw new ValidationException($"La propiedad {command.PropertyId} no pertenece a esta organización.");
            }
            result.Add(command.PropertyId);
        }

        return result;
    }

    private async Task<OrganizationMemberDto> BuildMemberDtoAsync(string userId)
    {
        var member = await _context.OrganizationMembers
            .FirstOrDefaultAsync(m => m.UserId == userId && !m.IsDeleted)
            ?? throw new KeyNotFoundException("El miembro no fue encontrado.");

        var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);

        var assignments = await _context.PropertyAssignments
            .Where(pa => pa.UserId == userId && pa.IsActive && !pa.IsDeleted)
            .Join(_context.Hotels.Where(h => !h.IsDeleted),
                pa => pa.PropertyId, h => h.Id, (pa, h) => new { pa, h })
            .Select(x => new
            {
                x.pa.PropertyId,
                x.pa.PropertyRole,
                PropertyName = x.h.Name
            })
            .ToListAsync();

        return new OrganizationMemberDto
        {
            UserId = userId,
            FullName = user is null
                ? string.Empty
                : string.Join(' ', new[] { user.FirstName, user.LastName }.Where(s => !string.IsNullOrWhiteSpace(s))),
            Email = user?.Email ?? string.Empty,
            OrganizationRole = member.OrganizationRole,
            IsActive = member.IsActive,
            Properties = assignments
                .Select(a => new PropertyAssignmentDto
                {
                    PropertyId = a.PropertyId,
                    PropertyName = a.PropertyName,
                    PropertyRole = a.PropertyRole
                })
                .ToList()
        };
    }

    private static bool IsAllowedOrganizationRole(string role)
        => AllowedOrganizationRoles.Contains(role, StringComparer.OrdinalIgnoreCase);

    private static bool IsAllowedRole(string role)
        => AllowedPropertyRoles.Contains(role, StringComparer.OrdinalIgnoreCase);

    private static string ResolveAppRole(string propertyRole)
        => (propertyRole ?? string.Empty).ToLowerInvariant() switch
        {
            "owner" or "admin" => "Admin",
            "manager" => "Manager",
            "receptionist" => "Receptionist",
            "housekeeping" => "Housekeeping",
            "maintenance" => "Maintenance",
            _ => "User"
        };

    private static string CreateTemporaryPassword()
    {
        const string letters = "abcdefghijkmnpqrstuvwxyzABCDEFGHJKLMNPQRSTUVWXYZ";
        const string digits = "23456789";
        const string symbols = "!@#$%";
        var random = new Random();
        var password = new StringBuilder(14);

        for (var i = 0; i < 9; i++) password.Append(letters[random.Next(letters.Length)]);
        password.Append(digits[random.Next(digits.Length)]);
        for (var i = 0; i < 3; i++) password.Append(symbols[random.Next(symbols.Length)]);
        password.Append(random.Next(9));

        return new string(password.ToString().OrderBy(_ => random.Next()).ToArray());
    }
}