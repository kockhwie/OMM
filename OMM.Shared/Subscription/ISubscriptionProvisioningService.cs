using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OMM.Shared.Models.Phase5;

namespace OMM.Shared.Subscription;

public interface ISubscriptionProvisioningService
{
    Task<MemberSubscription> ProvisionStandardSubscriptionAsync(string userId, CancellationToken cancellationToken = default);
}

public sealed class SubscriptionProvisioningService(
    ISubscriptionDbContextFactory dbContextFactory,
    ISubscriptionEntitlementService entitlementService,
    TimeProvider timeProvider,
    ILogger<SubscriptionProvisioningService> logger) : ISubscriptionProvisioningService
{
    public async Task<MemberSubscription> ProvisionStandardSubscriptionAsync(string userId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);

        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        // Check if user already has an active subscription (idempotent)
        var existing = await db.MemberSubscriptions
            .FirstOrDefaultAsync(s => s.UserId == userId && s.Status == SubscriptionConstants.Statuses.Active, cancellationToken);

        if (existing is not null)
        {
            logger.LogDebug("User {UserId} already has an active subscription {SubscriptionId}.", userId, existing.Id);
            return existing;
        }

        var standardPlan = await db.SubscriptionPlans
            .FirstOrDefaultAsync(p => p.Code == SubscriptionConstants.PlanCodes.Standard, cancellationToken);

        if (standardPlan is null)
        {
            throw new InvalidOperationException("Standard subscription plan is not configured in the database.");
        }

        var now = timeProvider.GetUtcNow();
        var isRelational = db.Database.IsRelational();
        await using var transaction = isRelational ? await db.Database.BeginTransactionAsync(cancellationToken) : null;

        try
        {
            var subscription = new MemberSubscription
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
                CreatedByUserId = userId,
                UpdatedAt = now,
                UpdatedByUserId = userId
            };

            db.MemberSubscriptions.Add(subscription);
            await db.SaveChangesAsync(cancellationToken);

            var changeEvent = new SubscriptionChangeEvent
            {
                UserId = userId,
                SubscriptionId = subscription.Id,
                Action = "Provisioned",
                ActorType = "Member",
                ActorId = userId,
                OccurredAt = now,
                Reason = "Initial registration provisioning",
                Notes = "Standard lifetime entitlement provisioned on registration.",
                NewStateJson = JsonSerializer.Serialize(new
                {
                    subscription.Id,
                    subscription.UserId,
                    subscription.SubscriptionPlanId,
                    subscription.Status,
                    subscription.EntitlementSlot,
                    subscription.StartedAt,
                    subscription.SubscriptionExpiryDate
                })
            };

            db.SubscriptionChangeEvents.Add(changeEvent);
            await db.SaveChangesAsync(cancellationToken);

            if (transaction is not null)
            {
                await transaction.CommitAsync(cancellationToken);
            }

            entitlementService.InvalidateEntitlementCache(userId);
            logger.LogInformation("Provisioned Standard lifetime subscription {SubscriptionId} for user {UserId}.", subscription.Id, userId);

            return subscription;
        }
        catch (Exception ex)
        {
            if (transaction is not null)
            {
                await transaction.RollbackAsync(cancellationToken);
            }
            logger.LogError(ex, "Failed to provision Standard lifetime subscription for user {UserId}.", userId);
            throw;
        }
    }
}

