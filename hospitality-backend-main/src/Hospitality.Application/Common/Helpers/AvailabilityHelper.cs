using Hospitality.Application.Common.Interfaces;
using Hospitality.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Hospitality.Application.Common.Helpers;

public static class AvailabilityHelper
{
    public static async Task<int> CalculateAvailableRoomsAsync(IApplicationDbContext context, Guid roomTypeId, DateTime date)
    {
        var totalRooms = await context.Rooms
            .CountAsync(r => r.RoomTypeId == roomTypeId && !r.IsDeleted);

        if (totalRooms == 0) return 0;

        var occupied = await context.Reservations
            .CountAsync(r => r.Room.RoomTypeId == roomTypeId &&
                             r.CheckInDate.Date <= date &&
                             r.CheckOutDate.Date > date &&
                             (r.Status == ReservationStatus.Confirmed ||
                              r.Status == ReservationStatus.CheckedIn));

        return Math.Max(0, totalRooms - occupied);
    }
}