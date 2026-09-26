using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.EntityFrameworkCore;
using OMM.Public.Data;
using OMM.Public.Data.Entities;
using OMM.Public.Models;
using OMM.Public.Services;

namespace OMM.Integration.Tests;

public sealed class PublicNotificationTests
{
    [Fact]
    public async Task Generation_is_deterministic_and_persisted()
    {
        await using var fixture = await Fixture.CreateAsync("user-1");
        await fixture.AddPositionAsync("user-1", DateOnly.FromDateTime(DateTime.UtcNow.AddDays(7)));
        var service = fixture.CreateService();

        await service.GenerateAsync();
        await service.GenerateAsync();

        Assert.Single(await service.GetAsync());
    }

    [Fact]
    public async Task Read_and_dismiss_state_survive_reload_and_are_scoped()
    {
        await using var fixture = await Fixture.CreateAsync("user-1");
        await fixture.AddPositionAsync("user-1", DateOnly.FromDateTime(DateTime.UtcNow.AddDays(7)));
        var service = fixture.CreateService();
        await service.GenerateAsync();
        var id = (await service.GetAsync()).Single().Id;

        Assert.True(await service.MarkReadAsync(id));
        Assert.True((await service.GetAsync()).Single().Read);
        Assert.True(await service.DismissAsync(id));
        Assert.Empty(await service.GetAsync());

        await fixture.SetUserAsync("user-2");
        Assert.False(await service.MarkReadAsync(id));
    }

    private sealed class Fixture : IAsyncDisposable
    {
        private readonly DbContextOptions<ApplicationDbContext> options;
        private readonly TestAuth auth;
        private Fixture(DbContextOptions<ApplicationDbContext> options, TestAuth auth) { this.options = options; this.auth = auth; }

        public static async Task<Fixture> CreateAsync(string userId)
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase($"notification-tests-{Guid.NewGuid():N}").Options;
            await using var db = new ApplicationDbContext(options);
            await db.Database.EnsureCreatedAsync();
            db.Users.AddRange(
                new ApplicationUser { Id = userId, UserName = $"{userId}@example.com", Email = $"{userId}@example.com" },
                new ApplicationUser { Id = "user-2", UserName = "user-2@example.com", Email = "user-2@example.com" });
            await db.SaveChangesAsync();
            return new Fixture(options, new TestAuth(new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, userId)], "Test"))));
        }

        public async Task AddPositionAsync(string userId, DateOnly maturityDate)
        {
            await using var db = new ApplicationDbContext(options);
            var mine = new MineEntity { UserId = userId, Name = "Mine", Category = MineCategory.Investments, Type = MineType.Stocks, CurrencyId = 1, Status = "active", UpdatedOn = DateOnly.FromDateTime(DateTime.UtcNow), CreatedAt = DateTimeOffset.UtcNow };
            db.Mines.Add(mine);
            await db.SaveChangesAsync();
            db.MinePositions.Add(new MinePositionEntity { UserId = userId, MineId = mine.Id, Label = "Fixed deposit", PositionType = PositionType.FixedDeposit, MaturityDate = maturityDate, CreatedAt = DateTimeOffset.UtcNow });
            await db.SaveChangesAsync();
        }

        public INotificationService CreateService() => new NotificationService(new Factory(options), auth);
        public Task SetUserAsync(string userId) { auth.SetUser(new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, userId)], "Test"))); return Task.CompletedTask; }
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class Factory(DbContextOptions<ApplicationDbContext> options) : IDbContextFactory<ApplicationDbContext>
    {
        public ApplicationDbContext CreateDbContext() => new(options);
        public Task<ApplicationDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) => Task.FromResult(new ApplicationDbContext(options));
    }

    private sealed class TestAuth(ClaimsPrincipal initialUser) : AuthenticationStateProvider
    {
        private ClaimsPrincipal user = initialUser;
        public override Task<AuthenticationState> GetAuthenticationStateAsync() => Task.FromResult(new AuthenticationState(user));
        public void SetUser(ClaimsPrincipal next) => user = next;
    }
}
