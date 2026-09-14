using FluentAssertions;
using Hospitality.Application.Automation.Commands;
using Hospitality.Application.ChannelManager.Adapters;
using Hospitality.Application.ChannelManager.Services;
using Hospitality.Application.Common.Interfaces;
using Hospitality.Application.Onboarding.Commands;
using Hospitality.Application.Onboarding.Services;
using Hospitality.Application.Organizations.Commands;
using Hospitality.Application.Organizations.Services;
using Hospitality.Application.RatePlans.Commands;
using Hospitality.Application.RatePlans.Services;
using Hospitality.Application.Rates.Commands;
using Hospitality.Application.Rates.Services;
using Hospitality.Domain.Entities;
using Hospitality.Domain.Enums;
using Hospitality.Domain.Exceptions;
using Hospitality.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Hospitality.UnitTests;

/// <summary>
/// Cobertura mínima por servicio nuevo (GATE): happy path + rechazo para
/// RatePlan, Rates, Onboarding, Channels y Organizations. Patrón: InMemory
/// + Moq, igual que PropertyAccessTests.
/// </summary>
public class ServiceCoverageTests
{
    private static readonly Guid HotelId = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");
    private static readonly Guid OrgId = Guid.Parse("bbbbbbbb-0000-0000-0000-000000000001");
    private static readonly Guid RoomTypeId = Guid.Parse("cccccccc-0000-0000-0000-000000000001");
    private static readonly Guid RoomId = Guid.Parse("dddddddd-0000-0000-0000-000000000001");
    private static readonly Guid RatePlanId = Guid.Parse("eeeeeeee-0000-0000-0000-000000000001");
    private static readonly Guid ChannelId = Guid.Parse("ffffffff-0000-0000-0000-000000000001");
    private const string UserId = "user-owner";

    private static IApplicationDbContext NewContext() =>
        new ApplicationDbContext(
            new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase($"svc-{Guid.NewGuid():N}")
                .Options);

    // ------------------------------------------------------------------ RatePlans

    public class RatePlanServiceTests
    {
        [Fact]
        public async Task CreatePlan_WithValidCommand_PersistsPlan()
        {
            var ctx = NewContext();
            var svc = new RatePlanService(ctx);

            var dto = await svc.CreatePlanAsync(new UpsertRatePlanCommand
            {
                HotelId = HotelId,
                Name = "  Flexible Plus  ",
                Multiplier = 1.2m,
                MinStay = 2,
                IsDefault = false
            });

            dto.Name.Should().Be("Flexible Plus");
            dto.MinStay.Should().Be(2);
            dto.IsDefault.Should().BeFalse();
            (await ctx.RatePlans.CountAsync()).Should().Be(1);
            (await ctx.RatePlans.FirstAsync()).HotelId.Should().Be(HotelId);
        }

        [Fact]
        public async Task CreatePlan_WithZeroMinStay_Rejects()
        {
            var ctx = NewContext();
            var svc = new RatePlanService(ctx);

            var act = async () => await svc.CreatePlanAsync(new UpsertRatePlanCommand
            {
                HotelId = HotelId,
                Name = "Ilegal",
                MinStay = 0
            });

            await act.Should().ThrowAsync<ValidationException>()
                .WithMessage("*estancia mínima*");
        }

        [Fact]
        public async Task CreatePlan_WithZeroMultiplier_Rejects()
        {
            var ctx = NewContext();
            var svc = new RatePlanService(ctx);

            var act = async () => await svc.CreatePlanAsync(new UpsertRatePlanCommand
            {
                HotelId = HotelId,
                Name = "Ilegal",
                Multiplier = 0
            });

            await act.Should().ThrowAsync<ValidationException>()
                .WithMessage("*multiplicador*");
        }

        [Fact]
        public async Task CreatePlan_DuplicateNameInHotel_Rejects()
        {
            var ctx = NewContext();
            var svc = new RatePlanService(ctx);
            await svc.CreatePlanAsync(new UpsertRatePlanCommand
            {
                HotelId = HotelId,
                Name = "Flexible",
                IsDefault = false
            });

            var act = async () => await svc.CreatePlanAsync(new UpsertRatePlanCommand
            {
                HotelId = HotelId,
                Name = "flexible",
                IsDefault = false
            });

            await act.Should().ThrowAsync<ValidationException>()
                .WithMessage("*Ya existe un plan con ese nombre*");
        }
    }

    // ------------------------------------------------------------------ Rates

