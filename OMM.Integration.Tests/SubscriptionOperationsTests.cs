using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using OMM.Public.Data;
using OMM.Shared.Infrastructure.Settings;
using OMM.Shared.Models.Phase5;
using OMM.Shared.Subscription;

namespace OMM.Integration.Tests;

public class SubscriptionOperationsTests
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
    public async Task Member_can_create_upgrade_request_and_submit_reference()
    {
        var (options, factory) = CreateContext();
        var userId = Guid.NewGuid().ToString();

        var entitlementService = new SubscriptionEntitlementService(
            factory,
            new StubSystemSettingsService(),
            new MemoryCache(new MemoryCacheOptions()),
            TimeProvider.System,
            NullLogger<SubscriptionEntitlementService>.Instance);

        var upgradeService = new SubscriptionUpgradeService(
            factory,
            TimeProvider.System,
            NullLogger<SubscriptionUpgradeService>.Instance);

        // 1. Create request
        var request = await upgradeService.CreateUpgradeRequestAsync(userId, targetPlanId: 2);
        Assert.NotNull(request);
        Assert.Equal("AwaitingPayment", request.Status);
        Assert.Equal(99m, request.AmountDue);
        Assert.Equal("MYR", request.CurrencyCode);
        Assert.Null(request.PaymentReference);

        // 2. Submit payment reference
        var submitted = await upgradeService.SubmitPaymentReferenceAsync(request.Id, userId, "MAYBANK-12345");
        Assert.Equal("PaymentSubmitted", submitted.Status);
        Assert.Equal("MAYBANK-12345", submitted.PaymentReference);
        Assert.NotNull(submitted.SubmittedAt);
    }

    [Fact]
    public async Task Member_cannot_create_duplicate_open_request_for_same_plan()
    {
        var (options, factory) = CreateContext();
        var userId = Guid.NewGuid().ToString();

        var entitlementService = new SubscriptionEntitlementService(
            factory,
            new StubSystemSettingsService(),
            new MemoryCache(new MemoryCacheOptions()),
            TimeProvider.System,
            NullLogger<SubscriptionEntitlementService>.Instance);

        var upgradeService = new SubscriptionUpgradeService(
            factory,
            TimeProvider.System,
            NullLogger<SubscriptionUpgradeService>.Instance);

        var req1 = await upgradeService.CreateUpgradeRequestAsync(userId, targetPlanId: 2);
        var req2 = await upgradeService.CreateUpgradeRequestAsync(userId, targetPlanId: 2);

        Assert.Equal(req1.Id, req2.Id);
    }

    [Fact]
    public async Task Member_can_withdraw_open_upgrade_request()
    {
        var (options, factory) = CreateContext();
        var userId = Guid.NewGuid().ToString();

        var entitlementService = new SubscriptionEntitlementService(
            factory,
            new StubSystemSettingsService(),
            new MemoryCache(new MemoryCacheOptions()),
            TimeProvider.System,
            NullLogger<SubscriptionEntitlementService>.Instance);

        var upgradeService = new SubscriptionUpgradeService(
            factory,
            TimeProvider.System,
            NullLogger<SubscriptionUpgradeService>.Instance);

        var request = await upgradeService.CreateUpgradeRequestAsync(userId, targetPlanId: 2);
        var withdrawn = await upgradeService.WithdrawUpgradeRequestAsync(request.Id, userId);

        Assert.Equal("Withdrawn", withdrawn.Status);
        Assert.Equal("Withdrawn by member.", withdrawn.ReviewNotes);
    }

    [Fact]
    public async Task Admin_approval_of_first_pro_terminates_standard_and_creates_active_pro()
    {
        var (options, factory) = CreateContext();
        var userId = Guid.NewGuid().ToString();
        var adminId = "admin-1";

        // Seed initial Standard subscription
        long standardId;
        await using (var seedDb = new ApplicationDbContext(options))
        {
            var standard = new MemberSubscription
            {
                UserId = userId,
                SubscriptionPlanId = 1,
                Status = "Active",
                EntitlementSlot = "Current",
                StartedAt = DateTimeOffset.UtcNow.AddDays(-20)
            };
            seedDb.MemberSubscriptions.Add(standard);
            await seedDb.SaveChangesAsync();
            standardId = standard.Id;
        }

        var entitlementService = new SubscriptionEntitlementService(
            factory,
            new StubSystemSettingsService(),
            new MemoryCache(new MemoryCacheOptions()),
            TimeProvider.System,
            NullLogger<SubscriptionEntitlementService>.Instance);

        var upgradeService = new SubscriptionUpgradeService(
            factory,
            TimeProvider.System,
            NullLogger<SubscriptionUpgradeService>.Instance);

        var adminService = new SubscriptionAdminService(
            factory,
            entitlementService,
            TimeProvider.System,
            NullLogger<SubscriptionAdminService>.Instance);

        var request = await upgradeService.CreateUpgradeRequestAsync(userId, targetPlanId: 2);
        await upgradeService.SubmitPaymentReferenceAsync(request.Id, userId, "REF-999");

        var proSub = await adminService.ApproveUpgradeRequestAsync(request.Id, adminId, "Payment verified via bank slip.");

        Assert.NotNull(proSub);
        Assert.Equal("Active", proSub.Status);
        Assert.Equal("Current", proSub.EntitlementSlot);
        Assert.True(proSub.IsPaid);
        Assert.False(proSub.ManuallyGranted);
        Assert.NotNull(proSub.SubscriptionExpiryDate);

        await using var verifyDb = new ApplicationDbContext(options);

        // Previous standard is terminated
        var standardUpdated = await verifyDb.MemberSubscriptions.FindAsync(standardId);
        Assert.NotNull(standardUpdated);
        Assert.Equal("Terminated", standardUpdated.Status);
        Assert.Null(standardUpdated.EntitlementSlot);
        Assert.NotNull(standardUpdated.TerminatedAt);

        // Request updated
        var requestUpdated = await verifyDb.SubscriptionUpgradeRequests.FindAsync(request.Id);
        Assert.NotNull(requestUpdated);
        Assert.Equal("Approved", requestUpdated.Status);
        Assert.Equal(proSub.Id, requestUpdated.ResultingSubscriptionId);

        // Entitlement resolves to Pro
        var entitlement = await entitlementService.GetEntitlementAsync(userId);
        Assert.Equal(EntitlementTier.Pro, entitlement.Tier);
        Assert.True(entitlement.IsAdvisoryPermitted);
    }

    [Fact]
    public async Task Admin_approval_of_early_renewal_creates_scheduled_subscription()
    {
        var (options, factory) = CreateContext();
        var userId = Guid.NewGuid().ToString();
        var adminId = "admin-1";

        var currentExpiry = DateTimeOffset.UtcNow.AddMonths(6);
        long activeProId;
        await using (var seedDb = new ApplicationDbContext(options))
        {
            var activePro = new MemberSubscription
            {
                UserId = userId,
                SubscriptionPlanId = 2,
                IsPaid = true,
                Status = "Active",
                EntitlementSlot = "Current",
                StartedAt = DateTimeOffset.UtcNow.AddMonths(-6),
                SubscriptionExpiryDate = currentExpiry
            };
            seedDb.MemberSubscriptions.Add(activePro);
            await seedDb.SaveChangesAsync();
            activeProId = activePro.Id;
        }

        var entitlementService = new SubscriptionEntitlementService(
            factory,
            new StubSystemSettingsService(),
            new MemoryCache(new MemoryCacheOptions()),
            TimeProvider.System,
            NullLogger<SubscriptionEntitlementService>.Instance);

        var upgradeService = new SubscriptionUpgradeService(
            factory,
            TimeProvider.System,
            NullLogger<SubscriptionUpgradeService>.Instance);

        var adminService = new SubscriptionAdminService(
            factory,
            entitlementService,
            TimeProvider.System,
            NullLogger<SubscriptionAdminService>.Instance);

        var request = await upgradeService.CreateUpgradeRequestAsync(userId, targetPlanId: 2);
        await upgradeService.SubmitPaymentReferenceAsync(request.Id, userId, "RENEW-REF-1");

        var scheduledSub = await adminService.ApproveUpgradeRequestAsync(request.Id, adminId, "Renewal verified.");

        Assert.NotNull(scheduledSub);
        Assert.Equal("Scheduled", scheduledSub.Status);
        Assert.Equal("Scheduled", scheduledSub.EntitlementSlot);
        Assert.Equal(currentExpiry, scheduledSub.StartedAt);
        Assert.Equal(currentExpiry.AddMonths(12), scheduledSub.SubscriptionExpiryDate);

        await using var verifyDb = new ApplicationDbContext(options);

        // Active Pro is still active
        var activeProStill = await verifyDb.MemberSubscriptions.FindAsync(activeProId);
        Assert.NotNull(activeProStill);
        Assert.Equal("Active", activeProStill.Status);
        Assert.Equal("Current", activeProStill.EntitlementSlot);

        // Resolver sees Pro and sees scheduled renewal
        var entitlement = await entitlementService.GetEntitlementAsync(userId);
        Assert.Equal(EntitlementTier.Pro, entitlement.Tier);
        Assert.True(entitlement.HasScheduledRenewal);
        Assert.Equal(scheduledSub.Id, entitlement.ScheduledSubscriptionId);
        Assert.Equal(currentExpiry, entitlement.ScheduledRenewalStartDate);
    }

    [Fact]
    public async Task Admin_approval_is_idempotent()
    {
        var (options, factory) = CreateContext();
        var userId = Guid.NewGuid().ToString();
        var adminId = "admin-1";

        var entitlementService = new SubscriptionEntitlementService(
            factory,
            new StubSystemSettingsService(),
            new MemoryCache(new MemoryCacheOptions()),
            TimeProvider.System,
            NullLogger<SubscriptionEntitlementService>.Instance);

        var upgradeService = new SubscriptionUpgradeService(
            factory,
            TimeProvider.System,
            NullLogger<SubscriptionUpgradeService>.Instance);

        var adminService = new SubscriptionAdminService(
            factory,
            entitlementService,
            TimeProvider.System,
            NullLogger<SubscriptionAdminService>.Instance);

        var request = await upgradeService.CreateUpgradeRequestAsync(userId, targetPlanId: 2);
        await upgradeService.SubmitPaymentReferenceAsync(request.Id, userId, "REF-111");

        var sub1 = await adminService.ApproveUpgradeRequestAsync(request.Id, adminId, "First approval");
        var sub2 = await adminService.ApproveUpgradeRequestAsync(request.Id, adminId, "Second approval");

        Assert.Equal(sub1.Id, sub2.Id);
    }

    [Fact]
    public async Task Admin_rejection_leaves_member_current_subscription_untouched()
    {
        var (options, factory) = CreateContext();
        var userId = Guid.NewGuid().ToString();
        var adminId = "admin-1";

        long standardId;
        await using (var seedDb = new ApplicationDbContext(options))
        {
            var standard = new MemberSubscription
            {
                UserId = userId,
                SubscriptionPlanId = 1,
                Status = "Active",
                EntitlementSlot = "Current",
                StartedAt = DateTimeOffset.UtcNow.AddDays(-10)
            };
            seedDb.MemberSubscriptions.Add(standard);
            await seedDb.SaveChangesAsync();
            standardId = standard.Id;
        }

        var entitlementService = new SubscriptionEntitlementService(
            factory,
            new StubSystemSettingsService(),
            new MemoryCache(new MemoryCacheOptions()),
            TimeProvider.System,
            NullLogger<SubscriptionEntitlementService>.Instance);

        var upgradeService = new SubscriptionUpgradeService(
            factory,
            TimeProvider.System,
            NullLogger<SubscriptionUpgradeService>.Instance);

        var adminService = new SubscriptionAdminService(
            factory,
            entitlementService,
            TimeProvider.System,
            NullLogger<SubscriptionAdminService>.Instance);

        var request = await upgradeService.CreateUpgradeRequestAsync(userId, targetPlanId: 2);
        await upgradeService.SubmitPaymentReferenceAsync(request.Id, userId, "BAD-REF");

        var rejected = await adminService.RejectUpgradeRequestAsync(request.Id, adminId, "Invalid transaction reference.");

        Assert.Equal("Rejected", rejected.Status);
        Assert.Equal("Invalid transaction reference.", rejected.ReviewNotes);

        await using var verifyDb = new ApplicationDbContext(options);
        var verifiedStandard = await verifyDb.MemberSubscriptions.FindAsync(standardId);
        Assert.NotNull(verifiedStandard);
        Assert.Equal("Active", verifiedStandard.Status);
        Assert.Equal("Current", verifiedStandard.EntitlementSlot);
    }

    [Fact]
    public async Task Admin_manual_grant_creates_finite_term_pro_with_ManuallyGranted_true()
    {
        var (options, factory) = CreateContext();
        var userId = Guid.NewGuid().ToString();
        var adminId = "admin-1";

        var entitlementService = new SubscriptionEntitlementService(
            factory,
            new StubSystemSettingsService(),
            new MemoryCache(new MemoryCacheOptions()),
            TimeProvider.System,
            NullLogger<SubscriptionEntitlementService>.Instance);

        var adminService = new SubscriptionAdminService(
            factory,
            entitlementService,
            TimeProvider.System,
            NullLogger<SubscriptionAdminService>.Instance);

        var sub = await adminService.GrantSubscriptionAsync(
            userId,
            planId: 2,
            durationMonths: 12,
            adminUserId: adminId,
            reason: "VIP promotional grant",
            notes: "Granted at private wealth event");

        Assert.NotNull(sub);
        Assert.Equal("Active", sub.Status);
        Assert.Equal("Current", sub.EntitlementSlot);
        Assert.True(sub.ManuallyGranted);
        Assert.False(sub.IsPaid);
        Assert.NotNull(sub.SubscriptionExpiryDate);

        var entitlement = await entitlementService.GetEntitlementAsync(userId);
        Assert.Equal(EntitlementTier.Pro, entitlement.Tier);
        Assert.True(entitlement.ManuallyGranted);
        Assert.False(entitlement.IsPaid);
    }

    [Fact]
    public async Task Early_termination_terminates_pro_cancels_scheduled_and_restores_standard_lifetime()
    {
        var (options, factory) = CreateContext();
        var userId = Guid.NewGuid().ToString();
        var originalExpiry = DateTimeOffset.UtcNow.AddMonths(8);

        long proId;
        long schedId;
        await using (var seedDb = new ApplicationDbContext(options))
        {
            var pro = new MemberSubscription
            {
                UserId = userId,
                SubscriptionPlanId = 2,
                IsPaid = true,
                Status = "Active",
                EntitlementSlot = "Current",
                StartedAt = DateTimeOffset.UtcNow.AddMonths(-4),
                SubscriptionExpiryDate = originalExpiry
            };
            var sched = new MemberSubscription
            {
                UserId = userId,
                SubscriptionPlanId = 2,
                IsPaid = true,
                Status = "Scheduled",
                EntitlementSlot = "Scheduled",
                StartedAt = originalExpiry,
                SubscriptionExpiryDate = originalExpiry.AddMonths(12)
            };
            seedDb.MemberSubscriptions.AddRange(pro, sched);
            await seedDb.SaveChangesAsync();
            proId = pro.Id;
            schedId = sched.Id;
        }

        var entitlementService = new SubscriptionEntitlementService(
            factory,
            new StubSystemSettingsService(),
            new MemoryCache(new MemoryCacheOptions()),
            TimeProvider.System,
            NullLogger<SubscriptionEntitlementService>.Instance);

        var adminService = new SubscriptionAdminService(
            factory,
            entitlementService,
            TimeProvider.System,
            NullLogger<SubscriptionAdminService>.Instance);

        var standardRestored = await adminService.TerminateSubscriptionEarlyAsync(
            userId,
            actorUserId: userId,
            actorType: "Member",
            reason: "Voluntary cancellation by member");

        Assert.NotNull(standardRestored);
        Assert.Equal("Active", standardRestored.Status);
        Assert.Equal("Current", standardRestored.EntitlementSlot);
        Assert.Null(standardRestored.SubscriptionExpiryDate);

        await using var verifyDb = new ApplicationDbContext(options);

        // Previous Pro is terminated with original expiry preserved
        var proTerminated = await verifyDb.MemberSubscriptions.FindAsync(proId);
        Assert.NotNull(proTerminated);
        Assert.Equal("Terminated", proTerminated.Status);
        Assert.Null(proTerminated.EntitlementSlot);
        Assert.NotNull(proTerminated.TerminatedAt);
        Assert.Equal(originalExpiry, proTerminated.SubscriptionExpiryDate);

        // Scheduled renewal is cancelled
        var schedCancelled = await verifyDb.MemberSubscriptions.FindAsync(schedId);
        Assert.NotNull(schedCancelled);
        Assert.Equal("Cancelled", schedCancelled.Status);
        Assert.Null(schedCancelled.EntitlementSlot);

        // Entitlement is immediately Standard
        var entitlement = await entitlementService.GetEntitlementAsync(userId);
        Assert.Equal(EntitlementTier.Standard, entitlement.Tier);
        Assert.False(entitlement.IsAdvisoryPermitted);
        Assert.False(entitlement.HasScheduledRenewal);
    }

    [Fact]
    public async Task Suspend_and_resume_preserves_slots()
    {
        var (options, factory) = CreateContext();
        var userId = Guid.NewGuid().ToString();
        var adminId = "admin-1";

        long proId;
        long schedId;
        await using (var seedDb = new ApplicationDbContext(options))
        {
            var pro = new MemberSubscription
            {
                UserId = userId,
                SubscriptionPlanId = 2,
                IsPaid = true,
                Status = "Active",
                EntitlementSlot = "Current",
                StartedAt = DateTimeOffset.UtcNow.AddMonths(-1),
                SubscriptionExpiryDate = DateTimeOffset.UtcNow.AddMonths(11)
            };
            var sched = new MemberSubscription
            {
                UserId = userId,
                SubscriptionPlanId = 2,
                IsPaid = true,
                Status = "Scheduled",
                EntitlementSlot = "Scheduled",
                StartedAt = DateTimeOffset.UtcNow.AddMonths(11),
                SubscriptionExpiryDate = DateTimeOffset.UtcNow.AddMonths(23)
            };
            seedDb.MemberSubscriptions.AddRange(pro, sched);
            await seedDb.SaveChangesAsync();
            proId = pro.Id;
            schedId = sched.Id;
        }

        var entitlementService = new SubscriptionEntitlementService(
            factory,
            new StubSystemSettingsService(),
            new MemoryCache(new MemoryCacheOptions()),
            TimeProvider.System,
            NullLogger<SubscriptionEntitlementService>.Instance);

        var adminService = new SubscriptionAdminService(
            factory,
            entitlementService,
            TimeProvider.System,
            NullLogger<SubscriptionAdminService>.Instance);

        // Suspend
        await adminService.SuspendSubscriptionsAsync(userId, adminId, "Suspected fraud investigation");

        await using (var checkDb = new ApplicationDbContext(options))
        {
            var proSuspended = await checkDb.MemberSubscriptions.FindAsync(proId);
            var schedSuspended = await checkDb.MemberSubscriptions.FindAsync(schedId);

            Assert.Equal("Suspended", proSuspended!.Status);
            Assert.Equal("Current", proSuspended.EntitlementSlot); // Preserved slot!

            Assert.Equal("Suspended", schedSuspended!.Status);
            Assert.Equal("Scheduled", schedSuspended.EntitlementSlot); // Preserved slot!
        }

        // Suspended blocks Pro access
        var entSuspended = await entitlementService.GetEntitlementAsync(userId);
        Assert.Equal(EntitlementTier.Standard, entSuspended.Tier);

        // Resume
        await adminService.ResumeSubscriptionsAsync(userId, adminId, "Investigation cleared");

        await using (var resumeDb = new ApplicationDbContext(options))
        {
            var proResumed = await resumeDb.MemberSubscriptions.FindAsync(proId);
            var schedResumed = await resumeDb.MemberSubscriptions.FindAsync(schedId);

            Assert.Equal("Active", proResumed!.Status);
            Assert.Equal("Current", proResumed.EntitlementSlot);

            Assert.Equal("Scheduled", schedResumed!.Status);
            Assert.Equal("Scheduled", schedResumed.EntitlementSlot);
        }

        var entResumed = await entitlementService.GetEntitlementAsync(userId);
        Assert.Equal(EntitlementTier.Pro, entResumed.Tier);
        Assert.True(entResumed.IsAdvisoryPermitted);
    }
}

