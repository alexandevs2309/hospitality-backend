using FluentAssertions;
using Hospitality.Domain.Entities;
using Hospitality.Domain.Enums;
using Xunit;

namespace Hospitality.UnitTests.Domain;

public class HousekeepingStatusTests
{
    private static (HousekeepingStatus status, Room room) CreateDirtyRoom(string priority = "Normal")
    {
        var room = new Room
        {
            RoomNumber = "101",
            Status = RoomStatus.Dirty,
            IsClean = false,
            IsMaintenanceRequired = false
        };
        var status = new HousekeepingStatus
        {
            Status = HousekeepingStatusType.Dirty,
            Priority = priority,
            Room = room
        };
        return (status, room);
    }

    [Fact]
    public void StartCleaning_OnlyFromDirty()
    {
        var (status, _) = CreateDirtyRoom();

        status.StartCleaning("Carmen");

        status.Status.Should().Be(HousekeepingStatusType.InProgress);
        status.HousekeeperName.Should().Be("Carmen");
        status.StartedAt.Should().NotBeNull();
    }

    [Fact]
    public void StartCleaning_DoesNothing_WhenAlreadyClean()
    {
        var (status, _) = CreateDirtyRoom();
        status.StartCleaning("Carmen");
        status.CompleteCleaning(true);
        var original = status.Status;

        status.StartCleaning("Otra");

        status.Status.Should().Be(original);
    }

    [Fact]
    public void CompleteCleaning_PassedInspection_MarksRoomAvailableAndClean()
    {
        var (status, room) = CreateDirtyRoom();
        status.StartCleaning("Carmen");

        status.CompleteCleaning(true);

        status.Status.Should().Be(HousekeepingStatusType.Clean);
        status.PassedInspection.Should().BeTrue();
        room.IsClean.Should().BeTrue();
        room.Status.Should().Be(RoomStatus.Available);
    }

    [Fact]
    public void CompleteCleaning_WithMaintenanceRequired_SetsRoomToMaintenance()
    {
        var (status, room) = CreateDirtyRoom();
        room.IsMaintenanceRequired = true;
        status.StartCleaning("Carmen");

        status.CompleteCleaning(true);

        room.Status.Should().Be(RoomStatus.Maintenance);
    }

    [Fact]
    public void FailInspection_ReturnsToDirty_AndKeepsFailure()
    {
        var (status, _) = CreateDirtyRoom();
        status.StartCleaning("Carmen");
        status.SendToInspection();

        status.FailInspection("Camas sin hacer");

        status.Status.Should().Be(HousekeepingStatusType.Dirty);
        status.InspectionNotes.Should().Be("Camas sin hacer");
        status.PassedInspection.Should().BeFalse();
    }

    [Fact]
    public void SendToInspection_OnlyFromInProgress()
    {
        var (status, _) = CreateDirtyRoom();

        status.SendToInspection();

        status.Status.Should().Be(HousekeepingStatusType.Dirty);
    }

    [Fact]
    public void IsUrgent_WhenPriorityIsAlta()
    {
        var (status, _) = CreateDirtyRoom("Alta");

        status.IsUrgent.Should().BeTrue();
    }

    [Fact]
    public void IsUrgent_FalseForNormalPriorityWithoutCloseCheckIn()
    {
        var (status, _) = CreateDirtyRoom("Normal");

        status.IsUrgent.Should().BeFalse();
    }
}