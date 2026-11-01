using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using OMM.Public.Data;
using OMM.Shared.Infrastructure.Settings;
using OMM.Shared.Models.Phase5;
using OMM.Shared.Subscription;

namespace OMM.Integration.Tests;

public class AccountRetentionTests
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
    public async Task PrepareForAccountDeletion_terminates_active_and_cancels_scheduled_subscriptions()
    {
        var (options, factory) = CreateContext();
        var userId = Guid.NewGuid().ToString();

        long activeSubId;
        long scheduledSubId;
        long openRequestId;

        await using (var seedContext = new ApplicationDbContext(options))
        {
            // 1. Current active Pro subscription
            var activeSub = new MemberSubscription
            {
                UserId = userId,
                SubscriptionPlanId = 2,
                IsPaid = true,
                Status = "Active",
                EntitlementSlot = "Current",
                StartedAt = DateTimeOffset.UtcNow.AddMonths(-2),
                SubscriptionExpiryDate = DateTimeOffset.UtcNow.AddMonths(10)
            };
            seedContext.MemberSubscriptions.Add(activeSub);

            // 2. Scheduled future renewal
            var scheduledSub = new MemberSubscription
            {
                UserId = userId,
                SubscriptionPlanId = 2,
                IsPaid = true,
                Status = "Scheduled",
                EntitlementSlot = "Scheduled",
                StartedAt = DateTimeOffset.UtcNow.AddMonths(10),
                SubscriptionExpiryDate = DateTimeOffset.UtcNow.AddMonths(22)
            };
            seedContext.MemberSubscriptions.Add(scheduledSub);

            // 3. Open upgrade request
            var openRequest = new SubscriptionUpgradeRequest
            {
                UserId = userId,
                SubscriptionPlanId = 2,
                AmountDue = 99m,
                CurrencyCode = "MYR",
                Status = "PaymentSubmitted",
                PaymentReference = "BANK-REF-123",
                CreatedAt = DateTimeOffset.UtcNow
            };
            seedContext.SubscriptionUpgradeRequests.Add(openRequest);

            await seedContext.SaveChangesAsync();
            activeSubId = activeSub.Id;
            scheduledSubId = scheduledSub.Id;
            openRequestId = openRequest.Id;
        }

        var entitlementService = new SubscriptionEntitlementService(
            factory,
            new StubSystemSettingsService(),
            new MemoryCache(new MemoryCacheOptions()),
            TimeProvider.System,
            NullLogger<SubscriptionEntitlementService>.Instance);

        var retentionService = new AccountRetentionService(
            factory,
            entitlementService,
            TimeProvider.System,
            NullLogger<AccountRetentionService>.Instance);

        await retentionService.PrepareForAccountDeletionAsync(userId);

        await using var verifyContext = new ApplicationDbContext(options);

        // Verify active subscription became Terminated and cleared slot
        var updatedActive = await verifyContext.MemberSubscriptions.FindAsync(activeSubId);
        Assert.NotNull(updatedActive);
        Assert.Equal("Terminated", updatedActive.Status);
        Assert.Null(updatedActive.EntitlementSlot);
        Assert.NotNull(updatedActive.TerminatedAt);

        // Verify scheduled subscription became Cancelled and cleared slot
        var updatedScheduled = await verifyContext.MemberSubscriptions.FindAsync(scheduledSubId);
        Assert.NotNull(updatedScheduled);
        Assert.Equal("Cancelled", updatedScheduled.Status);
        Assert.Null(updatedScheduled.EntitlementSlot);
        Assert.NotNull(updatedScheduled.TerminatedAt);

        // Verify upgrade request became Withdrawn
        var updatedRequest = await verifyContext.SubscriptionUpgradeRequests.FindAsync(openRequestId);
        Assert.NotNull(updatedRequest);
        Assert.Equal("Withdrawn", updatedRequest.Status);
        Assert.Equal("Withdrawn due to member account closure.", updatedRequest.ReviewNotes);

        // Verify change events recorded for both
        var changeEvents = await verifyContext.SubscriptionChangeEvents
            .Where(e => e.UserId == userId)
            .ToListAsync();

        Assert.Equal(2, changeEvents.Count);
        Assert.Contains(changeEvents, e => e.Action == "Terminated" && e.SubscriptionId == activeSubId);
        Assert.Contains(changeEvents, e => e.Action == "Cancelled" && e.SubscriptionId == scheduledSubId);
    }

    [Fact]
    public async Task PrepareForAccountDeletion_NullsInquirySessionUserId_AndRetainsInquiryRecords()
    {
        var (options, factory) = CreateContext();
        var userId = Guid.NewGuid().ToString();
        long sessionId;
        long turnId;

        await using (var seedContext = new ApplicationDbContext(options))
        {
            var session = new InquirySession
            {
                UserId = userId,
                EntryPoint = "DirectQuestion",
                RequestedMode = "PRO",
                IsAnonymous = false,
                FlowVersion = "v1",
                StartedAt = DateTimeOffset.UtcNow,
                ExpiresAt = DateTimeOffset.UtcNow.AddYears(3)
            };
            seedContext.InquirySessions.Add(session);
            await seedContext.SaveChangesAsync();
            sessionId = session.Id;

            var turn = new InquiryTurn
            {
                InquirySessionId = sessionId,
                TurnNumber = 1,
                RequestedMode = "PRO",
                EffectiveMode = "PRO",
                CreatedAt = DateTimeOffset.UtcNow
            };
            seedContext.InquiryTurns.Add(turn);
            await seedContext.SaveChangesAsync();
            turnId = turn.Id;

            var rawMessage = new InquiryRawMessage
            {
                InquiryTurnId = turnId,
                MessageText = "PROTECTED[Member message to retain]",
                CreatedAt = DateTimeOffset.UtcNow,
                ExpiresAt = DateTimeOffset.UtcNow.AddYears(3)
            };
            seedContext.InquiryRawMessages.Add(rawMessage);

            var telemetry = new InquiryTelemetryEvent
            {
                UserId = userId,
                InquirySessionId = sessionId,
                EventType = "TurnCompleted",
                PayloadJson = "{}",
                OccurredAt = DateTimeOffset.UtcNow,
                ExpiresAt = DateTimeOffset.UtcNow.AddYears(3)
            };
            seedContext.InquiryTelemetryEvents.Add(telemetry);

            await seedContext.SaveChangesAsync();
        }

        var entitlementService = new SubscriptionEntitlementService(
            factory,
            new StubSystemSettingsService(),
            new MemoryCache(new MemoryCacheOptions()),
            TimeProvider.System,
            NullLogger<SubscriptionEntitlementService>.Instance);

        var retentionService = new AccountRetentionService(
            factory,
            entitlementService,
            TimeProvider.System,
            NullLogger<AccountRetentionService>.Instance);

        await retentionService.PrepareForAccountDeletionAsync(userId);

        await using var verifyContext = new ApplicationDbContext(options);

        // Verify session still exists but UserId is severed (set to null)
        var updatedSession = await verifyContext.InquirySessions.FindAsync(sessionId);
        Assert.NotNull(updatedSession);
        Assert.Null(updatedSession.UserId);

        // Verify turns and raw messages are preserved
        var updatedTurn = await verifyContext.InquiryTurns.FindAsync(turnId);
        Assert.NotNull(updatedTurn);

        var updatedRaw = await verifyContext.InquiryRawMessages.FindAsync(turnId);
        Assert.NotNull(updatedRaw);
        Assert.Equal("PROTECTED[Member message to retain]", updatedRaw.MessageText);

        // Verify telemetry UserId was also set to null
        var updatedTelemetry = await verifyContext.InquiryTelemetryEvents
            .FirstOrDefaultAsync(t => t.InquirySessionId == sessionId);
        Assert.NotNull(updatedTelemetry);
        Assert.Null(updatedTelemetry.UserId);
    }
}
