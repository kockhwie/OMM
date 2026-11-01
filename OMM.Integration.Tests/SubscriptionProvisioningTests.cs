using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using OMM.Public.Data;
using OMM.Shared.Infrastructure.Settings;
using OMM.Shared.Models.Phase5;
using OMM.Shared.Subscription;

namespace OMM.Integration.Tests;

public class SubscriptionProvisioningTests
{
    private static (DbContextOptions<ApplicationDbContext> Options, ISubscriptionDbContextFactory Factory) CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        using (var context = new ApplicationDbContext(options))
        {
            context.Database.EnsureCreated();

            if (!context.AiPromptModes.Any())
            {
                context.AiPromptModes.AddRange(
                    new AiPromptMode { Code = "STANDARD", DisplayName = "Standard", IsAdvisory = false, IsActive = true, SchemaVersion = "v1" },
                    new AiPromptMode { Code = "PRO", DisplayName = "Pro", IsAdvisory = true, IsActive = true, SchemaVersion = "v1" });
                context.SubscriptionPlans.AddRange(
                    new SubscriptionPlan { Id = 1, Code = "STANDARD", DisplayName = "Standard", Description = "Lifetime educational", PromptModeCode = "STANDARD", DurationMonths = null, PriceAmount = null, CurrencyCode = null, IsActive = true, SortOrder = 1 },
                    new SubscriptionPlan { Id = 2, Code = "PRO", DisplayName = "Pro", Description = "Personalized Pro Advice", PromptModeCode = "PRO", DurationMonths = 12, PriceAmount = 99m, CurrencyCode = "MYR", IsActive = true, SortOrder = 2 });
                context.SaveChanges();
            }
        }

        var factory = new TestSubscriptionDbContextFactory(options);
        return (options, factory);
    }

    private sealed class TestSubscriptionDbContextFactory(DbContextOptions<ApplicationDbContext> options) : ISubscriptionDbContextFactory
    {
        public Task<ISubscriptionDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult<ISubscriptionDbContext>(new ApplicationDbContext(options));
        }
    }

    private sealed class StubSystemSettingsService : ISystemSettingsService
    {
        public Task<string?> GetSettingAsync(string key, CancellationToken ct = default) => Task.FromResult<string?>(null);
        public Task<bool> GetBoolSettingAsync(string key, bool defaultValue = false, CancellationToken ct = default) => Task.FromResult(defaultValue);
        public Task SetSettingAsync(string key, string value, string? userId = null, string? description = null, CancellationToken ct = default) => Task.CompletedTask;
        public Task<bool> IsGoogleAuthEnabledAsync(CancellationToken ct = default) => Task.FromResult(false);
        public Task SetGoogleAuthEnabledAsync(bool enabled, string? userId = null, CancellationToken ct = default) => Task.CompletedTask;
        public Task<bool> IsAiClassificationEnabledAsync(CancellationToken ct = default) => Task.FromResult(true);
        public Task SetAiClassificationEnabledAsync(bool enabled, string? userId = null, CancellationToken ct = default) => Task.CompletedTask;
        public Task<bool> IsPublicProEnabledAsync(CancellationToken ct = default) => Task.FromResult(true);
        public Task SetPublicProEnabledAsync(bool enabled, string? userId = null, CancellationToken ct = default) => Task.CompletedTask;
    }

    [Fact]
    public async Task Provisioning_creates_standard_lifetime_subscription_and_change_event()
    {
        var (options, factory) = CreateContext();
        var userId = Guid.NewGuid().ToString();

        var entitlementService = new SubscriptionEntitlementService(
            factory,
            new StubSystemSettingsService(),
            new MemoryCache(new MemoryCacheOptions()),
            TimeProvider.System,
            NullLogger<SubscriptionEntitlementService>.Instance);

        var provisioningService = new SubscriptionProvisioningService(
            factory,
            entitlementService,
            TimeProvider.System,
            NullLogger<SubscriptionProvisioningService>.Instance);

        var subscription = await provisioningService.ProvisionStandardSubscriptionAsync(userId);

        Assert.NotNull(subscription);
        Assert.Equal(userId, subscription.UserId);
        Assert.Equal(1, subscription.SubscriptionPlanId);
        Assert.Equal("Active", subscription.Status);
        Assert.Equal("Current", subscription.EntitlementSlot);
        Assert.Null(subscription.SubscriptionExpiryDate);
        Assert.False(subscription.IsPaid);
        Assert.False(subscription.ManuallyGranted);

        await using var verifyContext = new ApplicationDbContext(options);
        var changeEvent = await verifyContext.SubscriptionChangeEvents
            .FirstOrDefaultAsync(e => e.SubscriptionId == subscription.Id);

        Assert.NotNull(changeEvent);
        Assert.Equal("Provisioned", changeEvent.Action);
        Assert.Equal("Member", changeEvent.ActorType);
        Assert.Equal(userId, changeEvent.ActorId);
    }

    [Fact]
    public async Task Provisioning_is_idempotent()
    {
        var (options, factory) = CreateContext();
        var userId = Guid.NewGuid().ToString();

        var entitlementService = new SubscriptionEntitlementService(
            factory,
            new StubSystemSettingsService(),
            new MemoryCache(new MemoryCacheOptions()),
            TimeProvider.System,
            NullLogger<SubscriptionEntitlementService>.Instance);

        var provisioningService = new SubscriptionProvisioningService(
            factory,
            entitlementService,
            TimeProvider.System,
            NullLogger<SubscriptionProvisioningService>.Instance);

        var sub1 = await provisioningService.ProvisionStandardSubscriptionAsync(userId);
        var sub2 = await provisioningService.ProvisionStandardSubscriptionAsync(userId);

        Assert.Equal(sub1.Id, sub2.Id);

        await using var verifyContext = new ApplicationDbContext(options);
        var totalSubs = await verifyContext.MemberSubscriptions
            .CountAsync(s => s.UserId == userId);
        Assert.Equal(1, totalSubs);
    }
}
