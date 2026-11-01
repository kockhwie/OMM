using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OMM.Shared.Models.Phase5;

namespace OMM.Shared.Subscription;

public interface ISubscriptionUpgradeService
{
    Task<SubscriptionUpgradeRequest> CreateUpgradeRequestAsync(string userId, int targetPlanId, CancellationToken cancellationToken = default);
    Task<SubscriptionUpgradeRequest> SubmitPaymentReferenceAsync(long upgradeRequestId, string userId, string paymentReference, CancellationToken cancellationToken = default);
    Task<SubscriptionUpgradeRequest> WithdrawUpgradeRequestAsync(long upgradeRequestId, string userId, CancellationToken cancellationToken = default);
    Task<SubscriptionUpgradeRequest?> GetActiveUpgradeRequestAsync(string userId, int? planId = null, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SubscriptionUpgradeRequest>> GetMemberUpgradeRequestsAsync(string userId, CancellationToken cancellationToken = default);
}

public sealed class SubscriptionUpgradeService(
    ISubscriptionDbContextFactory dbContextFactory,
    TimeProvider timeProvider,
    ILogger<SubscriptionUpgradeService> logger) : ISubscriptionUpgradeService
{
    public async Task<SubscriptionUpgradeRequest> CreateUpgradeRequestAsync(
        string userId,
        int targetPlanId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);

        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        // Check if an open request already exists for this member and plan
        var existingOpenRequest = await db.SubscriptionUpgradeRequests
            .FirstOrDefaultAsync(r => r.UserId == userId &&
                                     r.SubscriptionPlanId == targetPlanId &&
                                     (r.Status == "AwaitingPayment" || r.Status == "PaymentSubmitted"),
                                 cancellationToken);

        if (existingOpenRequest is not null)
        {
            logger.LogDebug("User {UserId} already has an open upgrade request {RequestId} for plan {PlanId}.",
                userId, existingOpenRequest.Id, targetPlanId);
            return existingOpenRequest;
        }

        var plan = await db.SubscriptionPlans
            .FirstOrDefaultAsync(p => p.Id == targetPlanId && p.IsActive, cancellationToken);

        if (plan is null)
        {
            throw new InvalidOperationException($"Target subscription plan {targetPlanId} not found or is inactive.");
        }

        // Enforce: at most one current Active and one future Scheduled subscription per user
        var userSubscriptions = await db.MemberSubscriptions
            .Where(s => s.UserId == userId &&
                       (s.Status == SubscriptionConstants.Statuses.Active ||
                        s.Status == SubscriptionConstants.Statuses.Scheduled))
            .ToListAsync(cancellationToken);

        var hasCurrentPro = userSubscriptions.Any(s => s.EntitlementSlot == SubscriptionConstants.Slots.Current &&
                                                      s.SubscriptionPlanId == targetPlanId);
        var hasScheduled = userSubscriptions.Any(s => s.EntitlementSlot == SubscriptionConstants.Slots.Scheduled);

        if (hasCurrentPro && hasScheduled)
        {
            throw new InvalidOperationException("You already have an active subscription and a scheduled renewal. Additional renewal requests are not permitted at this time.");
        }

        var now = timeProvider.GetUtcNow();
        var request = new SubscriptionUpgradeRequest
        {
            UserId = userId,
            SubscriptionPlanId = targetPlanId,
            AmountDue = plan.PriceAmount ?? 0m,
            CurrencyCode = plan.CurrencyCode ?? "MYR",
            Status = "AwaitingPayment",
            PaymentReference = null,
            CreatedAt = now,
            SubmittedAt = null,
            ReviewedAt = null,
            ReviewedByUserId = null,
            ReviewNotes = null,
            ResultingSubscriptionId = null
        };

        db.SubscriptionUpgradeRequests.Add(request);
        await db.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Created upgrade request {RequestId} for user {UserId} (Plan: {PlanId}, Amount: {Amount} {Currency}).",
            request.Id, userId, plan.Id, request.AmountDue, request.CurrencyCode);

        return request;
    }

    public async Task<SubscriptionUpgradeRequest> SubmitPaymentReferenceAsync(
        long upgradeRequestId,
        string userId,
        string paymentReference,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        ArgumentException.ThrowIfNullOrWhiteSpace(paymentReference);

        var trimmedRef = paymentReference.Trim();
        if (string.IsNullOrWhiteSpace(trimmedRef))
        {
            throw new ArgumentException("Payment reference cannot be empty.", nameof(paymentReference));
        }

        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        var request = await db.SubscriptionUpgradeRequests
            .FirstOrDefaultAsync(r => r.Id == upgradeRequestId && r.UserId == userId, cancellationToken);

        if (request is null)
        {
            throw new InvalidOperationException($"Upgrade request {upgradeRequestId} was not found for user {userId}.");
        }

        if (request.Status != "AwaitingPayment")
        {
            throw new InvalidOperationException($"Cannot submit payment reference for request in '{request.Status}' status. Expected 'AwaitingPayment'.");
        }

        var now = timeProvider.GetUtcNow();
        request.PaymentReference = trimmedRef;
        request.Status = "PaymentSubmitted";
        request.SubmittedAt = now;

        await db.SaveChangesAsync(cancellationToken);

        logger.LogInformation("User {UserId} submitted payment reference for upgrade request {RequestId}.", userId, request.Id);
        return request;
    }

    public async Task<SubscriptionUpgradeRequest> WithdrawUpgradeRequestAsync(
        long upgradeRequestId,
        string userId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);

        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        var request = await db.SubscriptionUpgradeRequests
            .FirstOrDefaultAsync(r => r.Id == upgradeRequestId && r.UserId == userId, cancellationToken);

        if (request is null)
        {
            throw new InvalidOperationException($"Upgrade request {upgradeRequestId} was not found for user {userId}.");
        }

        if (request.Status != "AwaitingPayment" && request.Status != "PaymentSubmitted")
        {
            throw new InvalidOperationException($"Cannot withdraw request in '{request.Status}' status. Only open requests may be withdrawn.");
        }

        var now = timeProvider.GetUtcNow();
        request.Status = "Withdrawn";
        request.ReviewedAt = now;
        request.ReviewNotes = "Withdrawn by member.";

        await db.SaveChangesAsync(cancellationToken);
        logger.LogInformation("User {UserId} withdrew upgrade request {RequestId}.", userId, request.Id);
        return request;
    }

    public async Task<SubscriptionUpgradeRequest?> GetActiveUpgradeRequestAsync(
        string userId,
        int? planId = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            return null;
        }

        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        var query = db.SubscriptionUpgradeRequests
            .Where(r => r.UserId == userId && (r.Status == "AwaitingPayment" || r.Status == "PaymentSubmitted"));

        if (planId.HasValue)
        {
            query = query.Where(r => r.SubscriptionPlanId == planId.Value);
        }

        return await query.OrderByDescending(r => r.CreatedAt).FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<SubscriptionUpgradeRequest>> GetMemberUpgradeRequestsAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Array.Empty<SubscriptionUpgradeRequest>();
        }

        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        return await db.SubscriptionUpgradeRequests
            .Where(r => r.UserId == userId)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync(cancellationToken);
    }
}

