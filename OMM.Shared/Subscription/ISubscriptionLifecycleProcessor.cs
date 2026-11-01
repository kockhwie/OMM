using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OMM.Shared.Models.Phase5;

namespace OMM.Shared.Subscription;

public interface ISubscriptionLifecycleProcessor
{
    Task<SubscriptionLifecycleJobRun> ProcessLifecycleAsync(
        string triggerSource,
        string? adminUserId = null,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SubscriptionLifecycleJobRun>> GetRecentJobRunsAsync(
        int take = 20,
        CancellationToken cancellationToken = default);

    Task<SubscriptionLifecycleJobRun?> GetLastSuccessfulRunAsync(
        CancellationToken cancellationToken = default);
}

public sealed class SubscriptionLifecycleProcessor(
    ISubscriptionDbContextFactory dbContextFactory,
    ISubscriptionEntitlementService entitlementService,
    ISubscriptionNotificationDispatcher notificationDispatcher,
    TimeProvider timeProvider,
    ILogger<SubscriptionLifecycleProcessor> logger) : ISubscriptionLifecycleProcessor
{
    private static readonly TimeSpan LockTimeout = TimeSpan.FromMinutes(15);

    public async Task<SubscriptionLifecycleJobRun> ProcessLifecycleAsync(
        string triggerSource,
        string? adminUserId = null,
        CancellationToken cancellationToken = default)
    {
        var now = timeProvider.GetUtcNow();
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        // 1. Single-run concurrency check
        var activeRun = await db.SubscriptionLifecycleJobRuns
            .Where(r => r.Status == "Running" && r.StartedAt > now.Subtract(LockTimeout))
            .OrderByDescending(r => r.StartedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (activeRun is not null)
        {
            throw new InvalidOperationException(
                $"A subscription lifecycle job is already running (CorrelationId: '{activeRun.CorrelationId}', StartedAt: {activeRun.StartedAt}).");
        }

        var standardPlan = await db.SubscriptionPlans
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Code == SubscriptionConstants.PlanCodes.Standard, cancellationToken);

        if (standardPlan is null)
        {
            throw new InvalidOperationException("Standard plan is not configured in database.");
        }

        var jobRun = new SubscriptionLifecycleJobRun
        {
            TriggerSource = triggerSource,
            TriggeredByAdminId = adminUserId,
            Status = "Running",
            StartedAt = now,
            CorrelationId = Guid.NewGuid().ToString("N"),
            ScannedCount = 0,
            ActivatedCount = 0,
            ExpiredCount = 0,
            DowngradedCount = 0,
            NotifiedCount = 0
        };

        db.SubscriptionLifecycleJobRuns.Add(jobRun);
        await db.SaveChangesAsync(cancellationToken);

        try
        {
            // 2. Process Due Scheduled Renewals
            // Find Scheduled subscriptions whose start date has arrived
            var dueRenewals = await db.MemberSubscriptions
                .Where(s => s.Status == SubscriptionConstants.Statuses.Scheduled &&
                           s.StartedAt <= now &&
                           (s.SubscriptionExpiryDate == null || s.SubscriptionExpiryDate > now))
                .ToListAsync(cancellationToken);

            jobRun.ScannedCount += dueRenewals.Count;

            foreach (var scheduled in dueRenewals)
            {
                if (string.IsNullOrWhiteSpace(scheduled.UserId))
                {
                    continue;
                }

                var isRelational = db.Database.IsRelational();
                await using var trans = isRelational ? await db.Database.BeginTransactionAsync(cancellationToken) : null;

                try
                {
                    // Expire old current subscription for this user
                    var oldCurrent = await db.MemberSubscriptions
                        .Where(s => s.UserId == scheduled.UserId &&
                                   s.EntitlementSlot == SubscriptionConstants.Slots.Current &&
                                   s.Id != scheduled.Id)
                        .FirstOrDefaultAsync(cancellationToken);

                    if (oldCurrent is not null)
                    {
                        var prevJson = JsonSerializer.Serialize(new { oldCurrent.Id, oldCurrent.Status, oldCurrent.EntitlementSlot });
                        oldCurrent.Status = SubscriptionConstants.Statuses.Expired;
                        oldCurrent.EntitlementSlot = null;
                        oldCurrent.UpdatedAt = now;
                        oldCurrent.UpdatedByUserId = "System/LifecycleJob";

                        db.SubscriptionChangeEvents.Add(new SubscriptionChangeEvent
                        {
                            UserId = scheduled.UserId,
                            SubscriptionId = oldCurrent.Id,
                            Action = "Expired",
                            ActorType = "System",
                            ActorId = "LifecycleJob",
                            OccurredAt = now,
                            Reason = "Replaced by scheduled renewal",
                            PreviousStateJson = prevJson,
                            NewStateJson = JsonSerializer.Serialize(new { oldCurrent.Id, oldCurrent.Status, oldCurrent.EntitlementSlot })
                        });
                    }

                    // Activate scheduled subscription
                    var schedPrevJson = JsonSerializer.Serialize(new { scheduled.Id, scheduled.Status, scheduled.EntitlementSlot });
                    scheduled.Status = SubscriptionConstants.Statuses.Active;
                    scheduled.EntitlementSlot = SubscriptionConstants.Slots.Current;
                    scheduled.UpdatedAt = now;
                    scheduled.UpdatedByUserId = "System/LifecycleJob";

                    db.SubscriptionChangeEvents.Add(new SubscriptionChangeEvent
                    {
                        UserId = scheduled.UserId,
                        SubscriptionId = scheduled.Id,
                        Action = "Activated",
                        ActorType = "System",
                        ActorId = "LifecycleJob",
                        OccurredAt = now,
                        Reason = "Scheduled renewal term started",
                        PreviousStateJson = schedPrevJson,
                        NewStateJson = JsonSerializer.Serialize(new { scheduled.Id, scheduled.Status, scheduled.EntitlementSlot })
                    });

                    await db.SaveChangesAsync(cancellationToken);
                    if (trans is not null)
                    {
                        await trans.CommitAsync(cancellationToken);
                    }

                    jobRun.ActivatedCount++;
                    entitlementService.InvalidateEntitlementCache(scheduled.UserId);
                }
                catch (Exception ex)
                {
                    if (trans is not null)
                    {
                        await trans.RollbackAsync(cancellationToken);
                    }
                    logger.LogError(ex, "Failed to activate scheduled renewal {SubscriptionId} for user {UserId}.",
                        scheduled.Id, scheduled.UserId);
                }
            }

            // 3. Process Expired Subscriptions Without Renewal
            var expiredSubs = await db.MemberSubscriptions
                .Where(s => s.Status == SubscriptionConstants.Statuses.Active &&
                           s.SubscriptionExpiryDate != null &&
                           s.SubscriptionExpiryDate <= now)
                .ToListAsync(cancellationToken);

            jobRun.ScannedCount += expiredSubs.Count;

            foreach (var expired in expiredSubs)
            {
                if (string.IsNullOrWhiteSpace(expired.UserId))
                {
                    continue;
                }

                var isRelational = db.Database.IsRelational();
                await using var trans = isRelational ? await db.Database.BeginTransactionAsync(cancellationToken) : null;

                try
                {
                    // Check if this user already has an active replacement
                    var hasOtherActive = await db.MemberSubscriptions
                        .AnyAsync(s => s.UserId == expired.UserId &&
                                      s.Status == SubscriptionConstants.Statuses.Active &&
                                      s.Id != expired.Id, cancellationToken);

                    // 1. Expire the subscription
                    var prevJson = JsonSerializer.Serialize(new { expired.Id, expired.Status, expired.EntitlementSlot });
                    expired.Status = SubscriptionConstants.Statuses.Expired;
                    expired.EntitlementSlot = null;
                    expired.UpdatedAt = now;
                    expired.UpdatedByUserId = "System/LifecycleJob";

                    db.SubscriptionChangeEvents.Add(new SubscriptionChangeEvent
                    {
                        UserId = expired.UserId,
                        SubscriptionId = expired.Id,
                        Action = "Expired",
                        ActorType = "System",
                        ActorId = "LifecycleJob",
                        OccurredAt = now,
                        Reason = "Subscription term expired",
                        PreviousStateJson = prevJson,
                        NewStateJson = JsonSerializer.Serialize(new { expired.Id, expired.Status, expired.EntitlementSlot })
                    });

                    // 2. Create idempotent downgrade event if not existing
                    var existingDowngrade = await db.SubscriptionDowngradeEvents
                        .FirstOrDefaultAsync(e => e.SourceSubscriptionId == expired.Id, cancellationToken);

                    if (existingDowngrade is null)
                    {
                        var downgradeEvent = new SubscriptionDowngradeEvent
                        {
                            UserId = expired.UserId,
                            SourceSubscriptionId = expired.Id,
                            PreviousPlanId = expired.SubscriptionPlanId,
                            ResultingPlanId = standardPlan.Id,
                            Reason = "Term expired without renewal",
                            DetectedAt = now,
                            ExpiredAt = expired.SubscriptionExpiryDate ?? now,
                            NotificationStatus = "Pending",
                            NotifiedAt = null
                        };
                        db.SubscriptionDowngradeEvents.Add(downgradeEvent);
                        jobRun.DowngradedCount++;
                    }

                    // 3. Fallback to Standard lifetime row if no other active row exists
                    if (!hasOtherActive)
                    {
                        var standardSub = new MemberSubscription
                        {
                            UserId = expired.UserId,
                            SubscriptionPlanId = standardPlan.Id,
                            IsPaid = false,
                            ManuallyGranted = false,
                            Status = SubscriptionConstants.Statuses.Active,
                            EntitlementSlot = SubscriptionConstants.Slots.Current,
                            StartedAt = now,
                            SubscriptionExpiryDate = null,
                            TerminatedAt = null,
                            CreatedAt = now,
                            CreatedByUserId = "System/LifecycleJob",
                            UpdatedAt = now,
                            UpdatedByUserId = "System/LifecycleJob"
                        };
                        db.MemberSubscriptions.Add(standardSub);

                        db.SubscriptionChangeEvents.Add(new SubscriptionChangeEvent
                        {
                            UserId = expired.UserId,
                            SubscriptionId = standardSub.Id,
                            Action = "DowngradedToStandard",
                            ActorType = "System",
                            ActorId = "LifecycleJob",
                            OccurredAt = now,
                            Reason = "Term expired fallback to Standard lifetime",
                            Notes = "Restored lifetime educational access.",
                            NewStateJson = JsonSerializer.Serialize(new { standardSub.Id, standardSub.Status, standardSub.EntitlementSlot })
                        });
                    }

                    await db.SaveChangesAsync(cancellationToken);
                    if (trans is not null)
                    {
                        await trans.CommitAsync(cancellationToken);
                    }

                    jobRun.ExpiredCount++;
                    entitlementService.InvalidateEntitlementCache(expired.UserId);
                }
                catch (Exception ex)
                {
                    if (trans is not null)
                    {
                        await trans.RollbackAsync(cancellationToken);
                    }
                    logger.LogError(ex, "Failed to expire and downgrade subscription {SubscriptionId} for user {UserId}.",
                        expired.Id, expired.UserId);
                }
            }

            // 4. Process Pending Downgrade Notifications
            var pendingNotifications = await db.SubscriptionDowngradeEvents
                .Where(e => e.NotificationStatus == "Pending")
                .ToListAsync(cancellationToken);

            foreach (var downgrade in pendingNotifications)
            {
                try
                {
                    await notificationDispatcher.DispatchDowngradeNotificationAsync(downgrade, cancellationToken);
                    downgrade.NotificationStatus = "Sent";
                    downgrade.NotifiedAt = timeProvider.GetUtcNow();
                    jobRun.NotifiedCount++;
                }
                catch (Exception ex)
                {
                    downgrade.NotificationStatus = "Failed";
                    logger.LogError(ex, "Failed to dispatch notification for downgrade event {EventId}.", downgrade.Id);
                }
            }

            await db.SaveChangesAsync(cancellationToken);

            // 5. Finalize Job Run
            jobRun.CompletedAt = timeProvider.GetUtcNow();
            jobRun.Status = "Succeeded";
            await db.SaveChangesAsync(cancellationToken);

            logger.LogInformation(
                "Lifecycle job run {JobRunId} completed successfully (Scanned: {Scanned}, Activated: {Activated}, Expired: {Expired}, Downgraded: {Downgraded}, Notified: {Notified}).",
                jobRun.Id, jobRun.ScannedCount, jobRun.ActivatedCount, jobRun.ExpiredCount, jobRun.DowngradedCount, jobRun.NotifiedCount);

            return jobRun;
        }
        catch (Exception ex)
        {
            jobRun.CompletedAt = timeProvider.GetUtcNow();
            jobRun.Status = "Failed";
            var sanitized = ex.Message.Length > 2000 ? ex.Message[..2000] : ex.Message;
            jobRun.ErrorSummary = sanitized;
            await db.SaveChangesAsync(CancellationToken.None);

            logger.LogError(ex, "Lifecycle job run {JobRunId} failed: {Error}", jobRun.Id, sanitized);
            throw;
        }
    }

    public async Task<IReadOnlyList<SubscriptionLifecycleJobRun>> GetRecentJobRunsAsync(
        int take = 20,
        CancellationToken cancellationToken = default)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        return await db.SubscriptionLifecycleJobRuns
            .OrderByDescending(r => r.StartedAt)
            .Take(take)
            .ToListAsync(cancellationToken);
    }

    public async Task<SubscriptionLifecycleJobRun?> GetLastSuccessfulRunAsync(
        CancellationToken cancellationToken = default)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        return await db.SubscriptionLifecycleJobRuns
            .Where(r => r.Status == "Succeeded")
            .OrderByDescending(r => r.CompletedAt ?? r.StartedAt)
            .FirstOrDefaultAsync(cancellationToken);
    }
}

