using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OMM.Shared.Models.Phase5;

namespace OMM.Shared.Subscription;

public interface ISubscriptionAdminService
{
    Task<MemberSubscription> ApproveUpgradeRequestAsync(long upgradeRequestId, string adminUserId, string? reviewNotes = null, CancellationToken cancellationToken = default);
    Task<SubscriptionUpgradeRequest> RejectUpgradeRequestAsync(long upgradeRequestId, string adminUserId, string reason, CancellationToken cancellationToken = default);
    Task<MemberSubscription> GrantSubscriptionAsync(string userId, int planId, int durationMonths, string adminUserId, string reason, string? notes = null, CancellationToken cancellationToken = default);
    Task<MemberSubscription> CancelScheduledRenewalAsync(long scheduledSubscriptionId, string actorUserId, string actorType, string reason, string? notes = null, CancellationToken cancellationToken = default);
    Task<MemberSubscription> TerminateSubscriptionEarlyAsync(string userId, string actorUserId, string actorType, string reason, string? notes = null, CancellationToken cancellationToken = default);
    Task SuspendSubscriptionsAsync(string userId, string adminUserId, string reason, string? notes = null, CancellationToken cancellationToken = default);
    Task ResumeSubscriptionsAsync(string userId, string adminUserId, string reason, string? notes = null, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SubscriptionUpgradeRequest>> GetPendingUpgradeRequestsAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<MemberSubscription>> GetSubscriptionHistoryAsync(string userId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SubscriptionChangeEvent>> GetSubscriptionAuditTrailAsync(string? userId = null, long? subscriptionId = null, CancellationToken cancellationToken = default);
}

public sealed class SubscriptionAdminService(
    ISubscriptionDbContextFactory dbContextFactory,
    ISubscriptionEntitlementService entitlementService,
    TimeProvider timeProvider,
    ILogger<SubscriptionAdminService> logger) : ISubscriptionAdminService
{
    public async Task<MemberSubscription> ApproveUpgradeRequestAsync(
        long upgradeRequestId,
        string adminUserId,
        string? reviewNotes = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(adminUserId);

        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var now = timeProvider.GetUtcNow();

        var request = await db.SubscriptionUpgradeRequests
            .FirstOrDefaultAsync(r => r.Id == upgradeRequestId, cancellationToken);

        if (request is null)
        {
            throw new InvalidOperationException($"Upgrade request {upgradeRequestId} was not found.");
        }

        // Idempotency: if already approved, return the existing resulting subscription
        if (request.Status == "Approved")
        {
            if (request.ResultingSubscriptionId.HasValue)
            {
                var existingSub = await db.MemberSubscriptions
                    .FirstOrDefaultAsync(s => s.Id == request.ResultingSubscriptionId.Value, cancellationToken);
                if (existingSub is not null)
                {
                    logger.LogInformation("Upgrade request {RequestId} was already approved. Returning existing subscription {SubscriptionId}.",
                        request.Id, existingSub.Id);
                    return existingSub;
                }
            }
            throw new InvalidOperationException($"Upgrade request {upgradeRequestId} is already approved but has no resulting subscription.");
        }

        if (request.Status != "PaymentSubmitted" && request.Status != "AwaitingPayment")
        {
            throw new InvalidOperationException($"Cannot approve request in '{request.Status}' status. Expected 'PaymentSubmitted' or 'AwaitingPayment'.");
        }

        if (string.IsNullOrWhiteSpace(request.UserId))
        {
            throw new InvalidOperationException($"Upgrade request {upgradeRequestId} has no associated user.");
        }

        var plan = await db.SubscriptionPlans
            .FirstOrDefaultAsync(p => p.Id == request.SubscriptionPlanId, cancellationToken);

        if (plan is null)
        {
            throw new InvalidOperationException($"Plan {request.SubscriptionPlanId} was not found.");
        }

        var isRelational = db.Database.IsRelational();
        await using var transaction = isRelational ? await db.Database.BeginTransactionAsync(cancellationToken) : null;

        try
        {
            var userSubscriptions = await db.MemberSubscriptions
                .Where(s => s.UserId == request.UserId &&
                           (s.Status == SubscriptionConstants.Statuses.Active ||
                            s.Status == SubscriptionConstants.Statuses.Scheduled))
                .ToListAsync(cancellationToken);

            var currentActive = userSubscriptions.FirstOrDefault(s => s.EntitlementSlot == SubscriptionConstants.Slots.Current && s.Status == SubscriptionConstants.Statuses.Active);
            var scheduled = userSubscriptions.FirstOrDefault(s => s.EntitlementSlot == SubscriptionConstants.Slots.Scheduled && s.Status == SubscriptionConstants.Statuses.Scheduled);

            MemberSubscription newSub;
            var duration = plan.DurationMonths ?? 12;

            if (currentActive is not null && currentActive.SubscriptionPlanId == plan.Id && currentActive.SubscriptionExpiryDate.HasValue && currentActive.SubscriptionExpiryDate.Value > now)
            {
                // Early Pro renewal! Current remains Active; new term is Scheduled starting at current expiry
                if (scheduled is not null)
                {
                    throw new InvalidOperationException("Member already has a scheduled renewal. At most one scheduled renewal is allowed.");
                }

                var renewalStart = currentActive.SubscriptionExpiryDate.Value;
                var renewalExpiry = renewalStart.AddMonths(duration);

                newSub = new MemberSubscription
                {
                    UserId = request.UserId,
                    SubscriptionPlanId = plan.Id,
                    IsPaid = true,
                    ManuallyGranted = false,
                    Status = SubscriptionConstants.Statuses.Scheduled,
                    EntitlementSlot = SubscriptionConstants.Slots.Scheduled,
                    StartedAt = renewalStart,
                    SubscriptionExpiryDate = renewalExpiry,
                    TerminatedAt = null,
                    CreatedAt = now,
                    CreatedByUserId = adminUserId,
                    UpdatedAt = now,
                    UpdatedByUserId = adminUserId,
                    PaymentReference = request.PaymentReference
                };

                db.MemberSubscriptions.Add(newSub);
                await db.SaveChangesAsync(cancellationToken);

                db.SubscriptionChangeEvents.Add(new SubscriptionChangeEvent
                {
                    UserId = request.UserId,
                    SubscriptionId = newSub.Id,
                    Action = "RenewalApproved",
                    ActorType = "Admin",
                    ActorId = adminUserId,
                    OccurredAt = now,
                    Reason = "Early Pro renewal payment approved",
                    Notes = reviewNotes,
                    NewStateJson = JsonSerializer.Serialize(new { newSub.Id, newSub.Status, newSub.EntitlementSlot, newSub.StartedAt, newSub.SubscriptionExpiryDate })
                });
            }
            else
            {
                // First Pro approval or upgrading from Standard:
                // Terminate the active Standard subscription in the same transaction
                if (currentActive is not null)
                {
                    var prevJson = JsonSerializer.Serialize(new { currentActive.Id, currentActive.Status, currentActive.EntitlementSlot });
                    currentActive.Status = SubscriptionConstants.Statuses.Terminated;
                    currentActive.EntitlementSlot = null;
                    currentActive.TerminatedAt = now;
                    currentActive.UpdatedAt = now;
                    currentActive.UpdatedByUserId = adminUserId;

                    db.SubscriptionChangeEvents.Add(new SubscriptionChangeEvent
                    {
                        UserId = request.UserId,
                        SubscriptionId = currentActive.Id,
                        Action = "Terminated",
                        ActorType = "Admin",
                        ActorId = adminUserId,
                        OccurredAt = now,
                        Reason = "Upgraded to Pro",
                        Notes = "Standard lifetime entitlement terminated upon Pro activation.",
                        PreviousStateJson = prevJson,
                        NewStateJson = JsonSerializer.Serialize(new { currentActive.Id, currentActive.Status, currentActive.EntitlementSlot, currentActive.TerminatedAt })
                    });
                }

                var expiry = now.AddMonths(duration);
                newSub = new MemberSubscription
                {
                    UserId = request.UserId,
                    SubscriptionPlanId = plan.Id,
                    IsPaid = true,
                    ManuallyGranted = false,
                    Status = SubscriptionConstants.Statuses.Active,
                    EntitlementSlot = SubscriptionConstants.Slots.Current,
                    StartedAt = now,
                    SubscriptionExpiryDate = expiry,
                    TerminatedAt = null,
                    CreatedAt = now,
                    CreatedByUserId = adminUserId,
                    UpdatedAt = now,
                    UpdatedByUserId = adminUserId,
                    PaymentReference = request.PaymentReference
                };

                db.MemberSubscriptions.Add(newSub);
                await db.SaveChangesAsync(cancellationToken);

                db.SubscriptionChangeEvents.Add(new SubscriptionChangeEvent
                {
                    UserId = request.UserId,
                    SubscriptionId = newSub.Id,
                    Action = "Approved",
                    ActorType = "Admin",
                    ActorId = adminUserId,
                    OccurredAt = now,
                    Reason = "Payment verified and approved",
                    Notes = reviewNotes,
                    NewStateJson = JsonSerializer.Serialize(new { newSub.Id, newSub.Status, newSub.EntitlementSlot, newSub.StartedAt, newSub.SubscriptionExpiryDate })
                });
            }

            // Update request
            request.Status = "Approved";
            request.ReviewedByUserId = adminUserId;
            request.ReviewedAt = now;
            request.ReviewNotes = reviewNotes;
            request.ResultingSubscriptionId = newSub.Id;

            await db.SaveChangesAsync(cancellationToken);

            if (transaction is not null)
            {
                await transaction.CommitAsync(cancellationToken);
            }

            entitlementService.InvalidateEntitlementCache(request.UserId);
            logger.LogInformation("Admin {AdminId} approved upgrade request {RequestId} -> Subscription {SubscriptionId} (Slot: {Slot}).",
                adminUserId, request.Id, newSub.Id, newSub.EntitlementSlot);

            return newSub;
        }
        catch (Exception ex)
        {
            if (transaction is not null)
            {
                await transaction.RollbackAsync(cancellationToken);
            }
            logger.LogError(ex, "Failed to approve upgrade request {RequestId}.", upgradeRequestId);
            throw;
        }
    }

    public async Task<SubscriptionUpgradeRequest> RejectUpgradeRequestAsync(
        long upgradeRequestId,
        string adminUserId,
        string reason,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(adminUserId);
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);

        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        var request = await db.SubscriptionUpgradeRequests
            .FirstOrDefaultAsync(r => r.Id == upgradeRequestId, cancellationToken);

        if (request is null)
        {
            throw new InvalidOperationException($"Upgrade request {upgradeRequestId} was not found.");
        }

        if (request.Status != "PaymentSubmitted" && request.Status != "AwaitingPayment")
        {
            throw new InvalidOperationException($"Cannot reject request in '{request.Status}' status. Expected 'PaymentSubmitted' or 'AwaitingPayment'.");
        }

        var now = timeProvider.GetUtcNow();
        request.Status = "Rejected";
        request.ReviewedByUserId = adminUserId;
        request.ReviewedAt = now;
        request.ReviewNotes = reason.Trim();

        await db.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Admin {AdminId} rejected upgrade request {RequestId}. Reason: {Reason}.",
            adminUserId, request.Id, request.ReviewNotes);

        return request;
    }

