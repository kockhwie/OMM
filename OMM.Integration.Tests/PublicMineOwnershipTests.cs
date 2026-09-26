using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.EntityFrameworkCore;
using OMM.Public.Data;
using OMM.Public.Data.Entities;
using OMM.Public.Services;
using OMM.Public.Models;

namespace OMM.Integration.Tests;

public sealed class PublicMineOwnershipTests
{
    [Fact]
    public async Task Add_mine_overwrites_submitted_owner_and_reads_only_current_users_mines()
    {
        await using var fixture = await MineFixture.CreateAsync("user-1");
        var repository = fixture.CreateRepository();

        var mine = await repository.AddMineAsync(fixture.NewMine(userId: "another-user"));

        Assert.Equal("user-1", mine.UserId);
        Assert.Single(await repository.GetMinesAsync());
        await fixture.SetCurrentUserAsync("user-2");
        Assert.Empty(await repository.GetMinesAsync());
    }

    [Fact]
    public async Task Mine_lookup_and_delete_cannot_cross_user_boundary()
    {
        await using var fixture = await MineFixture.CreateAsync("user-1");
        var repository = fixture.CreateRepository();
        var mine = await repository.AddMineAsync(fixture.NewMine(userId: string.Empty));

        await fixture.SetCurrentUserAsync("user-2");

        Assert.Null(await repository.GetMineAsync(mine.Id));
        Assert.False(await repository.SoftDeleteMineAsync(mine.Id));
    }

    [Fact]
    public async Task Soft_deleted_mine_is_excluded_from_reads_but_remains_in_database()
    {
        await using var fixture = await MineFixture.CreateAsync("user-1");
        var repository = fixture.CreateRepository();
        var mine = await repository.AddMineAsync(fixture.NewMine(userId: string.Empty));

        Assert.True(await repository.SoftDeleteMineAsync(mine.Id));
        Assert.Empty(await repository.GetMinesAsync());

        await using var db = fixture.CreateContext();
        Assert.True(await db.Mines.IgnoreQueryFilters().Where(item => item.Id == mine.Id).Select(item => item.IsDeleted).SingleAsync());
    }

    [Fact]
    public async Task Position_requires_a_mine_owned_by_current_user()
    {
        await using var fixture = await MineFixture.CreateAsync("user-1");
        var repository = fixture.CreateRepository();
        var mine = await repository.AddMineAsync(fixture.NewMine(userId: string.Empty));

        var position = await repository.AddPositionAsync(new MinePositionEntity
        {
            UserId = "another-user",
            MineId = mine.Id,
            Label = "Lot 1",
            PositionType = PositionType.StockLot
        });

        Assert.Equal("user-1", position.UserId);
        await fixture.SetCurrentUserAsync("user-2");
        Assert.Null(await repository.GetPositionAsync(mine.Id, position.Id));
        Assert.False(await repository.SoftDeletePositionAsync(mine.Id, position.Id));
    }

    [Fact]
    public async Task Updates_are_scoped_to_the_current_user()
    {
        await using var fixture = await MineFixture.CreateAsync("user-1");
        var repository = fixture.CreateRepository();
        var mine = await repository.AddMineAsync(fixture.NewMine(userId: string.Empty));

        mine.Name = "Updated mine";
        Assert.NotNull(await repository.UpdateMineAsync(mine));

        await fixture.SetCurrentUserAsync("user-2");
        mine.Name = "Cross-user update";
        Assert.Null(await repository.UpdateMineAsync(mine));
    }

    private sealed class MineFixture : IAsyncDisposable
    {
        private readonly DbContextOptions<ApplicationDbContext> options;
        private readonly TestAuthenticationStateProvider authenticationStateProvider;

        private MineFixture(
            DbContextOptions<ApplicationDbContext> options,
            TestAuthenticationStateProvider authenticationStateProvider)
        {
            this.options = options;
            this.authenticationStateProvider = authenticationStateProvider;
        }

        public static async Task<MineFixture> CreateAsync(string userId)
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase($"mine-tests-{Guid.NewGuid():N}")
                .Options;
            await using var db = new ApplicationDbContext(options);
            await db.Database.EnsureCreatedAsync();
            db.Users.Add(new ApplicationUser { Id = userId, UserName = $"{userId}@example.com", Email = $"{userId}@example.com" });
            db.Users.Add(new ApplicationUser { Id = "user-2", UserName = "user-2@example.com", Email = "user-2@example.com" });
            await db.SaveChangesAsync();
            return new MineFixture(options, CreateAuthProvider(userId));
        }

        public MineEntity NewMine(string userId) => new()
        {
            UserId = userId,
            Name = "Test mine",
            Category = MineCategory.Investments,
            Type = MineType.Stocks,
            CurrencyId = 1,
            Status = "active",
            UpdatedOn = DateOnly.FromDateTime(DateTime.UtcNow)
        };

        public IMineRepository CreateRepository() => new MineRepository(
            new TestDbContextFactory(options),
            authenticationStateProvider);

        public ApplicationDbContext CreateContext() => new(options);

        public async Task SetCurrentUserAsync(string userId)
        {
            authenticationStateProvider.SetUser(new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim(ClaimTypes.NameIdentifier, userId)],
                authenticationType: "Test")));
            await Task.CompletedTask;
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;

        private static TestAuthenticationStateProvider CreateAuthProvider(string userId) =>
            new(new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim(ClaimTypes.NameIdentifier, userId)],
                authenticationType: "Test")));
    }

    private sealed class TestDbContextFactory(DbContextOptions<ApplicationDbContext> options)
        : IDbContextFactory<ApplicationDbContext>
    {
        public ApplicationDbContext CreateDbContext() => new(options);

        public Task<ApplicationDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new ApplicationDbContext(options));
    }

    private sealed class TestAuthenticationStateProvider(ClaimsPrincipal initialUser)
        : AuthenticationStateProvider
    {
        private ClaimsPrincipal user = initialUser;

        public override Task<AuthenticationState> GetAuthenticationStateAsync() =>
            Task.FromResult(new AuthenticationState(user));

        public void SetUser(ClaimsPrincipal nextUser) => user = nextUser;
    }
}
