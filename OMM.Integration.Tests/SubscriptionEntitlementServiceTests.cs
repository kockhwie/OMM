using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using OMM.Public.Data;
using OMM.Public.Services;
using OMM.Shared.Infrastructure.Settings;
using OMM.Shared.Models.Phase5;
using OMM.Shared.Subscription;

namespace OMM.Integration.Tests;

public class SubscriptionEntitlementServiceTests
{
    private static (ApplicationDbContext Context, ISubscriptionDbContextFactory Factory) CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        var context = new ApplicationDbContext(options);
        context.Database.EnsureCreated();

        // Seed basic PromptModes and Plans if not already seeded
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

        var factory = new TestSubscriptionDbContextFactory(options);
        return (context, factory);
    }

    private sealed class TestSubscriptionDbContextFactory(DbContextOptions<ApplicationDbContext> options) : ISubscriptionDbContextFactory
    {
        public Task<ISubscriptionDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult<ISubscriptionDbContext>(new ApplicationDbContext(options));
        }
    }

    private sealed class TestSystemSettingsService(bool publicProEnabled = true) : ISystemSettingsService
    {
        public bool PublicProEnabled { get; set; } = publicProEnabled;

        public Task<string?> GetSettingAsync(string key, CancellationToken ct = default) => Task.FromResult<string?>(null);
        public Task<bool> GetBoolSettingAsync(string key, bool defaultValue = false, CancellationToken ct = default)
        {
            if (key == SystemSettingKeys.PublicProEnabled)
            {
                return Task.FromResult(PublicProEnabled);
            }
            return Task.FromResult(defaultValue);
        }
        public Task SetSettingAsync(string key, string value, string? userId = null, string? description = null, CancellationToken ct = default) => Task.CompletedTask;
        public Task<bool> IsGoogleAuthEnabledAsync(CancellationToken ct = default) => Task.FromResult(false);
        public Task SetGoogleAuthEnabledAsync(bool enabled, string? userId = null, CancellationToken ct = default) => Task.CompletedTask;
        public Task<bool> IsAiClassificationEnabledAsync(CancellationToken ct = default) => Task.FromResult(true);
        public Task SetAiClassificationEnabledAsync(bool enabled, string? userId = null, CancellationToken ct = default) => Task.CompletedTask;
        public Task<bool> IsPublicProEnabledAsync(CancellationToken ct = default) => Task.FromResult(PublicProEnabled);
        public Task SetPublicProEnabledAsync(bool enabled, string? userId = null, CancellationToken ct = default)
        {
            PublicProEnabled = enabled;
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task Non_member_resolves_to_non_member_tier_without_advisory()
    {
        var (_, factory) = CreateContext();
        var service = new SubscriptionEntitlementService(
            factory,
            new TestSystemSettingsService(),
            new MemoryCache(new MemoryCacheOptions()),
            TimeProvider.System,
            NullLogger<SubscriptionEntitlementService>.Instance);

        var entitlement = await service.GetEntitlementAsync(null);

        Assert.Equal(EntitlementTier.NonMember, entitlement.Tier);
        Assert.False(entitlement.IsAdvisoryPermitted);
        Assert.Equal(string.Empty, entitlement.DisplayLabel);
    }

    [Fact]
    public async Task User_with_active_standard_lifetime_resolves_to_standard_tier()
    {
        var (context, factory) = CreateContext();
        var userId = Guid.NewGuid().ToString();

        context.MemberSubscriptions.Add(new MemberSubscription
        {
            UserId = userId,
            SubscriptionPlanId = 1,
            IsPaid = false,
            ManuallyGranted = false,
            Status = "Active",
            EntitlementSlot = "Current",
            StartedAt = DateTimeOffset.UtcNow.AddDays(-10),
            SubscriptionExpiryDate = null
        });
        await context.SaveChangesAsync();

        var service = new SubscriptionEntitlementService(
            factory,
            new TestSystemSettingsService(),
            new MemoryCache(new MemoryCacheOptions()),
            TimeProvider.System,
            NullLogger<SubscriptionEntitlementService>.Instance);

        var entitlement = await service.GetEntitlementAsync(userId);

        Assert.Equal(EntitlementTier.Standard, entitlement.Tier);
        Assert.False(entitlement.IsAdvisoryPermitted);
        Assert.Equal("Educational information only", entitlement.DisplayLabel);
        Assert.Null(entitlement.SubscriptionExpiryDate);
    }

    [Fact]
    public async Task User_with_active_paid_pro_resolves_to_pro_tier_with_personalized_label()
    {
        var (context, factory) = CreateContext();
        var userId = Guid.NewGuid().ToString();

        context.MemberSubscriptions.Add(new MemberSubscription
        {
            UserId = userId,
            SubscriptionPlanId = 2,
            IsPaid = true,
            ManuallyGranted = false,
            Status = "Active",
            EntitlementSlot = "Current",
            StartedAt = DateTimeOffset.UtcNow.AddDays(-30),
            SubscriptionExpiryDate = DateTimeOffset.UtcNow.AddDays(335)
        });
        await context.SaveChangesAsync();

        var service = new SubscriptionEntitlementService(
            factory,
            new TestSystemSettingsService(),
            new MemoryCache(new MemoryCacheOptions()),
            TimeProvider.System,
            NullLogger<SubscriptionEntitlementService>.Instance);

        var entitlement = await service.GetEntitlementAsync(userId);

        Assert.Equal(EntitlementTier.Pro, entitlement.Tier);
        Assert.True(entitlement.IsAdvisoryPermitted);
        Assert.Equal("Personalized Pro Advice", entitlement.DisplayLabel);
        Assert.True(entitlement.IsPaid);
        Assert.False(entitlement.ManuallyGranted);
    }

    [Fact]
    public async Task User_with_manually_granted_pro_resolves_to_pro_tier_without_payment()
    {
        var (context, factory) = CreateContext();
        var userId = Guid.NewGuid().ToString();

        context.MemberSubscriptions.Add(new MemberSubscription
        {
            UserId = userId,
            SubscriptionPlanId = 2,
            IsPaid = false,
            ManuallyGranted = true,
            Status = "Active",
            EntitlementSlot = "Current",
            StartedAt = DateTimeOffset.UtcNow.AddDays(-10),
            SubscriptionExpiryDate = DateTimeOffset.UtcNow.AddDays(355)
        });
        await context.SaveChangesAsync();

        var service = new SubscriptionEntitlementService(
            factory,
            new TestSystemSettingsService(),
            new MemoryCache(new MemoryCacheOptions()),
            TimeProvider.System,
            NullLogger<SubscriptionEntitlementService>.Instance);

        var entitlement = await service.GetEntitlementAsync(userId);

        Assert.Equal(EntitlementTier.Pro, entitlement.Tier);
        Assert.True(entitlement.IsAdvisoryPermitted);
        Assert.Equal("Personalized Pro Advice", entitlement.DisplayLabel);
        Assert.False(entitlement.IsPaid);
        Assert.True(entitlement.ManuallyGranted);
    }

    [Fact]
    public async Task Expired_pro_subscription_automatically_resolves_to_standard()
    {
        var (context, factory) = CreateContext();
        var userId = Guid.NewGuid().ToString();

        context.MemberSubscriptions.Add(new MemberSubscription
        {
            UserId = userId,
            SubscriptionPlanId = 2,
            IsPaid = true,
            Status = "Active",
            EntitlementSlot = "Current",
            StartedAt = DateTimeOffset.UtcNow.AddYears(-2),
            SubscriptionExpiryDate = DateTimeOffset.UtcNow.AddDays(-1) // Expired yesterday
        });
        await context.SaveChangesAsync();

        var service = new SubscriptionEntitlementService(
            factory,
            new TestSystemSettingsService(),
            new MemoryCache(new MemoryCacheOptions()),
            TimeProvider.System,
            NullLogger<SubscriptionEntitlementService>.Instance);

        var entitlement = await service.GetEntitlementAsync(userId);

        Assert.Equal(EntitlementTier.Standard, entitlement.Tier);
        Assert.False(entitlement.IsAdvisoryPermitted);
        Assert.Equal("Educational information only", entitlement.DisplayLabel);
    }

    [Fact]
    public async Task Pro_subscription_with_null_expiry_is_invalid_and_treated_as_standard()
    {
        var (context, factory) = CreateContext();
        var userId = Guid.NewGuid().ToString();

        context.MemberSubscriptions.Add(new MemberSubscription
        {
            UserId = userId,
            SubscriptionPlanId = 2,
            IsPaid = true,
            Status = "Active",
            EntitlementSlot = "Current",
            StartedAt = DateTimeOffset.UtcNow.AddDays(-10),
            SubscriptionExpiryDate = null // Null expiry on Pro is invalid
        });
        await context.SaveChangesAsync();

        var service = new SubscriptionEntitlementService(
            factory,
            new TestSystemSettingsService(),
            new MemoryCache(new MemoryCacheOptions()),
            TimeProvider.System,
            NullLogger<SubscriptionEntitlementService>.Instance);

        var entitlement = await service.GetEntitlementAsync(userId);

        Assert.Equal(EntitlementTier.Standard, entitlement.Tier);
        Assert.False(entitlement.IsAdvisoryPermitted);
    }

    [Fact]
    public async Task Suspended_pro_subscription_blocks_advisory_access()
    {
        var (context, factory) = CreateContext();
        var userId = Guid.NewGuid().ToString();

        context.MemberSubscriptions.Add(new MemberSubscription
        {
            UserId = userId,
            SubscriptionPlanId = 2,
            IsPaid = true,
            Status = "Suspended",
            EntitlementSlot = "Current",
            StartedAt = DateTimeOffset.UtcNow.AddDays(-10),
            SubscriptionExpiryDate = DateTimeOffset.UtcNow.AddDays(300)
        });
        await context.SaveChangesAsync();

        var service = new SubscriptionEntitlementService(
            factory,
            new TestSystemSettingsService(),
            new MemoryCache(new MemoryCacheOptions()),
            TimeProvider.System,
            NullLogger<SubscriptionEntitlementService>.Instance);

        var entitlement = await service.GetEntitlementAsync(userId);

        Assert.Equal(EntitlementTier.Standard, entitlement.Tier);
        Assert.False(entitlement.IsAdvisoryPermitted);
    }

    [Fact]
    public async Task Due_scheduled_renewal_is_recognized_by_resolver()
    {
        var (context, factory) = CreateContext();
        var userId = Guid.NewGuid().ToString();

        // Expired current
        context.MemberSubscriptions.Add(new MemberSubscription
        {
            UserId = userId,
            SubscriptionPlanId = 2,
            IsPaid = true,
            Status = "Active",
            EntitlementSlot = "Current",
            StartedAt = DateTimeOffset.UtcNow.AddYears(-1),
            SubscriptionExpiryDate = DateTimeOffset.UtcNow.AddMinutes(-10)
        });

        // Due renewal started 10 minutes ago
        context.MemberSubscriptions.Add(new MemberSubscription
        {
            UserId = userId,
            SubscriptionPlanId = 2,
            IsPaid = true,
            Status = "Scheduled",
            EntitlementSlot = "Scheduled",
            StartedAt = DateTimeOffset.UtcNow.AddMinutes(-10),
            SubscriptionExpiryDate = DateTimeOffset.UtcNow.AddYears(1)
        });
        await context.SaveChangesAsync();

        var service = new SubscriptionEntitlementService(
            factory,
            new TestSystemSettingsService(),
            new MemoryCache(new MemoryCacheOptions()),
            TimeProvider.System,
            NullLogger<SubscriptionEntitlementService>.Instance);

        var entitlement = await service.GetEntitlementAsync(userId);

        Assert.Equal(EntitlementTier.Pro, entitlement.Tier);
        Assert.True(entitlement.IsAdvisoryPermitted);
    }

    [Fact]
    public async Task Future_scheduled_renewal_does_not_grant_early_access()
    {
        var (context, factory) = CreateContext();
        var userId = Guid.NewGuid().ToString();

        // Standard current
        context.MemberSubscriptions.Add(new MemberSubscription
        {
            UserId = userId,
            SubscriptionPlanId = 1,
            IsPaid = false,
            Status = "Active",
            EntitlementSlot = "Current",
            StartedAt = DateTimeOffset.UtcNow.AddDays(-10),
            SubscriptionExpiryDate = null
        });

        // Future scheduled renewal starting in 30 days
        var futureStart = DateTimeOffset.UtcNow.AddDays(30);
        context.MemberSubscriptions.Add(new MemberSubscription
        {
            UserId = userId,
            SubscriptionPlanId = 2,
            IsPaid = true,
            Status = "Scheduled",
            EntitlementSlot = "Scheduled",
            StartedAt = futureStart,
            SubscriptionExpiryDate = futureStart.AddYears(1)
        });
        await context.SaveChangesAsync();

        var service = new SubscriptionEntitlementService(
            factory,
            new TestSystemSettingsService(),
            new MemoryCache(new MemoryCacheOptions()),
            TimeProvider.System,
            NullLogger<SubscriptionEntitlementService>.Instance);

        var entitlement = await service.GetEntitlementAsync(userId);

        Assert.Equal(EntitlementTier.Standard, entitlement.Tier);
        Assert.False(entitlement.IsAdvisoryPermitted);
        Assert.True(entitlement.HasScheduledRenewal);
        Assert.Equal(futureStart, entitlement.ScheduledRenewalStartDate);
    }

    [Fact]
    public async Task Standard_member_cannot_request_pro_mode()
    {
        var (context, factory) = CreateContext();
        var userId = Guid.NewGuid().ToString();

        context.MemberSubscriptions.Add(new MemberSubscription
        {
            UserId = userId,
            SubscriptionPlanId = 1,
            Status = "Active",
            EntitlementSlot = "Current",
            StartedAt = DateTimeOffset.UtcNow.AddDays(-5)
        });
        await context.SaveChangesAsync();

        var service = new SubscriptionEntitlementService(
            factory,
            new TestSystemSettingsService(),
            new MemoryCache(new MemoryCacheOptions()),
            TimeProvider.System,
            NullLogger<SubscriptionEntitlementService>.Instance);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.EvaluateRuntimeModeAsync(userId, "PRO"));
    }

    [Fact]
    public async Task Pro_member_can_voluntarily_request_standard_educational_mode()
    {
        var (context, factory) = CreateContext();
        var userId = Guid.NewGuid().ToString();

        context.MemberSubscriptions.Add(new MemberSubscription
        {
            UserId = userId,
            SubscriptionPlanId = 2,
            IsPaid = true,
            Status = "Active",
            EntitlementSlot = "Current",
            StartedAt = DateTimeOffset.UtcNow.AddDays(-5),
            SubscriptionExpiryDate = DateTimeOffset.UtcNow.AddDays(360)
        });
        await context.SaveChangesAsync();

        var service = new SubscriptionEntitlementService(
            factory,
            new TestSystemSettingsService(),
            new MemoryCache(new MemoryCacheOptions()),
            TimeProvider.System,
            NullLogger<SubscriptionEntitlementService>.Instance);

        var eval = await service.EvaluateRuntimeModeAsync(userId, "STANDARD");

        Assert.Equal("STANDARD", eval.EffectiveMode);
        Assert.Equal("Educational information only", eval.DisplayLabel);
        Assert.False(eval.IsAdvisoryPermitted);
        Assert.False(eval.ProUnavailableFallback);
    }

    [Fact]
    public async Task Pro_mode_falls_back_to_standard_when_PublicProEnabled_is_false()
    {
        var (context, factory) = CreateContext();
        var userId = Guid.NewGuid().ToString();

        context.MemberSubscriptions.Add(new MemberSubscription
        {
            UserId = userId,
            SubscriptionPlanId = 2,
            IsPaid = true,
            Status = "Active",
            EntitlementSlot = "Current",
            StartedAt = DateTimeOffset.UtcNow.AddDays(-5),
            SubscriptionExpiryDate = DateTimeOffset.UtcNow.AddDays(360)
        });
        await context.SaveChangesAsync();

        var settings = new TestSystemSettingsService(publicProEnabled: false);
        var service = new SubscriptionEntitlementService(
            factory,
            settings,
            new MemoryCache(new MemoryCacheOptions()),
            TimeProvider.System,
            NullLogger<SubscriptionEntitlementService>.Instance);

        var eval = await service.EvaluateRuntimeModeAsync(userId, "PRO");

        Assert.Equal("STANDARD", eval.EffectiveMode);
        Assert.Equal("Educational information only", eval.DisplayLabel);
        Assert.False(eval.IsAdvisoryPermitted);
        Assert.True(eval.ProUnavailableFallback);
        Assert.Equal("Standard mode — Pro analysis temporarily unavailable", eval.FallbackReason);
    }

    [Fact]
    public async Task Pro_mode_is_granted_when_PublicProEnabled_is_true()
    {
        var (context, factory) = CreateContext();
        var userId = Guid.NewGuid().ToString();

        context.MemberSubscriptions.Add(new MemberSubscription
        {
            UserId = userId,
            SubscriptionPlanId = 2,
            IsPaid = true,
            Status = "Active",
            EntitlementSlot = "Current",
            StartedAt = DateTimeOffset.UtcNow.AddDays(-5),
            SubscriptionExpiryDate = DateTimeOffset.UtcNow.AddDays(360)
        });
        await context.SaveChangesAsync();

        var service = new SubscriptionEntitlementService(
            factory,
            new TestSystemSettingsService(publicProEnabled: true),
            new MemoryCache(new MemoryCacheOptions()),
            TimeProvider.System,
            NullLogger<SubscriptionEntitlementService>.Instance);

        var eval = await service.EvaluateRuntimeModeAsync(userId, "PRO");

        Assert.Equal("PRO", eval.EffectiveMode);
        Assert.Equal("Personalized Pro Advice", eval.DisplayLabel);
        Assert.True(eval.IsAdvisoryPermitted);
        Assert.False(eval.ProUnavailableFallback);
    }
}