    public async Task<MemberSubscription> GrantSubscriptionAsync(
        string userId,
        int planId,
        int durationMonths,
        string adminUserId,
        string reason,
        string? notes = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        ArgumentException.ThrowIfNullOrWhiteSpace(adminUserId);
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);

        if (durationMonths <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(durationMonths), "Manual Pro grant must have a finite term greater than zero months.");
        }

        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var plan = await db.SubscriptionPlans.FirstOrDefaultAsync(p => p.Id == planId, cancellationToken);

        if (plan is null)
        {
            throw new InvalidOperationException($"Plan {planId} was not found.");
        }

        var now = timeProvider.GetUtcNow();
        var isRelational = db.Database.IsRelational();
        await using var transaction = isRelational ? await db.Database.BeginTransactionAsync(cancellationToken) : null;

        try
        {
            var userSubscriptions = await db.MemberSubscriptions
                .Where(s => s.UserId == userId &&
                           (s.Status == SubscriptionConstants.Statuses.Active ||
                            s.Status == SubscriptionConstants.Statuses.Scheduled))
                .ToListAsync(cancellationToken);

            var currentActive = userSubscriptions.FirstOrDefault(s => s.EntitlementSlot == SubscriptionConstants.Slots.Current && s.Status == SubscriptionConstants.Statuses.Active);
            var scheduled = userSubscriptions.FirstOrDefault(s => s.EntitlementSlot == SubscriptionConstants.Slots.Scheduled && s.Status == SubscriptionConstants.Statuses.Scheduled);

            MemberSubscription newSub;

            if (currentActive is not null && currentActive.SubscriptionPlanId == plan.Id && currentActive.SubscriptionExpiryDate.HasValue && currentActive.SubscriptionExpiryDate.Value > now)
            {
                // Active Pro member: manual grant becomes scheduled renewal
                if (scheduled is not null)
                {
                    throw new InvalidOperationException("Member already has a scheduled renewal. Cancel or wait for existing renewal before granting an additional term.");
                }

                var start = currentActive.SubscriptionExpiryDate.Value;
                var expiry = start.AddMonths(durationMonths);

                newSub = new MemberSubscription
                {
                    UserId = userId,
                    SubscriptionPlanId = plan.Id,
                    IsPaid = false,
                    ManuallyGranted = true,
                    Status = SubscriptionConstants.Statuses.Scheduled,
                    EntitlementSlot = SubscriptionConstants.Slots.Scheduled,
                    StartedAt = start,
                    SubscriptionExpiryDate = expiry,
                    TerminatedAt = null,
                    CreatedAt = now,
                    CreatedByUserId = adminUserId,
                    UpdatedAt = now,
                    UpdatedByUserId = adminUserId
                };

                db.MemberSubscriptions.Add(newSub);
                await db.SaveChangesAsync(cancellationToken);

                db.SubscriptionChangeEvents.Add(new SubscriptionChangeEvent
                {
                    UserId = userId,
                    SubscriptionId = newSub.Id,
                    Action = "ManualGrantRenewal",
                    ActorType = "Admin",
                    ActorId = adminUserId,
                    OccurredAt = now,
                    Reason = reason,
                    Notes = notes,
                    NewStateJson = JsonSerializer.Serialize(new { newSub.Id, newSub.Status, newSub.EntitlementSlot, newSub.ManuallyGranted, newSub.StartedAt, newSub.SubscriptionExpiryDate })
                });
            }
            else
            {
                // Terminate previous active subscription (e.g. Standard)
                if (currentActive is not null)
                {
                    var prevJson = JsonSerializer.Serialize(new { currentActive.Id, currentActive.Status, currentActive.EntitlementSlot });
                    currentActive.Status = SubscriptionConstants.Statuses.Terminated;
                    currentActive.EntitlementSlot = null;
                    currentActive.TerminatedAt = now;
                    currentActive.UpdatedAt = now;
                    currentActive.UpdatedByUserId = adminUserId;

                    db.SubscriptionChangeEvents.Add(new SubscriptionChangeEvent
                    {
                        UserId = userId,
                        SubscriptionId = currentActive.Id,
                        Action = "Terminated",
                        ActorType = "Admin",
                        ActorId = adminUserId,
                        OccurredAt = now,
                        Reason = "Manual grant upgrade",
                        Notes = "Terminated previous subscription upon manual grant activation.",
                        PreviousStateJson = prevJson,
                        NewStateJson = JsonSerializer.Serialize(new { currentActive.Id, currentActive.Status, currentActive.EntitlementSlot, currentActive.TerminatedAt })
                    });
                }

                var expiry = now.AddMonths(durationMonths);
                newSub = new MemberSubscription
                {
                    UserId = userId,
                    SubscriptionPlanId = plan.Id,
                    IsPaid = false,
                    ManuallyGranted = true,
                    Status = SubscriptionConstants.Statuses.Active,
                    EntitlementSlot = SubscriptionConstants.Slots.Current,
                    StartedAt = now,
                    SubscriptionExpiryDate = expiry,
                    TerminatedAt = null,
                    CreatedAt = now,
                    CreatedByUserId = adminUserId,
                    UpdatedAt = now,
                    UpdatedByUserId = adminUserId
                };

                db.MemberSubscriptions.Add(newSub);
                await db.SaveChangesAsync(cancellationToken);

                db.SubscriptionChangeEvents.Add(new SubscriptionChangeEvent
                {
                    UserId = userId,
                    SubscriptionId = newSub.Id,
                    Action = "ManualGrant",
                    ActorType = "Admin",
                    ActorId = adminUserId,
                    OccurredAt = now,
                    Reason = reason,
                    Notes = notes,
                    NewStateJson = JsonSerializer.Serialize(new { newSub.Id, newSub.Status, newSub.EntitlementSlot, newSub.ManuallyGranted, newSub.StartedAt, newSub.SubscriptionExpiryDate })
                });
            }

            await db.SaveChangesAsync(cancellationToken);

            if (transaction is not null)
            {
                await transaction.CommitAsync(cancellationToken);
            }

            entitlementService.InvalidateEntitlementCache(userId);
            logger.LogInformation("Admin {AdminId} issued manual grant {SubscriptionId} for user {UserId} ({Duration} months).",
                adminUserId, newSub.Id, userId, durationMonths);

            return newSub;
        }
        catch (Exception ex)
        {
            if (transaction is not null)
            {
                await transaction.RollbackAsync(cancellationToken);
            }
            logger.LogError(ex, "Failed to issue manual grant for user {UserId}.", userId);
            throw;
        }
    }

    public async Task<MemberSubscription> CancelScheduledRenewalAsync(
        long scheduledSubscriptionId,
        string actorUserId,
        string actorType,
        string reason,
        string? notes = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(actorUserId);
        ArgumentException.ThrowIfNullOrWhiteSpace(actorType);
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);

        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        var sub = await db.MemberSubscriptions
            .FirstOrDefaultAsync(s => s.Id == scheduledSubscriptionId, cancellationToken);

        if (sub is null)
        {
            throw new InvalidOperationException($"Subscription {scheduledSubscriptionId} was not found.");
        }

        if (sub.Status != SubscriptionConstants.Statuses.Scheduled)
        {
            throw new InvalidOperationException($"Cannot cancel subscription in '{sub.Status}' status. Expected 'Scheduled'.");
        }

        var now = timeProvider.GetUtcNow();
        var prevJson = JsonSerializer.Serialize(new { sub.Id, sub.Status, sub.EntitlementSlot });

        sub.Status = SubscriptionConstants.Statuses.Cancelled;
        sub.EntitlementSlot = null;
        sub.TerminatedAt = now;
        sub.UpdatedAt = now;
        sub.UpdatedByUserId = actorUserId;

        db.SubscriptionChangeEvents.Add(new SubscriptionChangeEvent
        {
            UserId = sub.UserId,
            SubscriptionId = sub.Id,
            Action = "Cancelled",
            ActorType = actorType,
            ActorId = actorUserId,
            OccurredAt = now,
            Reason = reason,
            Notes = notes,
            PreviousStateJson = prevJson,
            NewStateJson = JsonSerializer.Serialize(new { sub.Id, sub.Status, sub.EntitlementSlot, sub.TerminatedAt })
        });

        await db.SaveChangesAsync(cancellationToken);

        if (!string.IsNullOrWhiteSpace(sub.UserId))
        {
            entitlementService.InvalidateEntitlementCache(sub.UserId);
        }

        logger.LogInformation("{ActorType} {ActorId} cancelled scheduled renewal {SubscriptionId}.", actorType, actorUserId, sub.Id);
        return sub;
    }

    public async Task<MemberSubscription> TerminateSubscriptionEarlyAsync(
        string userId,
        string actorUserId,
        string actorType,
        string reason,
        string? notes = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        ArgumentException.ThrowIfNullOrWhiteSpace(actorUserId);
        ArgumentException.ThrowIfNullOrWhiteSpace(actorType);
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);

        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var now = timeProvider.GetUtcNow();

        var isRelational = db.Database.IsRelational();
        await using var transaction = isRelational ? await db.Database.BeginTransactionAsync(cancellationToken) : null;

        try
        {
            var userSubscriptions = await db.MemberSubscriptions
                .Where(s => s.UserId == userId &&
                           (s.Status == SubscriptionConstants.Statuses.Active ||
                            s.Status == SubscriptionConstants.Statuses.Scheduled))
                .ToListAsync(cancellationToken);

            var activePro = userSubscriptions.FirstOrDefault(s => s.EntitlementSlot == SubscriptionConstants.Slots.Current && s.Status == SubscriptionConstants.Statuses.Active);
            if (activePro is null)
            {
                throw new InvalidOperationException($"User {userId} has no active subscription to terminate early.");
            }

            // 1. Terminate current active Pro (preserve original scheduled expiry)
            var prevJson = JsonSerializer.Serialize(new { activePro.Id, activePro.Status, activePro.EntitlementSlot });
            activePro.Status = SubscriptionConstants.Statuses.Terminated;
            activePro.EntitlementSlot = null;
            activePro.TerminatedAt = now;
            activePro.UpdatedAt = now;
            activePro.UpdatedByUserId = actorUserId;

            db.SubscriptionChangeEvents.Add(new SubscriptionChangeEvent
            {
                UserId = userId,
                SubscriptionId = activePro.Id,
                Action = "Terminated",
                ActorType = actorType,
                ActorId = actorUserId,
                OccurredAt = now,
                Reason = reason,
                Notes = notes,
                PreviousStateJson = prevJson,
                NewStateJson = JsonSerializer.Serialize(new { activePro.Id, activePro.Status, activePro.EntitlementSlot, activePro.TerminatedAt })
            });

            // 2. Cancel any scheduled renewal
            var scheduled = userSubscriptions.FirstOrDefault(s => s.EntitlementSlot == SubscriptionConstants.Slots.Scheduled && s.Status == SubscriptionConstants.Statuses.Scheduled);
            if (scheduled is not null)
            {
                var schedPrevJson = JsonSerializer.Serialize(new { scheduled.Id, scheduled.Status, scheduled.EntitlementSlot });
                scheduled.Status = SubscriptionConstants.Statuses.Cancelled;
                scheduled.EntitlementSlot = null;
                scheduled.TerminatedAt = now;
                scheduled.UpdatedAt = now;
                scheduled.UpdatedByUserId = actorUserId;

                db.SubscriptionChangeEvents.Add(new SubscriptionChangeEvent
                {
                    UserId = userId,
                    SubscriptionId = scheduled.Id,
                    Action = "Cancelled",
                    ActorType = actorType,
                    ActorId = actorUserId,
                    OccurredAt = now,
                    Reason = "Cancelled due to early termination of active subscription",
                    Notes = notes,
                    PreviousStateJson = schedPrevJson,
                    NewStateJson = JsonSerializer.Serialize(new { scheduled.Id, scheduled.Status, scheduled.EntitlementSlot, scheduled.TerminatedAt })
                });
            }

            // 3. Create Standard lifetime subscription
            var standardPlan = await db.SubscriptionPlans
                .FirstOrDefaultAsync(p => p.Code == SubscriptionConstants.PlanCodes.Standard, cancellationToken);

            if (standardPlan is null)
            {
                throw new InvalidOperationException("Standard plan not found to restore lifetime entitlement.");
            }

            var standardSub = new MemberSubscription
            {
                UserId = userId,
                SubscriptionPlanId = standardPlan.Id,
                IsPaid = false,
                ManuallyGranted = false,
                Status = SubscriptionConstants.Statuses.Active,
                EntitlementSlot = SubscriptionConstants.Slots.Current,
                StartedAt = now,
                SubscriptionExpiryDate = null,
                TerminatedAt = null,
                CreatedAt = now,
                CreatedByUserId = actorUserId,
                UpdatedAt = now,
                UpdatedByUserId = actorUserId
            };

            db.MemberSubscriptions.Add(standardSub);
            await db.SaveChangesAsync(cancellationToken);

            db.SubscriptionChangeEvents.Add(new SubscriptionChangeEvent
            {
                UserId = userId,
                SubscriptionId = standardSub.Id,
                Action = "RevertedToStandard",
                ActorType = actorType,
                ActorId = actorUserId,
                OccurredAt = now,
                Reason = "Early termination fallback to lifetime Standard",
                Notes = "No automatic refund issued.",
                NewStateJson = JsonSerializer.Serialize(new { standardSub.Id, standardSub.Status, standardSub.EntitlementSlot })
            });

            await db.SaveChangesAsync(cancellationToken);

            if (transaction is not null)
            {
                await transaction.CommitAsync(cancellationToken);
            }

            entitlementService.InvalidateEntitlementCache(userId);
            logger.LogInformation("{ActorType} {ActorId} terminated subscription {SubscriptionId} early for user {UserId}. Restored to Standard lifetime.",
                actorType, actorUserId, activePro.Id, userId);

            return standardSub;
        }
        catch (Exception ex)
        {
            if (transaction is not null)
            {
                await transaction.RollbackAsync(cancellationToken);
            }
            logger.LogError(ex, "Failed early termination for user {UserId}.", userId);
            throw;
        }
    }

    public async Task SuspendSubscriptionsAsync(
        string userId,
        string adminUserId,
        string reason,
        string? notes = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        ArgumentException.ThrowIfNullOrWhiteSpace(adminUserId);
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);

        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var now = timeProvider.GetUtcNow();

        var subscriptions = await db.MemberSubscriptions
            .Where(s => s.UserId == userId &&
                       (s.Status == SubscriptionConstants.Statuses.Active ||
                        s.Status == SubscriptionConstants.Statuses.Scheduled))
            .ToListAsync(cancellationToken);

        if (subscriptions.Count == 0)
        {
            return;
        }

        foreach (var sub in subscriptions)
        {
            var prevJson = JsonSerializer.Serialize(new { sub.Id, sub.Status, sub.EntitlementSlot });
            sub.Status = SubscriptionConstants.Statuses.Suspended;
            // Preserves slot!
            sub.UpdatedAt = now;
            sub.UpdatedByUserId = adminUserId;

            db.SubscriptionChangeEvents.Add(new SubscriptionChangeEvent
            {
                UserId = userId,
                SubscriptionId = sub.Id,
                Action = "Suspended",
                ActorType = "Admin",
                ActorId = adminUserId,
                OccurredAt = now,
                Reason = reason,
                Notes = notes,
                PreviousStateJson = prevJson,
                NewStateJson = JsonSerializer.Serialize(new { sub.Id, sub.Status, sub.EntitlementSlot })
            });
        }

        await db.SaveChangesAsync(cancellationToken);
        entitlementService.InvalidateEntitlementCache(userId);
        logger.LogInformation("Admin {AdminId} suspended {Count} subscriptions for user {UserId}.", adminUserId, subscriptions.Count, userId);
    }

    public async Task ResumeSubscriptionsAsync(
        string userId,
        string adminUserId,
        string reason,
        string? notes = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        ArgumentException.ThrowIfNullOrWhiteSpace(adminUserId);
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);

        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var now = timeProvider.GetUtcNow();

        var suspended = await db.MemberSubscriptions
            .Where(s => s.UserId == userId && s.Status == SubscriptionConstants.Statuses.Suspended)
            .ToListAsync(cancellationToken);

        if (suspended.Count == 0)
        {
            return;
        }

        bool hasValidActive = false;

        foreach (var sub in suspended)
        {
            var prevJson = JsonSerializer.Serialize(new { sub.Id, sub.Status, sub.EntitlementSlot });

            if (sub.EntitlementSlot == SubscriptionConstants.Slots.Current)
            {
                // Restore only if dates remain valid
                if (sub.SubscriptionExpiryDate.HasValue && sub.SubscriptionExpiryDate.Value <= now)
                {
                    // Expired while suspended
                    sub.Status = SubscriptionConstants.Statuses.Expired;
                    sub.EntitlementSlot = null;
                    sub.UpdatedAt = now;
                    sub.UpdatedByUserId = adminUserId;
                }
                else
                {
                    sub.Status = SubscriptionConstants.Statuses.Active;
                    sub.UpdatedAt = now;
                    sub.UpdatedByUserId = adminUserId;
                    hasValidActive = true;
                }
            }
            else if (sub.EntitlementSlot == SubscriptionConstants.Slots.Scheduled)
            {
                if (sub.SubscriptionExpiryDate.HasValue && sub.SubscriptionExpiryDate.Value <= now)
                {
                    sub.Status = SubscriptionConstants.Statuses.Expired;
                    sub.EntitlementSlot = null;
                }
                else
                {
                    sub.Status = SubscriptionConstants.Statuses.Scheduled;
                }
                sub.UpdatedAt = now;
                sub.UpdatedByUserId = adminUserId;
            }

            db.SubscriptionChangeEvents.Add(new SubscriptionChangeEvent
            {
                UserId = userId,
                SubscriptionId = sub.Id,
                Action = "Resumed",
                ActorType = "Admin",
                ActorId = adminUserId,
                OccurredAt = now,
                Reason = reason,
                Notes = notes,
                PreviousStateJson = prevJson,
                NewStateJson = JsonSerializer.Serialize(new { sub.Id, sub.Status, sub.EntitlementSlot })
            });
        }

        // If no active subscription remains valid, create Standard lifetime
        if (!hasValidActive)
        {
            var standardPlan = await db.SubscriptionPlans
                .FirstOrDefaultAsync(p => p.Code == SubscriptionConstants.PlanCodes.Standard, cancellationToken);

            if (standardPlan is not null)
            {
                var standardSub = new MemberSubscription
                {
                    UserId = userId,
                    SubscriptionPlanId = standardPlan.Id,
                    IsPaid = false,
                    ManuallyGranted = false,
                    Status = SubscriptionConstants.Statuses.Active,
                    EntitlementSlot = SubscriptionConstants.Slots.Current,
                    StartedAt = now,
                    SubscriptionExpiryDate = null,
                    CreatedAt = now,
                    CreatedByUserId = adminUserId,
                    UpdatedAt = now,
                    UpdatedByUserId = adminUserId
                };
                db.MemberSubscriptions.Add(standardSub);
            }
        }

        await db.SaveChangesAsync(cancellationToken);
        entitlementService.InvalidateEntitlementCache(userId);
        logger.LogInformation("Admin {AdminId} resumed subscriptions for user {UserId}.", adminUserId, userId);
    }

    public async Task<IReadOnlyList<SubscriptionUpgradeRequest>> GetPendingUpgradeRequestsAsync(
        CancellationToken cancellationToken = default)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        return await db.SubscriptionUpgradeRequests
            .Where(r => r.Status == "PaymentSubmitted" || r.Status == "AwaitingPayment")
            .OrderBy(r => r.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<MemberSubscription>> GetSubscriptionHistoryAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Array.Empty<MemberSubscription>();
        }

        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        return await db.MemberSubscriptions
            .Where(s => s.UserId == userId)
            .OrderByDescending(s => s.StartedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<SubscriptionChangeEvent>> GetSubscriptionAuditTrailAsync(
        string? userId = null,
        long? subscriptionId = null,
        CancellationToken cancellationToken = default)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        var query = db.SubscriptionChangeEvents.AsQueryable();

        if (!string.IsNullOrWhiteSpace(userId))
        {
            query = query.Where(e => e.UserId == userId);
        }

        if (subscriptionId.HasValue)
        {
            query = query.Where(e => e.SubscriptionId == subscriptionId.Value);
        }

        return await query
            .OrderByDescending(e => e.OccurredAt)
            .ToListAsync(cancellationToken);
    }
}