    public class RateServiceTests
    {
        private static RateService NewService(out IApplicationDbContext ctx)
        {
            ctx = NewContext();
            return new RateService(ctx);
        }

        [Fact]
        public async Task SetRateRange_ValidRange_UpsertsDailyRoomRates()
        {
            var svc = NewService(out var ctx);

            var count = await svc.SetRateRangeAsync(new SetRateRangeCommand
            {
                HotelId = HotelId,
                RoomTypeId = RoomTypeId,
                From = new DateOnly(2026, 10, 1),
                To = new DateOnly(2026, 10, 3),
                Price = 150m
            });

            count.Should().Be(3);
            var rates = await ctx.RoomRates.ToListAsync();
            rates.Should().HaveCount(3);
            rates.Should().OnlyContain(r => r.HotelId == HotelId && r.RoomTypeId == RoomTypeId && r.Price == 150m);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-5)]
        public async Task SetRateRange_NonPositivePrice_Rejects(decimal price)
        {
            var svc = NewService(out _);

            var act = async () => await svc.SetRateRangeAsync(new SetRateRangeCommand
            {
                HotelId = HotelId,
                RoomTypeId = RoomTypeId,
                From = new DateOnly(2026, 10, 1),
                To = new DateOnly(2026, 10, 3),
                Price = price
            });

            await act.Should().ThrowAsync<ArgumentException>();
        }

        [Fact]
        public async Task SetRateRange_InvertedRange_Rejects()
        {
            var svc = NewService(out _);

            var act = async () => await svc.SetRateRangeAsync(new SetRateRangeCommand
            {
                HotelId = HotelId,
                RoomTypeId = RoomTypeId,
                From = new DateOnly(2026, 10, 5),
                To = new DateOnly(2026, 10, 3),
                Price = 150m
            });

            await act.Should().ThrowAsync<ArgumentException>();
        }

