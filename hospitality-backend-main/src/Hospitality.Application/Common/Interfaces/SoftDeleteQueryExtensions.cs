using Hospitality.Domain.Entities;

namespace Hospitality.Application.Common.Interfaces;

/// <summary>
/// Consultas base con el filtro de soft-delete ya aplicado.
/// Centraliza la convención "!IsDeleted" para que las consultas nuevas
/// no dependan de recordar filtrar entidades borradas manualmente.
/// </summary>
public static class SoftDeleteQueryExtensions
{
    public static IQueryable<Hotel> ActiveHotels(this IApplicationDbContext db)
        => db.Hotels.Where(h => !h.IsDeleted);

    public static IQueryable<Room> ActiveRooms(this IApplicationDbContext db)
        => db.Rooms.Where(r => !r.IsDeleted);

    public static IQueryable<Guest> ActiveGuests(this IApplicationDbContext db)
        => db.Guests.Where(g => !g.IsDeleted);
}