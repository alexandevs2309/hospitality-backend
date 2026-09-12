using Hospitality.Domain.Exceptions;
using Hospitality.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Hospitality.Application.Common.Interfaces;

public interface IHotelAccessGuard
{
    bool IsAdmin { get; }
    Guid? CurrentHotelId { get; }
    bool CanAccessHotel(Guid hotelId);
    void EnsureCanAccessHotel(Guid hotelId);
    Guid? ResolveRequestedHotel(Guid? requestedHotelId);
    Task<bool> IsPropertyOperatorAsync(Guid hotelId);
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

        return _currentUserService.HotelId.HasValue &&
               _currentUserService.HotelId.Value == hotelId;
    }

    public void EnsureCanAccessHotel(Guid hotelId)
    {
        if (!CanAccessHotel(hotelId))
        {
            throw new ForbiddenAccessException("No tiene permisos para acceder al hotel solicitado.");
        }
    }

    /// <summary>
    /// Resuelve el hotel efectivo a partir de lo solicitado en el request y el hotel del usuario.
    /// Si el usuario no es admin y solicita un hotel distinto al suyo, se lanza 403.
    /// Si no se solicita hotel, se usa el del usuario.
    /// </summary>
    public Guid? ResolveRequestedHotel(Guid? requestedHotelId)
    {
        if (requestedHotelId.HasValue && requestedHotelId.Value != Guid.Empty)
        {
            EnsureCanAccessHotel(requestedHotelId.Value);
            return requestedHotelId.Value;
        }

        if (IsAdmin)
        {
            return CurrentHotelId;
        }

        return CurrentHotelId;
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
}