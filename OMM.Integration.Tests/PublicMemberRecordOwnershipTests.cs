using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using OMM.Public.Data;
using OMM.Public.Data.Entities;
using OMM.Public.Models;
using OMM.Public.Services;

namespace OMM.Integration.Tests;

public sealed class PublicMemberRecordOwnershipTests
{
    [Theory]
    [InlineData(1200, RecordFrequency.Monthly, 1200)]
    [InlineData(1200, RecordFrequency.Annual, 100)]
    [InlineData(1200, RecordFrequency.OneOff, 0)]
    public void Monthly_equivalent_follows_approved_frequency_rules(
        decimal amount,
        RecordFrequency frequency,
        decimal expected)
    {
        Assert.Equal(expected, MemberRecordService.MonthlyEquivalent(amount, frequency));
    }

    [Fact]
    public async Task Income_uses_profile_currency_when_currency_is_not_submitted()
    {
        await using var fixture = await RecordFixture.CreateAsync("user-1", currencyId: 1);
        var service = fixture.CreateService();

        await service.AddIncomeRecordAsync(new IncomeRecord
        {
            Source = "Salary",
            Amount = 5000,
            Frequency = "annual"
        });

        await using var db = fixture.CreateContext();
        var record = await db.IncomeRecords.SingleAsync();
        Assert.Equal(1, record.CurrencyId);
        Assert.Equal(RecordFrequency.Annual, record.Frequency);
    }

    [Fact]
    public async Task Goal_links_only_current_users_mines_and_reads_are_scoped()
    {
        await using var fixture = await RecordFixture.CreateAsync("user-1", currencyId: 1);
        var mine = await fixture.AddMineAsync("user-1", "Owner mine");
        var service = fixture.CreateService();

        await service.AddGoalAsync(new Goal
        {
            Title = "Emergency fund",
            Target = 10000,
            TargetDate = "2030-01-01",
            LinkedMineIds = [mine.Id.ToString()]
        });

        await using var db = fixture.CreateContext();
        Assert.Single(await db.GoalMines.ToListAsync());
        Assert.Single(await service.GetGoalsAsync());

        await fixture.SetCurrentUserAsync("user-2");
        Assert.Empty(await service.GetGoalsAsync());
    }

    [Fact]
    public async Task Goal_cannot_link_another_users_mine()
    {
        await using var fixture = await RecordFixture.CreateAsync("user-1", currencyId: 1);
        var mine = await fixture.AddMineAsync("user-2", "Other mine");
        var service = fixture.CreateService();

        await Assert.ThrowsAsync<ArgumentException>(() => service.AddGoalAsync(new Goal
        {
            Title = "Invalid link",
            Target = 100,
            TargetDate = "2030-01-01",
            LinkedMineIds = [mine.Id.ToString()]
        }));
    }

    [Fact]
    public async Task Burden_delete_is_soft_and_user_scoped()
    {
        await using var fixture = await RecordFixture.CreateAsync("user-1", currencyId: 1);
        var service = fixture.CreateService();
        await service.AddBurdenAsync(new Burden { Name = "Loan", Balance = 1000, OriginalAmount = 1000, Currency = "USD" });
        var id = (await service.GetBurdensAsync()).Single().Id;

        Assert.True(await service.DeleteBurdenAsync(id));
        Assert.Empty(await service.GetBurdensAsync());
        await using var db = fixture.CreateContext();
        Assert.True(await db.Burdens.IgnoreQueryFilters().Where(item => item.Id.ToString() == id).Select(item => item.IsDeleted).SingleAsync());
    }

    private sealed class RecordFixture : IAsyncDisposable
    {
        private readonly DbContextOptions<ApplicationDbContext> options;
        private readonly TestAuthenticationStateProvider auth;

        private RecordFixture(DbContextOptions<ApplicationDbContext> options, TestAuthenticationStateProvider auth)
        {
            this.options = options;
            this.auth = auth;
        }

        public static async Task<RecordFixture> CreateAsync(string userId, int currencyId)
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase($"record-tests-{Guid.NewGuid():N}")
                .Options;
            await using var db = new ApplicationDbContext(options);
            await db.Database.EnsureCreatedAsync();
            db.Users.Add(new ApplicationUser { Id = userId, UserName = $"{userId}@example.com", Email = $"{userId}@example.com" });
            db.Users.Add(new ApplicationUser { Id = "user-2", UserName = "user-2@example.com", Email = "user-2@example.com" });
            db.MinerProfiles.Add(new MinerProfileEntity
            {
                UserId = userId,
                Email = $"{userId}@example.com",
                CurrencyId = currencyId,
                CreatedAt = DateTimeOffset.UtcNow
            });
            await db.SaveChangesAsync();
            return new RecordFixture(options, CreateAuth(userId));
        }

        public async Task<MineEntity> AddMineAsync(string userId, string name)
        {
            await using var db = CreateContext();
            var mine = new MineEntity
            {
                UserId = userId,
                Name = name,
                Category = MineCategory.Investments,
                Type = MineType.Stocks,
                CurrencyId = 1,
                Status = "active",
                UpdatedOn = DateOnly.FromDateTime(DateTime.UtcNow),
                CreatedAt = DateTimeOffset.UtcNow
            };
            db.Mines.Add(mine);
            await db.SaveChangesAsync();
            return mine;
        }

        public MemberRecordService CreateService() => new(
            new TestDbContextFactory(options),
            auth,
            new MinerProfileService(new TestDbContextFactory(options), auth, NullLogger<MinerProfileService>.Instance));

        public ApplicationDbContext CreateContext() => new(options);

        public Task SetCurrentUserAsync(string userId)
        {
            auth.SetUser(new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim(ClaimTypes.NameIdentifier, userId)], "Test")));
            return Task.CompletedTask;
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;

        private static TestAuthenticationStateProvider CreateAuth(string userId) => new(
            new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim(ClaimTypes.NameIdentifier, userId)], "Test")));
    }

    private sealed class TestDbContextFactory(DbContextOptions<ApplicationDbContext> options)
        : IDbContextFactory<ApplicationDbContext>
    {
        public ApplicationDbContext CreateDbContext() => new(options);
        public Task<ApplicationDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) => Task.FromResult(new ApplicationDbContext(options));
    }

    private sealed class TestAuthenticationStateProvider(ClaimsPrincipal initialUser) : AuthenticationStateProvider
    {
        private ClaimsPrincipal user = initialUser;
        public override Task<AuthenticationState> GetAuthenticationStateAsync() => Task.FromResult(new AuthenticationState(user));
        public void SetUser(ClaimsPrincipal nextUser) => user = nextUser;
    }
}
