using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OMM.Shared.Models.Phase5;

namespace OMM.Shared.Subscription;

public interface IAccountRetentionService
{
    Task PrepareForAccountDeletionAsync(string userId, CancellationToken cancellationToken = default);
}

public sealed class AccountRetentionService(
    ISubscriptionDbContextFactory dbContextFactory,
    ISubscriptionEntitlementService entitlementService,
    TimeProvider timeProvider,
    ILogger<AccountRetentionService> logger) : IAccountRetentionService
{
    public async Task PrepareForAccountDeletionAsync(string userId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);

        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var now = timeProvider.GetUtcNow();

        var isRelational = db.Database.IsRelational();
        await using var transaction = isRelational ? await db.Database.BeginTransactionAsync(cancellationToken) : null;

        try
        {
            // 1. Terminate any Active or Suspended subscriptions and clear slots
            var activeOrSuspended = await db.MemberSubscriptions
                .Where(s => s.UserId == userId &&
                           (s.Status == SubscriptionConstants.Statuses.Active || s.Status == SubscriptionConstants.Statuses.Suspended))
                .ToListAsync(cancellationToken);

            foreach (var sub in activeOrSuspended)
            {
                var prevJson = JsonSerializer.Serialize(new { sub.Id, sub.Status, sub.EntitlementSlot });
                sub.Status = SubscriptionConstants.Statuses.Terminated;
                sub.EntitlementSlot = null;
                sub.TerminatedAt = now;
                sub.UpdatedAt = now;
                sub.UpdatedByUserId = userId;

                db.SubscriptionChangeEvents.Add(new SubscriptionChangeEvent
                {
                    UserId = userId,
                    SubscriptionId = sub.Id,
                    Action = "Terminated",
                    ActorType = "Member",
                    ActorId = userId,
                    OccurredAt = now,
                    Reason = "Account deletion",
                    Notes = "Subscription terminated due to member account closure.",
                    PreviousStateJson = prevJson,
                    NewStateJson = JsonSerializer.Serialize(new { sub.Id, sub.Status, sub.EntitlementSlot, sub.TerminatedAt })
                });
            }

            // 2. Cancel any Scheduled subscriptions and clear slots
            var scheduled = await db.MemberSubscriptions
                .Where(s => s.UserId == userId && s.Status == SubscriptionConstants.Statuses.Scheduled)
                .ToListAsync(cancellationToken);

            foreach (var sub in scheduled)
            {
                var prevJson = JsonSerializer.Serialize(new { sub.Id, sub.Status, sub.EntitlementSlot });
                sub.Status = SubscriptionConstants.Statuses.Cancelled;
                sub.EntitlementSlot = null;
                sub.TerminatedAt = now;
                sub.UpdatedAt = now;
                sub.UpdatedByUserId = userId;

                db.SubscriptionChangeEvents.Add(new SubscriptionChangeEvent
                {
                    UserId = userId,
                    SubscriptionId = sub.Id,
                    Action = "Cancelled",
                    ActorType = "Member",
                    ActorId = userId,
                    OccurredAt = now,
                    Reason = "Account deletion",
                    Notes = "Scheduled subscription cancelled due to member account closure.",
                    PreviousStateJson = prevJson,
                    NewStateJson = JsonSerializer.Serialize(new { sub.Id, sub.Status, sub.EntitlementSlot, sub.TerminatedAt })
                });
            }

            // 3. Withdraw open upgrade requests
            var openRequests = await db.SubscriptionUpgradeRequests
                .Where(r => r.UserId == userId &&
                           (r.Status == "AwaitingPayment" || r.Status == "PaymentSubmitted"))
                .ToListAsync(cancellationToken);

            // Retain inquiry telemetry and raw-message ciphertext; only sever the deleted identity link.
            var inquirySessions = await db.InquirySessions
                .Where(session => session.UserId == userId)
                .ToListAsync(cancellationToken);
            foreach (var session in inquirySessions)
            {
                session.UserId = null;
            }

            var inquiryTelemetry = await db.InquiryTelemetryEvents
                .Where(item => item.UserId == userId)
                .ToListAsync(cancellationToken);
            foreach (var item in inquiryTelemetry)
            {
                item.UserId = null;
            }

            foreach (var req in openRequests)
            {
                req.Status = "Withdrawn";
                req.ReviewNotes = "Withdrawn due to member account closure.";
                req.ReviewedAt = now;
            }

            await db.SaveChangesAsync(cancellationToken);

            if (transaction is not null)
            {
                await transaction.CommitAsync(cancellationToken);
            }

            entitlementService.InvalidateEntitlementCache(userId);

            logger.LogInformation(
                "Prepared account retention for user '{UserId}': {TerminatedCount} subscriptions terminated, {CancelledCount} scheduled subscriptions cancelled, {WithdrawnCount} upgrade requests withdrawn.",
                userId,
                activeOrSuspended.Count,
                scheduled.Count,
                openRequests.Count);
        }
        catch (Exception ex)
        {
            if (transaction is not null)
            {
                await transaction.RollbackAsync(cancellationToken);
            }
            logger.LogError(ex, "Failed to prepare account retention for user '{UserId}'.", userId);
            throw;
        }
    }
}

