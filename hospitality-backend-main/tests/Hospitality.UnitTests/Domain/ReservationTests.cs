using FluentAssertions;
using Hospitality.Domain.Entities;
using Hospitality.Domain.Enums;
using Xunit;

namespace Hospitality.UnitTests.Domain;

public class ReservationTests
{
    private static Reservation CreateReservation(DateTime checkIn, DateTime checkOut) =>
        new()
        {
            CheckInDate = checkIn,
            CheckOutDate = checkOut,
            Status = ReservationStatus.Pending,
            RoomRate = 100m,
            TaxRate = 16.00m,
            Room = new Room { Status = RoomStatus.Available, RoomNumber = "101" }
        };

    [Fact]
    public void NumberOfNights_ComputesDifferenceInDays()
    {
        var reservation = CreateReservation(new DateTime(2026, 9, 10, 0, 0, 0, DateTimeKind.Utc), new DateTime(2026, 9, 13, 0, 0, 0, DateTimeKind.Utc));

        reservation.NumberOfNights.Should().Be(3);
    }

    [Fact]
    public void CalculateTotal_AddsTaxOverRoomAndExtraBedCharges()
    {
        var reservation = CreateReservation(DateTime.UtcNow, DateTime.UtcNow.AddDays(2));
        reservation.HasExtraBed = true;
        reservation.ExtraBedRate = 20m;

        reservation.CalculateTotal();

        reservation.Subtotal.Should().Be(240m); // (100 + 20) * 2 noches
        reservation.Taxes.Should().Be(38.4m);
        reservation.TotalAmount.Should().Be(278.4m);
    }

    [Fact]
    public void Confirm_TransitionsPendingToConfirmed()
    {
        var reservation = CreateReservation(DateTime.UtcNow.AddDays(1), DateTime.UtcNow.AddDays(3));

        reservation.Confirm();

        reservation.Status.Should().Be(ReservationStatus.Confirmed);
    }

    [Fact]
    public void Confirm_DoesNothing_WhenNotPending()
    {
        var reservation = CreateReservation(DateTime.UtcNow.AddDays(1), DateTime.UtcNow.AddDays(3));
        reservation.Confirm();
        reservation.Status.Should().Be(ReservationStatus.Confirmed);

        reservation.Confirm();

        reservation.Status.Should().Be(ReservationStatus.Confirmed);
    }

    [Fact]
    public void CheckIn_OccupiesRoom_AndSetsTimestamp()
    {
        var reservation = CreateReservation(DateTime.UtcNow.AddDays(1), DateTime.UtcNow.AddDays(3));
        reservation.Confirm();

        reservation.CheckIn();

        reservation.Status.Should().Be(ReservationStatus.CheckedIn);
        reservation.CheckedInAt.Should().NotBeNull();
        reservation.Room.Status.Should().Be(RoomStatus.Occupied);
    }

    [Fact]
    public void CheckIn_DoesNothing_UnlessConfirmed()
    {
        var reservation = CreateReservation(DateTime.UtcNow.AddDays(1), DateTime.UtcNow.AddDays(3));

        reservation.CheckIn();

        reservation.Status.Should().Be(ReservationStatus.Pending);
        reservation.Room.Status.Should().Be(RoomStatus.Available);
    }

    [Fact]
    public void CheckOut_MarksRoomDirty()
    {
        var reservation = CreateReservation(DateTime.UtcNow.AddDays(1), DateTime.UtcNow.AddDays(3));
        reservation.Confirm();
        reservation.CheckIn();

        reservation.CheckOut();

        reservation.Status.Should().Be(ReservationStatus.CheckedOut);
        reservation.CheckedOutAt.Should().NotBeNull();
        reservation.Room.Status.Should().Be(RoomStatus.Dirty);
    }

    [Fact]
    public void CheckOut_DoesNothing_UnlessCheckedIn()
    {
        var reservation = CreateReservation(DateTime.UtcNow.AddDays(1), DateTime.UtcNow.AddDays(3));
        reservation.Confirm();

        reservation.CheckOut();

        reservation.Status.Should().Be(ReservationStatus.Confirmed);
    }

    [Fact]
    public void Cancel_IsAllowedUntilCheckedOut()
    {
        var reservation = CreateReservation(DateTime.UtcNow.AddDays(1), DateTime.UtcNow.AddDays(3));
        reservation.Confirm();
        reservation.CheckIn();
        reservation.CheckOut();

        reservation.Cancel("Guest decided");

        reservation.Status.Should().Be(ReservationStatus.CheckedOut);
        reservation.CancelledAt.Should().BeNull();
    }

    [Fact]
    public void MarkAsNoShow_OnlyWhenPastCheckInDate()
    {
        var past = CreateReservation(DateTime.UtcNow.AddDays(-1), DateTime.UtcNow.AddDays(1));
        past.Confirm();
        past.MarkAsNoShow();
        past.Status.Should().Be(ReservationStatus.NoShow);

        var future = CreateReservation(DateTime.UtcNow.AddDays(1), DateTime.UtcNow.AddDays(3));
        future.Confirm();
        future.MarkAsNoShow();
        future.Status.Should().Be(ReservationStatus.Confirmed);
    }

    [Fact]
    public void CalculateCancellationFee_IsTotalOnCheckInDay()
    {
        var reservation = CreateReservation(DateTime.UtcNow, DateTime.UtcNow.AddDays(2));
        reservation.CalculateTotal();

        reservation.CalculateCancellationFee().Should().Be(reservation.TotalAmount);
    }
}