        [Fact]
        public async Task SetRateRange_ExistingOverrides_AreUpdatedOnce()
        {
            var svc = NewService(out var ctx);
            await svc.SetRateRangeAsync(new SetRateRangeCommand
            {
                HotelId = HotelId,
                RoomTypeId = RoomTypeId,
                From = new DateOnly(2026, 10, 1),
                To = new DateOnly(2026, 10, 2),
                Price = 100m
            });

            var second = await svc.SetRateRangeAsync(new SetRateRangeCommand
            {
                HotelId = HotelId,
                RoomTypeId = RoomTypeId,
                From = new DateOnly(2026, 10, 1),
                To = new DateOnly(2026, 10, 2),
                Price = 200m
            });

            second.Should().Be(2);
            (await ctx.RoomRates.CountAsync()).Should().Be(2);
            (await ctx.RoomRates.ToListAsync()).Should().OnlyContain(r => r.Price == 200m);
        }
    }

    // ------------------------------------------------------------------ Onboarding

    public class OnboardingServiceTests
    {
        [Fact]
        public async Task GetStatus_CompleteHotel_AllComplete()
        {
            var ctx = SeedCompleteHotel();

            var dto = await new OnboardingService(ctx).GetStatusAsync(HotelId);

            dto.AllComplete.Should().BeTrue();
            dto.CompletedSteps.Should().Be(4);
            dto.HasRoomTypes.Should().BeTrue();
            dto.HasRatePlan.Should().BeTrue();
            dto.HasChannel.Should().BeTrue();
        }

        [Fact]
        public async Task GetStatus_EmptyHotel_NotComplete()
        {
            var ctx = NewContext();
            ctx.Hotels.Add(MinimalHotel());
            await ctx.SaveChangesAsync();

            var dto = await new OnboardingService(ctx).GetStatusAsync(HotelId);

            dto.AllComplete.Should().BeFalse();
            dto.CompletedSteps.Should().Be(1);
            dto.HasRoomTypes.Should().BeFalse();
        }

        [Fact]
        public async Task GetStatus_UnknownHotel_ThrowsKeyNotFound()
        {
            var ctx = NewContext();

            var act = async () => await new OnboardingService(ctx)
                .GetStatusAsync(Guid.Parse("99999999-9999-9999-9999-999999999999"));

            await act.Should().ThrowAsync<KeyNotFoundException>();
        }

        private static IApplicationDbContext SeedCompleteHotel()
        {
            var ctx = NewContext();
            ctx.Hotels.Add(MinimalHotel());
            ctx.RatePlans.Add(new RatePlan
            {
                Id = RatePlanId,
                HotelId = HotelId,
                Name = "Flexible",
                Multiplier = 1m,
                MinStay = 1,
                Refundability = RefundabilityType.Flexible,
                CancellationDeadlineHours = 24,
                IsDefault = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
            ctx.RoomTypes.Add(new RoomType
            {
                Id = RoomTypeId,
                HotelId = HotelId,
                Name = "Doble",
                BasePrice = 100m,
                Capacity = 2,
                RatePlanId = RatePlanId
            });
            ctx.Rooms.Add(new Room
            {
                Id = RoomId,
                HotelId = HotelId,
                RoomTypeId = RoomTypeId,
                RoomNumber = "101",
                Floor = 1,
                Status = RoomStatus.Available,
                IsDeleted = false,
                IsClean = true
            });
            ctx.Channels.Add(new Channel
            {
                Id = ChannelId,
                HotelId = HotelId,
                Name = "Booking.com (Demo)",
                ChannelType = "Ota",
                CommissionRate = 0.15m,
                IsActive = true,
                CredentialsJson = "{}"
            });
            ctx.SaveChangesAsync().GetAwaiter().GetResult();
            return ctx;
        }
    }

    // ------------------------------------------------------------------ Channels

    public class ChannelManagerServiceTests
    {
        private sealed class FakeAdapter : IChannelAdapter
        {
            public string ChannelType => "Ota";
            public string DisplayName => "Booking.com (Demo)";
            public int PullCalls { get; private set; }

            public Task<bool> TestConnectionAsync(string credentialsJson) => Task.FromResult(true);

            public Task<Dictionary<string, ChannelRoomTypeMap>> FetchRoomTypeMapsAsync(string credentialsJson)
                => Task.FromResult(new Dictionary<string, ChannelRoomTypeMap>
                {
                    ["DBL_STD"] = new ChannelRoomTypeMap("DBL_STD", "FLEX")
                });

            public Task PushAvailabilityAsync(string credentialsJson, IEnumerable<AvailabilityUpdate> updates) => Task.CompletedTask;
            public Task PushRatesAsync(string credentialsJson, IEnumerable<RateUpdate> updates) => Task.CompletedTask;

            public Task<List<BookingPullResult>> PullBookingsAsync(string credentialsJson, DateTime from, DateTime to)
            {
                PullCalls++;
                return Task.FromResult(new List<BookingPullResult>
                {
                    new BookingPullResult(
                        ExternalBookingId: "BK-OTA-001",
                        ChannelRoomCode: "DBL_STD",
                        ChannelRatePlanCode: "FLEX",
                        CheckIn: new DateTime(2026, 10, 1),
                        CheckOut: new DateTime(2026, 10, 3),
                        Adults: 2,
                        Children: 0,
                        GuestName: "Ana Pérez",
                        GuestEmail: "ana.perez@test.dev",
                        GuestPhone: "+18091234567",
                        TotalPrice: 200m,
                        Currency: "USD",
                        RawData: new Dictionary<string, object>())
                });
            }

            public Task<bool> AcknowledgeBookingAsync(string credentialsJson, string externalBookingId, bool confirmed)
                => Task.FromResult(true);
        }

        private static ChannelManagerService NewService(
            IApplicationDbContext ctx,
            FakeAdapter adapter,
            out IAutomationService automation)
        {
            var automationMock = new Mock<IAutomationService>();
            automationMock.Setup(a => a.FireAutomationAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<Guid>()))
                .Returns(Task.CompletedTask);
            automation = automationMock.Object;
            var factory = new ChannelAdapterFactory(new IChannelAdapter[] { adapter });
            return new ChannelManagerService(ctx, factory, automation);
        }

        private static IApplicationDbContext SeedChannel(bool activeMappings = true)
        {
            var ctx = NewContext();
            ctx.Hotels.Add(MinimalHotel());
            ctx.RatePlans.Add(new RatePlan
            {
                Id = RatePlanId,
                HotelId = HotelId,
                Name = "Flexible",
                Multiplier = 1m,
                MinStay = 1,
                Refundability = RefundabilityType.Flexible,
                CancellationDeadlineHours = 24,
                IsDefault = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
            ctx.RoomTypes.Add(new RoomType
            {
                Id = RoomTypeId,
                HotelId = HotelId,
                Name = "Doble",
                BasePrice = 100m,
                Capacity = 2,
                RatePlanId = RatePlanId
            });
            ctx.Rooms.Add(new Room
            {
                Id = RoomId,
                HotelId = HotelId,
                RoomTypeId = RoomTypeId,
                RoomNumber = "101",
                Floor = 1,
                Status = RoomStatus.Available,
                IsDeleted = false,
                IsClean = true
            });
            ctx.Channels.Add(new Channel
            {
                Id = ChannelId,
                HotelId = HotelId,
                Name = "Booking.com (Demo)",
                ChannelType = "Ota",
                CommissionRate = 0.15m,
                IsActive = true,
                CredentialsJson = "{\"username\":\"demo\"}"
            });
            ctx.ChannelMappings.Add(new ChannelMapping
            {
                Id = Guid.NewGuid(),
                ChannelId = ChannelId,
                RoomTypeId = RoomTypeId,
                ChannelRoomCode = "DBL_STD",
                ChannelRatePlanCode = "FLEX",
                IsActive = activeMappings
            });
            ctx.SaveChangesAsync().GetAwaiter().GetResult();
            return ctx;
        }

        [Fact]
        public async Task ImportBookings_SeconRun_SkipsDuplicate()
        {
            var ctx = SeedChannel();
            var adapter = new FakeAdapter();
            var svc = NewService(ctx, adapter, out _);

            var first = await svc.ImportBookingsAsync(ChannelId, new DateTime(2026, 9, 30), new DateTime(2026, 10, 3));
            first.Should().ContainSingle();
            first[0].Status.Should().Be("Imported");
            (await ctx.Reservations.CountAsync()).Should().Be(1);

            var second = await svc.ImportBookingsAsync(ChannelId, new DateTime(2026, 9, 30), new DateTime(2026, 10, 3));
            second.Should().ContainSingle();
            second[0].Status.Should().Be("Skipped");
            second[0].Message.Should().Contain("Ya importada");
            (await ctx.Reservations.CountAsync()).Should().Be(1);
            adapter.PullCalls.Should().Be(2);
        }

        [Fact]
        public async Task ImportBookings_NoActiveMappings_ReturnsErrorStatus()
        {
            var ctx = SeedChannel(activeMappings: false);
            var svc = NewService(ctx, new FakeAdapter(), out _);

            var result = await svc.ImportBookingsAsync(ChannelId, new DateTime(2026, 9, 30), new DateTime(2026, 10, 3));

            result.Should().ContainSingle();
            result[0].Status.Should().Be("Error");
            result[0].Message.Should().Contain("mapeos activos");
            (await ctx.Reservations.CountAsync()).Should().Be(0);
        }

        [Fact]
        public async Task ImportBookings_UnknownChannel_ThrowsKeyNotFound()
        {
            var ctx = NewContext();
            var svc = NewService(ctx, new FakeAdapter(), out _);

            var act = async () => await svc.ImportBookingsAsync(ChannelId, new DateTime(2026, 9, 30), new DateTime(2026, 10, 3));

            await act.Should().ThrowAsync<KeyNotFoundException>();
        }
    }

    // ------------------------------------------------------------------ Organizations

    public class OrganizationServiceTests
    {
        private sealed class FakeUserStore :
            IUserStore<ApplicationUser>,
            IUserEmailStore<ApplicationUser>,
            IUserPasswordStore<ApplicationUser>,
            IUserSecurityStampStore<ApplicationUser>,
            IUserRoleStore<ApplicationUser>,
            IQueryableUserStore<ApplicationUser>
        {
            private readonly List<ApplicationUser> _users = new();

            public Func<string, Task<ApplicationUser?>>? FindByEmailHandler { get; set; }
            public Func<string, Task<ApplicationUser?>>? FindByNameHandler { get; set; }

            public IQueryable<ApplicationUser> Users => _users.AsQueryable();

            public Task<string> GetUserIdAsync(ApplicationUser user, CancellationToken ct) => Task.FromResult(user.Id);
            public Task<string?> GetUserNameAsync(ApplicationUser user, CancellationToken ct) => Task.FromResult<string?>(user.UserName);
            public Task SetUserNameAsync(ApplicationUser user, string? userName, CancellationToken ct)
            {
                user.UserName = userName;
                return Task.CompletedTask;
            }

            public Task<string?> GetNormalizedUserNameAsync(ApplicationUser user, CancellationToken ct)
                => Task.FromResult(user.NormalizedUserName);

            public Task SetNormalizedUserNameAsync(ApplicationUser user, string? normalizedName, CancellationToken ct)
            {
                user.NormalizedUserName = normalizedName;
                return Task.CompletedTask;
            }

            public Task<IdentityResult> CreateAsync(ApplicationUser user, CancellationToken ct)
            {
                user.Id ??= $"new-{Guid.NewGuid():N}";
                _users.Add(user);
                return Task.FromResult(IdentityResult.Success);
            }

            public Task<IdentityResult> UpdateAsync(ApplicationUser user, CancellationToken ct) => Task.FromResult(IdentityResult.Success);
            public Task<IdentityResult> DeleteAsync(ApplicationUser user, CancellationToken ct) => Task.FromResult(IdentityResult.Success);

            public Task<ApplicationUser?> FindByIdAsync(string userId, CancellationToken ct) =>
                Task.FromResult(_users.FirstOrDefault(u => u.Id == userId));

            public Task<ApplicationUser?> FindByNameAsync(string normalizedUserName, CancellationToken ct) =>
                FindByNameHandler is null
                    ? Task.FromResult(_users.FirstOrDefault(u => u.NormalizedUserName == normalizedUserName))
                    : FindByNameHandler(normalizedUserName);

            public Task<ApplicationUser?> FindByEmailAsync(string normalizedEmail, CancellationToken ct) =>
                FindByEmailHandler is null
                    ? Task.FromResult(_users.FirstOrDefault(u => u.NormalizedEmail == normalizedEmail))
                    : FindByEmailHandler(normalizedEmail);

            public Task<string?> GetEmailAsync(ApplicationUser user, CancellationToken ct) => Task.FromResult(user.Email);
            public Task SetEmailAsync(ApplicationUser user, string? email, CancellationToken ct)
            {
                user.Email = email;
                return Task.CompletedTask;
            }

            public Task<bool> GetEmailConfirmedAsync(ApplicationUser user, CancellationToken ct) => Task.FromResult(user.EmailConfirmed);
            public Task SetEmailConfirmedAsync(ApplicationUser user, bool confirmed, CancellationToken ct)
            {
                user.EmailConfirmed = confirmed;
                return Task.CompletedTask;
            }

            public Task<string?> GetNormalizedEmailAsync(ApplicationUser user, CancellationToken ct) => Task.FromResult(user.NormalizedEmail);
            public Task SetNormalizedEmailAsync(ApplicationUser user, string? normalizedEmail, CancellationToken ct)
            {
                user.NormalizedEmail = normalizedEmail;
                return Task.CompletedTask;
            }

            public Task<string?> GetPasswordHashAsync(ApplicationUser user, CancellationToken ct) => Task.FromResult(user.PasswordHash);
            public Task SetPasswordHashAsync(ApplicationUser user, string? passwordHash, CancellationToken ct)
            {
                user.PasswordHash = passwordHash;
                return Task.CompletedTask;
            }

            public Task<bool> HasPasswordAsync(ApplicationUser user, CancellationToken ct) => Task.FromResult(!string.IsNullOrEmpty(user.PasswordHash));

            public Task<string?> GetSecurityStampAsync(ApplicationUser user, CancellationToken ct) => Task.FromResult(user.SecurityStamp);
            public Task SetSecurityStampAsync(ApplicationUser user, string stamp, CancellationToken ct)
            {
                user.SecurityStamp = stamp;
                return Task.CompletedTask;
            }

            public Task<IList<string>> GetRolesAsync(ApplicationUser user, CancellationToken ct) => Task.FromResult<IList<string>>(new List<string>());
            public Task<bool> IsInRoleAsync(ApplicationUser user, string roleName, CancellationToken ct) => Task.FromResult(false);
            public Task AddToRoleAsync(ApplicationUser user, string roleName, CancellationToken ct) => Task.CompletedTask;
            public Task RemoveFromRoleAsync(ApplicationUser user, string roleName, CancellationToken ct) => Task.CompletedTask;
            public Task<IList<ApplicationUser>> GetUsersInRoleAsync(string roleName, CancellationToken ct)
                => Task.FromResult<IList<ApplicationUser>>(new List<ApplicationUser>());

            public void Dispose() { }
        }

        private static UserManager<ApplicationUser> BuildUserManager(FakeUserStore store) =>
            new UserManager<ApplicationUser>(
                store,
                Options.Create(new IdentityOptions()),
                new PasswordHasher<ApplicationUser>(),
                userValidators: Array.Empty<IUserValidator<ApplicationUser>>(),
                passwordValidators: Array.Empty<IPasswordValidator<ApplicationUser>>(),
                keyNormalizer: new UpperInvariantLookupNormalizer(),
                errors: new IdentityErrorDescriber(),
                services: new Mock<IServiceProvider>().Object,
                logger: new Mock<ILogger<UserManager<ApplicationUser>>>().Object);

        private static OrganizationService NewService(
            FakeUserStore store,
            out IApplicationDbContext ctx,
            string? currentUser = UserId)
        {
            ctx = NewContext();
            ctx.Organizations.Add(new Organization
            {
                Id = OrgId,
                Name = "Repro Inversiones",
                BusinessName = "Repro Inversiones SRL",
                Plan = "small",
                DefaultCurrency = "DOP",
                TimeZone = "America/Santo_Domingo",
                IsActive = true,
                IsDeleted = false,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
            ctx.Hotels.Add(MinimalHotel());
            ctx.SaveChangesAsync().GetAwaiter().GetResult();

            var userMock = new Mock<ICurrentUserService>();
            userMock.Setup(u => u.UserId).Returns(currentUser);

            if (currentUser is not null)
            {
                ctx.OrganizationMembers.Add(new OrganizationMember
                {
                    Id = Guid.NewGuid(),
                    OrganizationId = OrgId,
                    UserId = currentUser,
                    OrganizationRole = "Owner",
                    IsActive = true,
                    IsDeleted = false,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                });
                ctx.SaveChangesAsync().GetAwaiter().GetResult();
            }

            return new OrganizationService(ctx, userMock.Object, BuildUserManager(store),
                new Mock<ILogger<OrganizationService>>().Object);
        }

        private static InviteMemberCommand ValidInvite() => new()
        {
            Email = "new@test.dev",
            FirstName = "Nuevo",
            LastName = "Miembro",
            OrganizationRole = "Owner",
            Properties = new List<PropertyAssignmentCommand>
            {
                new() { PropertyId = HotelId, PropertyRole = "Manager" }
            }
        };

        [Fact]
        public async Task InviteMember_ValidCommand_CreatesMembershipAndAssignment()
        {
            var store = new FakeUserStore();
            var svc = NewService(store, out var ctx);

            var result = await svc.InviteMemberAsync(ValidInvite());

            result.TemporaryPassword.Should().NotBeNullOrEmpty();
            result.Member.UserId.Should().NotBeNullOrEmpty();
            result.Member.Properties.Should().ContainSingle(a => a.PropertyId == HotelId);

            var member = await ctx.OrganizationMembers
                .SingleAsync(m => m.UserId == result.Member.UserId);
            member.OrganizationRole.Should().Be("Owner");

            var assignment = await ctx.PropertyAssignments
                .SingleAsync(a => a.UserId == result.Member.UserId);
            assignment.PropertyId.Should().Be(HotelId);
            assignment.PropertyRole.Should().Be("Manager");
        }

        [Fact]
        public async Task InviteMember_DuplicateEmail_ThrowsConflict()
        {
            var store = new FakeUserStore
            {
                FindByEmailHandler = _ => Task.FromResult<ApplicationUser?>(new ApplicationUser { Id = "existing", Email = "new@test.dev" })
            };
            var svc = NewService(store, out _);

            var act = async () => await svc.InviteMemberAsync(ValidInvite());

            await act.Should().ThrowAsync<ConflictException>()
                .WithMessage("*cuenta registrada*");
        }

        [Fact]
        public async Task InviteMember_EmptyEmail_ThrowsValidation()
        {
            var store = new FakeUserStore();
            var svc = NewService(store, out _);

            var invite = ValidInvite();
            invite.Email = "  ";

            var act = async () => await svc.InviteMemberAsync(invite);

            await act.Should().ThrowAsync<ValidationException>()
                .WithMessage("*correo*obligatorio*");
        }

        [Fact]
        public async Task InviteMember_UserWithoutMembership_ThrowsForbidden()
        {
            var store = new FakeUserStore();
            var svc = NewService(store, out _, currentUser: null);

            var act = async () => await svc.InviteMemberAsync(ValidInvite());

            await act.Should().ThrowAsync<ForbiddenAccessException>();
        }
    }

    // ------------------------------------------------------------------ Helpers

    private static Hotel MinimalHotel() => new()
    {
        Id = HotelId,
        OrganizationId = OrgId,
        Name = "Repro",
        Address = "Calle 1",
        PhoneNumber = "809-000-0000",
        Email = "repro@test.dev",
        City = "Santo Domingo",
        Country = "DO",
        Currency = "DOP",
        TaxRate = 0.18m,
        IsActive = true,
        IsDeleted = false,
        StarRating = 4,
        TotalRooms = 10,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow
    };
}