using FluentAssertions;
using Hospitality.Application.Common.Interfaces;
using Hospitality.Domain.Entities;
using Hospitality.Domain.Exceptions;
using Hospitality.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace Hospitality.UnitTests;

/// <summary>
/// RBAC scopeado por propiedad: la autoridad de acceso es la tabla
/// PropertyAssignments, no un campo único HotelId ni el claim del JWT.
/// </summary>
public class PropertyAccessTests
{
    private static readonly Guid PropertyA = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid PropertyB = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid PropertyC = Guid.Parse("33333333-3333-3333-3333-333333333333");

    private const string UserId = "user-1";

    private static ICurrentUserService MockUser(
        Guid? hotelId = null,
        IReadOnlyList<Guid>? claimPropertyIds = null,
        bool isAdmin = false)
    {
        var mock = new Mock<ICurrentUserService>();
        mock.Setup(u => u.UserId).Returns(UserId);
        mock.Setup(u => u.HotelId).Returns(hotelId);
        mock.Setup(u => u.PropertyIds).Returns(claimPropertyIds ?? Array.Empty<Guid>());
        mock.Setup(u => u.IsInRole(It.IsAny<string>())).Returns(false);
        mock.Setup(u => u.IsInRole("Admin")).Returns(isAdmin);
        return mock.Object;
    }

    private static readonly Func<IApplicationDbContext> NewEmptyContext = () =>
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"guard-{Guid.NewGuid():N}")
            .Options;
        return new ApplicationDbContext(options);
    };

    private static IApplicationDbContext SeedContext(params PropertyAssignment[] assignments)
    {
        var ctx = NewEmptyContext();
        ctx.PropertyAssignments.AddRange(assignments);
        ctx.SaveChangesAsync().GetAwaiter().GetResult();
        return ctx;
    }

    private static PropertyAssignment Assignment(Guid propertyId, bool isActive = true, string role = "Owner") =>
        new()
        {
            Id = Guid.NewGuid(),
            PropertyId = propertyId,
            UserId = UserId,
            PropertyRole = role,
            IsActive = isActive,
            IsDeleted = false
        };

    [Fact]
    public void User_WithTwoAssignments_CanAccessBothButNotThird()
    {
        var ctx = SeedContext(Assignment(PropertyA), Assignment(PropertyB));
        var user = MockUser();
        var guard = new HotelAccessGuard(user, ctx);

        guard.CanAccessHotel(PropertyA).Should().BeTrue();
        guard.CanAccessHotel(PropertyB).Should().BeTrue();
        guard.CanAccessHotel(PropertyC).Should().BeFalse();

        guard.PropertyIds.Should().BeEquivalentTo(new[] { PropertyA, PropertyB });

        guard.EnsureCanAccessHotel(PropertyA);
        guard.EnsureCanAccessHotel(PropertyB);
        FluentActions.Invoking(() => guard.EnsureCanAccessHotel(PropertyC))
            .Should().Throw<ForbiddenAccessException>();

        guard.ResolveRequestedHotel(PropertyA).Should().Be(PropertyA);
        guard.ResolveRequestedHotel(PropertyB).Should().Be(PropertyB);
        FluentActions.Invoking(() => guard.ResolveRequestedHotel(PropertyC))
            .Should().Throw<ForbiddenAccessException>();
    }

    [Fact]
    public void User_WithoutAnyAssignment_CanAccessNothing()
    {
        var ctx = NewEmptyContext();
        var user = MockUser(hotelId: null, claimPropertyIds: Array.Empty<Guid>());
        var guard = new HotelAccessGuard(user, ctx);

        guard.CanAccessHotel(PropertyA).Should().BeFalse();
        guard.CanAccessHotel(PropertyB).Should().BeFalse();
        guard.PropertyIds.Should().BeEmpty();

        FluentActions.Invoking(() => guard.EnsureCanAccessHotel(PropertyA))
            .Should().Throw<ForbiddenAccessException>();
        FluentActions.Invoking(() => guard.ResolveRequestedHotel(PropertyA))
            .Should().Throw<ForbiddenAccessException>();
    }

    [Fact]
    public void RevokedAssignment_IsNotAccessible_EvenIfHotelIdClaimPointsToIt()
    {
        // La asignación existe pero está desactivada: la BD manda, no el claim.
        var ctx = SeedContext(Assignment(PropertyB, isActive: false));
        var user = MockUser(hotelId: PropertyB, claimPropertyIds: new[] { PropertyB });
        var guard = new HotelAccessGuard(user, ctx);

        guard.CanAccessHotel(PropertyB).Should().BeFalse();
        FluentActions.Invoking(() => guard.ResolveRequestedHotel(PropertyB))
            .Should().Throw<ForbiddenAccessException>();
    }

    [Fact]
    public void LegacyUser_WithoutAssignments_FallsBackToClaimAndSingleHotel()
    {
        // Usuario pre-Fase0 sin filas en PropertyAssignments: se respeta el claim
        // (compatibilidad) hasta que se migre la asignación.
        var ctx = NewEmptyContext();
        var user = MockUser(hotelId: PropertyA, claimPropertyIds: new[] { PropertyA, PropertyC });
        var guard = new HotelAccessGuard(user, ctx);

        guard.CanAccessHotel(PropertyA).Should().BeTrue();
        guard.CanAccessHotel(PropertyC).Should().BeTrue();
        guard.CanAccessHotel(PropertyB).Should().BeFalse();
    }

    [Fact]
    public void ResolveRequestedHotel_WithNoRequest_UsesActivePropertyOrFirstAssignment_UnlessAdmin()
    {
        var ctx = SeedContext(Assignment(PropertyA), Assignment(PropertyB));

        var withActive = MockUser(hotelId: PropertyB);
        new HotelAccessGuard(withActive, ctx).ResolveRequestedHotel(null).Should().Be(PropertyB);

        var noActive = MockUser(hotelId: null);
        new HotelAccessGuard(noActive, ctx).ResolveRequestedHotel(null).Should().Be(PropertyA);

        var admin = MockUser(hotelId: null, isAdmin: true);
        new HotelAccessGuard(admin, ctx).ResolveRequestedHotel(null).Should().BeNull();
        new HotelAccessGuard(admin, ctx).ResolveRequestedHotel(PropertyC).Should().Be(PropertyC);
    }
}