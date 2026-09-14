using Hospitality.Domain.Exceptions;
using Hospitality.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Hospitality.Application.Common.Interfaces;

public interface IHotelAccessGuard
{
    bool IsAdmin { get; }
    Guid? CurrentHotelId { get; }
    IReadOnlyList<Guid> PropertyIds { get; }
    bool CanAccessHotel(Guid hotelId);
    void EnsureCanAccessHotel(Guid hotelId);
    Guid? ResolveRequestedHotel(Guid? requestedHotelId);
    Task<bool> IsPropertyOperatorAsync(Guid hotelId);
    Task<bool> IsOrganizationAdminAsync();
}

public class HotelAccessGuard : IHotelAccessGuard
{
    private readonly ICurrentUserService _currentUserService;
    private readonly IApplicationDbContext _context;

    public HotelAccessGuard(ICurrentUserService currentUserService, IApplicationDbContext context)
    {
        _currentUserService = currentUserService;
        _context = context;
    }

    public bool IsAdmin => _currentUserService.IsInRole("Admin");

    public Guid? CurrentHotelId => _currentUserService.HotelId;

    /// <summary>
    /// Propiedades autorizadas del usuario. La autoridad es la BD
    /// (`PropertyAssignments` activas); el claim `property_ids` es un fast-path
    /// legacy que solo se respeta cuando el usuario aún no tiene asignaciones
    /// migradas (token pre-Fase0).
    /// </summary>
    public IReadOnlyList<Guid> PropertyIds => ResolveAuthorizedPropertyIds();

    private IReadOnlyList<Guid>? _authorizedIdsCache;

    private IReadOnlyList<Guid> ResolveAuthorizedPropertyIds()
    {
        if (_authorizedIdsCache is not null)
        {
            return _authorizedIdsCache;
        }

        var ids = new List<Guid>();
        var userId = _currentUserService.UserId;
        if (!string.IsNullOrEmpty(userId))
        {
            var activeIds = _context.PropertyAssignments
                .Where(pa => pa.UserId == userId && pa.IsActive && !pa.IsDeleted)
                .Select(pa => pa.PropertyId)
                .ToList();

            var hasAnyAssignment = _context.PropertyAssignments
                .Any(pa => pa.UserId == userId && !pa.IsDeleted);

            if (hasAnyAssignment)
            {
                // Usuario ya provisionado: la BD es la fuente única de verdad.
                // Asignaciones revocadas/desactivadas NO son accesibles aunque el
                // JWT aún lleve el claim.
                ids.AddRange(activeIds);
            }
            else
            {
                // Legacy: usuario pre-Fase0 aún sin fila de asignación → se
                // respeta el claim property_ids / HotelId único hasta migrar.
                ids.AddRange(_currentUserService.PropertyIds);
                if (_currentUserService.HotelId.HasValue)
                {
                    ids.Add(_currentUserService.HotelId.Value);
                }
            }
        }

        _authorizedIdsCache = ids.Distinct().ToList();
        return _authorizedIdsCache;
    }

    public bool CanAccessHotel(Guid hotelId)
    {
        if (hotelId == Guid.Empty)
        {
            return false;
        }

        if (IsAdmin)
        {
            return true;
        }

        return ResolveAuthorizedPropertyIds().Contains(hotelId);
    }

    public void EnsureCanAccessHotel(Guid hotelId)
    {
        if (!CanAccessHotel(hotelId))
        {
            throw new ForbiddenAccessException("No tiene permisos para acceder al hotel solicitado.");
        }
    }

    /// <summary>
    /// Resuelve el hotel efectivo a partir de lo solicitado en el request y las
    /// asignaciones del usuario. Si el usuario no es admin y solicita un hotel
    /// al que no tiene `PropertyAssignment` activa, se lanza 403. Si no se
    /// solicita hotel, se usa la propiedad activa y, si no hay, la primera
    /// asignación.
    /// </summary>
    public Guid? ResolveRequestedHotel(Guid? requestedHotelId)
    {
        if (requestedHotelId.HasValue && requestedHotelId.Value != Guid.Empty)
        {
            EnsureCanAccessHotel(requestedHotelId.Value);
            return requestedHotelId.Value;
        }

        if (CurrentHotelId.HasValue)
        {
            return CurrentHotelId;
        }

        if (IsAdmin)
        {
            return null;
        }

        var first = PropertyIds.FirstOrDefault();
        return first == Guid.Empty ? null : first;
    }

    /// <summary>
    /// Indica si el usuario opera la propiedad (Owner/Manager/Receptionist por
    /// membership) o es admin global. Usado para autorizar mutaciones por rol de
    /// propiedad en vez de roles de app.
    /// </summary>
    public async Task<bool> IsPropertyOperatorAsync(Guid hotelId)
    {
        if (IsAdmin)
        {
            return true;
        }

        var operatorRoles = new[] { "Owner", "Admin", "Manager", "Receptionist" };
        return await _context.PropertyAssignments
            .AnyAsync(pa => pa.PropertyId == hotelId
                && pa.UserId == _currentUserService.UserId
                && operatorRoles.Contains(pa.PropertyRole));
    }

    /// <summary>
    /// El usuario administra la organización (OrganizationRole Owner/Admin),
    /// lo que le permite añadir propiedades e invitar miembros.
    /// </summary>
    public Task<bool> IsOrganizationAdminAsync()
    {
        var userId = _currentUserService.UserId;
        if (string.IsNullOrEmpty(userId))
        {
            return Task.FromResult(false);
        }

        return _context.OrganizationMembers.AnyAsync(m =>
            m.UserId == userId && m.IsActive && !m.IsDeleted &&
            (m.OrganizationRole == "Owner" || m.OrganizationRole == "Admin"));
    }
}