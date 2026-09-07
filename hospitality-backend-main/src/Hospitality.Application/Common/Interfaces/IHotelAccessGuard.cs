using Hospitality.Domain.Exceptions;

namespace Hospitality.Application.Common.Interfaces;

public interface IHotelAccessGuard
{
    bool IsAdmin { get; }
    Guid? CurrentHotelId { get; }
    bool CanAccessHotel(Guid hotelId);
    void EnsureCanAccessHotel(Guid hotelId);
    Guid? ResolveRequestedHotel(Guid? requestedHotelId);
}

public class HotelAccessGuard : IHotelAccessGuard
{
    private readonly ICurrentUserService _currentUserService;

    public HotelAccessGuard(ICurrentUserService currentUserService)
    {
        _currentUserService = currentUserService;
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
}