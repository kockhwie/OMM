using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using OMM.Public.Data;
using OMM.Public.Data.Entities;
using OMM.Public.Services;

namespace OMM.Integration.Tests;

public sealed class PublicProfileTests
{
    [Fact]
    public async Task CreateIfMissing_is_idempotent_and_creates_no_financial_records()
    {
        await using var fixture = await ProfileFixture.CreateAsync("user-1", "one@example.com");
        var service = fixture.CreateService();

        var first = await service.CreateIfMissingAsync();
        var second = await service.CreateIfMissingAsync();

        Assert.Equal(first.UserId, second.UserId);
        await using var db = fixture.CreateContext();
        Assert.Equal(1, await db.MinerProfiles.CountAsync());
        Assert.Empty(await db.Mines.ToListAsync());
        Assert.Empty(await db.IncomeRecords.ToListAsync());
        Assert.Empty(await db.Burdens.ToListAsync());
        Assert.Empty(await db.Expenses.ToListAsync());
        Assert.Empty(await db.Goals.ToListAsync());
        Assert.Empty(await db.Notifications.ToListAsync());
    }

    [Fact]
    public async Task Existing_identity_user_can_receive_a_profile_without_financial_records()
    {
        await using var fixture = await ProfileFixture.CreateAsync("existing-user", "existing@example.com");
        var service = fixture.CreateService();

        var profile = await service.CreateIfMissingAsync();

        Assert.Equal("existing-user", profile.UserId);
        Assert.Equal("existing@example.com", profile.Email);
        Assert.False(profile.IsComplete);
        await using var db = fixture.CreateContext();
        Assert.Equal(1, await db.MinerProfiles.CountAsync());
    }

    [Fact]
    public async Task Profile_reads_are_scoped_to_the_authenticated_user()
    {
        await using var fixture = await ProfileFixture.CreateAsync("user-1", "one@example.com");
        await fixture.AddUserAsync("user-2", "two@example.com", "Other User");
        var service = fixture.CreateService();
        await service.CreateIfMissingAsync();

        var profile = await service.GetCurrentAsync();

        Assert.NotNull(profile);
        Assert.Equal("user-1", profile.UserId);
        Assert.NotEqual("Other User", profile.DisplayName);
    }

    [Fact]
    public async Task Profile_update_cannot_target_another_users_profile()
    {
        await using var fixture = await ProfileFixture.CreateAsync("user-1", "one@example.com", "Owner");
        await fixture.AddUserAsync("user-2", "two@example.com", "Other User");
        var service = fixture.CreateService();

        await service.UpdateCurrentAsync(new MinerProfileUpdate("Updated owner", null, null, "English"));

        await using var db = fixture.CreateContext();
        Assert.Equal("Updated owner", await db.MinerProfiles
            .Where(profile => profile.UserId == "user-1")
            .Select(profile => profile.DisplayName)
            .SingleAsync());
        Assert.Equal("Other User", await db.MinerProfiles
            .Where(profile => profile.UserId == "user-2")
            .Select(profile => profile.DisplayName)
            .SingleAsync());
    }

    private sealed class ProfileFixture : IAsyncDisposable
    {
        private readonly DbContextOptions<ApplicationDbContext> options;
        private readonly TestAuthenticationStateProvider authenticationStateProvider;

        private ProfileFixture(
            DbContextOptions<ApplicationDbContext> options,
            TestAuthenticationStateProvider authenticationStateProvider)
        {
            this.options = options;
            this.authenticationStateProvider = authenticationStateProvider;
        }

        public static async Task<ProfileFixture> CreateAsync(
            string userId,
            string email,
            string? displayName = null)
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase($"profile-tests-{Guid.NewGuid():N}")
                .Options;
            await using var db = new ApplicationDbContext(options);
            await db.Database.EnsureCreatedAsync();
            db.Users.Add(new ApplicationUser { Id = userId, UserName = email, Email = email });
            await db.SaveChangesAsync();

            var fixture = new ProfileFixture(options, CreateAuthProvider(userId));
            if (displayName is not null)
            {
                db.MinerProfiles.Add(new MinerProfileEntity
                {
                    UserId = userId,
                    Email = email,
                    DisplayName = displayName,
                    CreatedAt = DateTimeOffset.UtcNow
                });
                await db.SaveChangesAsync();
            }

            return fixture;
        }

        public async Task AddUserAsync(string userId, string email, string displayName)
        {
            await using var db = CreateContext();
            db.Users.Add(new ApplicationUser { Id = userId, UserName = email, Email = email });
            db.MinerProfiles.Add(new MinerProfileEntity
            {
                UserId = userId,
                Email = email,
                DisplayName = displayName,
                CreatedAt = DateTimeOffset.UtcNow
            });
            await db.SaveChangesAsync();
        }

        public MinerProfileService CreateService() => new(
            new TestDbContextFactory(options),
            authenticationStateProvider,
            NullLogger<MinerProfileService>.Instance);

        public ApplicationDbContext CreateContext() => new(options);

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

    private sealed class TestAuthenticationStateProvider(ClaimsPrincipal user)
        : AuthenticationStateProvider
    {
        public override Task<AuthenticationState> GetAuthenticationStateAsync() =>
            Task.FromResult(new AuthenticationState(user));
    }
}
