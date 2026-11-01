using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using OMM.Public.Data;
using OMM.Shared.Infrastructure.Settings;
using OMM.Shared.Models.Phase5;
using OMM.Shared.Subscription;

namespace OMM.Integration.Tests;

public class SubscriptionLifecycleProcessorTests
{
    private sealed class TestTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        private DateTimeOffset _utcNow = utcNow;
        public override DateTimeOffset GetUtcNow() => _utcNow;
        public void Advance(TimeSpan timeSpan) => _utcNow = _utcNow.Add(timeSpan);
    }
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

    private sealed class TestNotificationDispatcher : ISubscriptionNotificationDispatcher
    {
        public List<SubscriptionDowngradeEvent> DispatchedEvents { get; } = [];
        public bool ShouldFail { get; set; }

        public Task DispatchDowngradeNotificationAsync(
            SubscriptionDowngradeEvent downgradeEvent,
            CancellationToken cancellationToken = default)
        {
            if (ShouldFail)
            {
                throw new InvalidOperationException("Simulated notification service failure.");
            }

            DispatchedEvents.Add(downgradeEvent);
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task Single_run_concurrency_lock_prevents_overlapping_runs()
    {
        var (options, factory) = CreateContext();
        var fakeTime = new TestTimeProvider(DateTimeOffset.UtcNow);
        var memoryCache = new MemoryCache(new MemoryCacheOptions());
        var settings = new StubSystemSettingsService();
        var entitlement = new SubscriptionEntitlementService(factory, settings, memoryCache, fakeTime, NullLogger<SubscriptionEntitlementService>.Instance);
        var dispatcher = new TestNotificationDispatcher();

        // Seed an existing running job run within the 15-minute window
        await using (var db = new ApplicationDbContext(options))
        {
            db.SubscriptionLifecycleJobRuns.Add(new SubscriptionLifecycleJobRun
            {
                TriggerSource = "Scheduled",
                Status = "Running",
                StartedAt = fakeTime.GetUtcNow().AddMinutes(-5),
                CorrelationId = "active-run-123"
            });
            await db.SaveChangesAsync();
        }

        var processor = new SubscriptionLifecycleProcessor(
            factory,
            entitlement,
            dispatcher,
            fakeTime,
            NullLogger<SubscriptionLifecycleProcessor>.Instance);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            processor.ProcessLifecycleAsync("AdminManual", "admin1"));

        Assert.Contains("active-run-123", ex.Message);
    }

    [Fact]
    public async Task Stale_run_lock_older_than_15_minutes_allows_new_run()
    {
        var (options, factory) = CreateContext();
        var fakeTime = new TestTimeProvider(DateTimeOffset.UtcNow);
        var memoryCache = new MemoryCache(new MemoryCacheOptions());
        var settings = new StubSystemSettingsService();
        var entitlement = new SubscriptionEntitlementService(factory, settings, memoryCache, fakeTime, NullLogger<SubscriptionEntitlementService>.Instance);
        var dispatcher = new TestNotificationDispatcher();

        // Seed an abandoned running job run from 20 minutes ago
        await using (var db = new ApplicationDbContext(options))
        {
            db.SubscriptionLifecycleJobRuns.Add(new SubscriptionLifecycleJobRun
            {
                TriggerSource = "Scheduled",
                Status = "Running",
                StartedAt = fakeTime.GetUtcNow().AddMinutes(-20),
                CorrelationId = "stale-run-456"
            });
            await db.SaveChangesAsync();
        }

        var processor = new SubscriptionLifecycleProcessor(
            factory,
            entitlement,
            dispatcher,
            fakeTime,
            NullLogger<SubscriptionLifecycleProcessor>.Instance);

        var run = await processor.ProcessLifecycleAsync("AdminManual", "admin1");

        Assert.NotNull(run);
        Assert.Equal("Succeeded", run.Status);
        Assert.Equal("AdminManual", run.TriggerSource);
        Assert.Equal("admin1", run.TriggeredByAdminId);
    }

    [Fact]
    public async Task Due_scheduled_renewal_is_activated_and_old_subscription_expired()
    {
        var (options, factory) = CreateContext();
        var now = DateTimeOffset.UtcNow;
        var fakeTime = new TestTimeProvider(now);
        var memoryCache = new MemoryCache(new MemoryCacheOptions());
        var settings = new StubSystemSettingsService();
        var entitlement = new SubscriptionEntitlementService(factory, settings, memoryCache, fakeTime, NullLogger<SubscriptionEntitlementService>.Instance);
        var dispatcher = new TestNotificationDispatcher();

        var userId = "user-due-renewal";
        const long oldSubId = 101;
        const long newSubId = 102;

        await using (var db = new ApplicationDbContext(options))
        {
            // Current expiring Pro subscription
            db.MemberSubscriptions.Add(new MemberSubscription
            {
                Id = oldSubId,
                UserId = userId,
                SubscriptionPlanId = 2, // Pro
                Status = SubscriptionConstants.Statuses.Active,
                EntitlementSlot = SubscriptionConstants.Slots.Current,
                StartedAt = now.AddMonths(-12),
                SubscriptionExpiryDate = now.AddMinutes(-1), // Just reached expiry
                CreatedAt = now.AddMonths(-12),
                UpdatedAt = now.AddMonths(-12)
            });

            // Scheduled renewal starting at expiry
            db.MemberSubscriptions.Add(new MemberSubscription
            {
                Id = newSubId,
                UserId = userId,
                SubscriptionPlanId = 2, // Pro
                Status = SubscriptionConstants.Statuses.Scheduled,
                EntitlementSlot = SubscriptionConstants.Slots.Scheduled,
                StartedAt = now.AddMinutes(-1), // Due
                SubscriptionExpiryDate = now.AddMonths(12),
                CreatedAt = now.AddDays(-5),
                UpdatedAt = now.AddDays(-5)
            });

            await db.SaveChangesAsync();
        }

        var processor = new SubscriptionLifecycleProcessor(
            factory,
            entitlement,
            dispatcher,
            fakeTime,
            NullLogger<SubscriptionLifecycleProcessor>.Instance);

        var result = await processor.ProcessLifecycleAsync("Scheduled");

        Assert.Equal(1, result.ActivatedCount);
        Assert.Equal("Succeeded", result.Status);

        await using (var db = new ApplicationDbContext(options))
        {
            var oldSub = await db.MemberSubscriptions.FindAsync(oldSubId);
            var newSub = await db.MemberSubscriptions.FindAsync(newSubId);

            Assert.NotNull(oldSub);
            Assert.Equal(SubscriptionConstants.Statuses.Expired, oldSub.Status);
            Assert.Null(oldSub.EntitlementSlot);

            Assert.NotNull(newSub);
            Assert.Equal(SubscriptionConstants.Statuses.Active, newSub.Status);
            Assert.Equal(SubscriptionConstants.Slots.Current, newSub.EntitlementSlot);

            // Audit records created
            var changeEvents = await db.SubscriptionChangeEvents.Where(e => e.UserId == userId).ToListAsync();
            Assert.Equal(2, changeEvents.Count);
            Assert.Contains(changeEvents, e => e.Action == "Expired" && e.SubscriptionId == oldSubId);
            Assert.Contains(changeEvents, e => e.Action == "Activated" && e.SubscriptionId == newSubId);
        }
    }

    [Fact]
    public async Task Expired_pro_subscription_without_renewal_is_downgraded_to_standard()
    {
        var (options, factory) = CreateContext();
        var now = DateTimeOffset.UtcNow;
        var fakeTime = new TestTimeProvider(now);
        var memoryCache = new MemoryCache(new MemoryCacheOptions());
        var settings = new StubSystemSettingsService();
        var entitlement = new SubscriptionEntitlementService(factory, settings, memoryCache, fakeTime, NullLogger<SubscriptionEntitlementService>.Instance);
        var dispatcher = new TestNotificationDispatcher();

        var userId = "user-expired-pro";
        const long proSubId = 201;

        await using (var db = new ApplicationDbContext(options))
        {
            db.MemberSubscriptions.Add(new MemberSubscription
            {
                Id = proSubId,
                UserId = userId,
                SubscriptionPlanId = 2, // Pro
                Status = SubscriptionConstants.Statuses.Active,
                EntitlementSlot = SubscriptionConstants.Slots.Current,
                StartedAt = now.AddMonths(-12),
                SubscriptionExpiryDate = now.AddMinutes(-10), // Expired
                CreatedAt = now.AddMonths(-12),
                UpdatedAt = now.AddMonths(-12)
            });
            await db.SaveChangesAsync();
        }

        var processor = new SubscriptionLifecycleProcessor(
            factory,
            entitlement,
            dispatcher,
            fakeTime,
            NullLogger<SubscriptionLifecycleProcessor>.Instance);

        var result = await processor.ProcessLifecycleAsync("Scheduled");

        Assert.Equal(1, result.ExpiredCount);
        Assert.Equal(1, result.DowngradedCount);
        Assert.Equal(1, result.NotifiedCount);
        Assert.Equal("Succeeded", result.Status);

        await using (var db = new ApplicationDbContext(options))
        {
            var proSub = await db.MemberSubscriptions.FindAsync(proSubId);
            Assert.NotNull(proSub);
            Assert.Equal(SubscriptionConstants.Statuses.Expired, proSub.Status);
            Assert.Null(proSub.EntitlementSlot);

            // Downgrade event created and marked Sent
            var downgrade = await db.SubscriptionDowngradeEvents
                .FirstOrDefaultAsync(e => e.SourceSubscriptionId == proSubId);
            Assert.NotNull(downgrade);
            Assert.Equal("Sent", downgrade.NotificationStatus);
            Assert.NotNull(downgrade.NotifiedAt);
            Assert.Equal(userId, downgrade.UserId);

            // Standard lifetime subscription created
            var standardSub = await db.MemberSubscriptions
                .FirstOrDefaultAsync(s => s.UserId == userId && s.SubscriptionPlanId == 1 && s.Status == SubscriptionConstants.Statuses.Active);
            Assert.NotNull(standardSub);
            Assert.Equal(SubscriptionConstants.Slots.Current, standardSub.EntitlementSlot);
            Assert.Null(standardSub.SubscriptionExpiryDate);

            // Notification dispatcher invoked
            Assert.Single(dispatcher.DispatchedEvents);
            Assert.Equal(proSubId, dispatcher.DispatchedEvents[0].SourceSubscriptionId);
        }
    }

    [Fact]
    public async Task Idempotent_rerun_does_not_create_duplicate_downgrades_or_standard_subscriptions()
    {
        var (options, factory) = CreateContext();
        var now = DateTimeOffset.UtcNow;
        var fakeTime = new TestTimeProvider(now);
        var memoryCache = new MemoryCache(new MemoryCacheOptions());
        var settings = new StubSystemSettingsService();
        var entitlement = new SubscriptionEntitlementService(factory, settings, memoryCache, fakeTime, NullLogger<SubscriptionEntitlementService>.Instance);
        var dispatcher = new TestNotificationDispatcher();

        var userId = "user-idempotency";
        const long proSubId = 301;

        await using (var db = new ApplicationDbContext(options))
        {
            db.MemberSubscriptions.Add(new MemberSubscription
            {
                Id = proSubId,
                UserId = userId,
                SubscriptionPlanId = 2,
                Status = SubscriptionConstants.Statuses.Active,
                EntitlementSlot = SubscriptionConstants.Slots.Current,
                StartedAt = now.AddMonths(-12),
                SubscriptionExpiryDate = now.AddMinutes(-5),
                CreatedAt = now.AddMonths(-12),
                UpdatedAt = now.AddMonths(-12)
            });
            await db.SaveChangesAsync();
        }

        var processor = new SubscriptionLifecycleProcessor(
            factory,
            entitlement,
            dispatcher,
            fakeTime,
            NullLogger<SubscriptionLifecycleProcessor>.Instance);

        // Run 1
        var run1 = await processor.ProcessLifecycleAsync("Scheduled");
        Assert.Equal(1, run1.ExpiredCount);
        Assert.Equal(1, run1.DowngradedCount);
        Assert.Equal(1, run1.NotifiedCount);

        // Advance fake time slightly and Run 2
        fakeTime.Advance(TimeSpan.FromMinutes(2));
        var run2 = await processor.ProcessLifecycleAsync("Scheduled");
        Assert.Equal(0, run2.ActivatedCount);
        Assert.Equal(0, run2.ExpiredCount);
        Assert.Equal(0, run2.DowngradedCount);
        Assert.Equal(0, run2.NotifiedCount);

        await using (var db = new ApplicationDbContext(options))
        {
            var standardSubs = await db.MemberSubscriptions
                .Where(s => s.UserId == userId && s.SubscriptionPlanId == 1)
                .ToListAsync();
            Assert.Single(standardSubs);

            var downgradeEvents = await db.SubscriptionDowngradeEvents
                .Where(e => e.UserId == userId)
                .ToListAsync();
            Assert.Single(downgradeEvents);
        }
    }

    [Fact]
    public async Task Notification_failure_marks_event_as_failed_and_does_not_break_job()
    {
        var (options, factory) = CreateContext();
        var now = DateTimeOffset.UtcNow;
        var fakeTime = new TestTimeProvider(now);
        var memoryCache = new MemoryCache(new MemoryCacheOptions());
        var settings = new StubSystemSettingsService();
        var entitlement = new SubscriptionEntitlementService(factory, settings, memoryCache, fakeTime, NullLogger<SubscriptionEntitlementService>.Instance);
        var dispatcher = new TestNotificationDispatcher { ShouldFail = true };

        var userId = "user-notify-fail";
        const long proSubId = 401;

        await using (var db = new ApplicationDbContext(options))
        {
            db.MemberSubscriptions.Add(new MemberSubscription
            {
                Id = proSubId,
                UserId = userId,
                SubscriptionPlanId = 2,
                Status = SubscriptionConstants.Statuses.Active,
                EntitlementSlot = SubscriptionConstants.Slots.Current,
                StartedAt = now.AddMonths(-12),
                SubscriptionExpiryDate = now.AddMinutes(-5),
                CreatedAt = now.AddMonths(-12),
                UpdatedAt = now.AddMonths(-12)
            });
            await db.SaveChangesAsync();
        }

        var processor = new SubscriptionLifecycleProcessor(
            factory,
            entitlement,
            dispatcher,
            fakeTime,
            NullLogger<SubscriptionLifecycleProcessor>.Instance);

        var run = await processor.ProcessLifecycleAsync("Scheduled");

        Assert.Equal("Succeeded", run.Status);
        Assert.Equal(0, run.NotifiedCount);

        await using (var db = new ApplicationDbContext(options))
        {
            var downgrade = await db.SubscriptionDowngradeEvents
                .FirstOrDefaultAsync(e => e.SourceSubscriptionId == proSubId);
            Assert.NotNull(downgrade);
            Assert.Equal("Failed", downgrade.NotificationStatus);
            Assert.Null(downgrade.NotifiedAt);
        }
    }

    [Fact]
    public async Task Job_run_history_queries_return_accurate_telemetry()
    {
        var (options, factory) = CreateContext();
        var now = DateTimeOffset.UtcNow;
        var fakeTime = new TestTimeProvider(now);
        var memoryCache = new MemoryCache(new MemoryCacheOptions());
        var settings = new StubSystemSettingsService();
        var entitlement = new SubscriptionEntitlementService(factory, settings, memoryCache, fakeTime, NullLogger<SubscriptionEntitlementService>.Instance);
        var dispatcher = new TestNotificationDispatcher();

        await using (var db = new ApplicationDbContext(options))
        {
            db.SubscriptionLifecycleJobRuns.AddRange(
                new SubscriptionLifecycleJobRun
                {
                    TriggerSource = "Scheduled",
                    Status = "Succeeded",
                    StartedAt = now.AddHours(-2),
                    CompletedAt = now.AddHours(-2).AddSeconds(1),
                    CorrelationId = "run-1",
                    ScannedCount = 10,
                    ActivatedCount = 1
                },
                new SubscriptionLifecycleJobRun
                {
                    TriggerSource = "AdminManual",
                    TriggeredByAdminId = "admin-x",
                    Status = "Failed",
                    StartedAt = now.AddHours(-1),
                    CompletedAt = now.AddHours(-1).AddSeconds(1),
                    CorrelationId = "run-2",
                    ErrorSummary = "Transient error"
                },
                new SubscriptionLifecycleJobRun
                {
                    TriggerSource = "Scheduled",
                    Status = "Succeeded",
                    StartedAt = now,
                    CompletedAt = now.AddSeconds(2),
                    CorrelationId = "run-3",
                    ScannedCount = 5,
                    ExpiredCount = 1
                });
            await db.SaveChangesAsync();
        }

        var processor = new SubscriptionLifecycleProcessor(
            factory,
            entitlement,
            dispatcher,
            fakeTime,
            NullLogger<SubscriptionLifecycleProcessor>.Instance);

        var recent = await processor.GetRecentJobRunsAsync(10);
        Assert.Equal(3, recent.Count);
        Assert.Equal("run-3", recent[0].CorrelationId);
        Assert.Equal("run-2", recent[1].CorrelationId);
        Assert.Equal("run-1", recent[2].CorrelationId);

        var lastSuccess = await processor.GetLastSuccessfulRunAsync();
        Assert.NotNull(lastSuccess);
        Assert.Equal("run-3", lastSuccess.CorrelationId);
        Assert.Equal("Succeeded", lastSuccess.Status);
    }
}
