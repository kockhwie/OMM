using Microsoft.EntityFrameworkCore;
using OMM.Public.Data.Entities;
using OMM.Shared.Models.Phase5;
using OMM.Shared.Subscription;

namespace OMM.Public.Data;

public sealed class PublicMemberMigrationService(
    IDbContextFactory<ApplicationDbContext> dbContextFactory,
    ILogger<PublicMemberMigrationService> logger)
{
    public async Task<int> EnsureMinerProfilesAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        var users = await db.Users
            .AsNoTracking()
            .Where(user => user.Email != null)
            .Select(user => new { user.Id, user.Email, user.UserName })
            .ToListAsync(cancellationToken);

        var existingUserIds = await db.MinerProfiles
            .IgnoreQueryFilters()
            .Select(profile => profile.UserId)
            .ToHashSetAsync(cancellationToken);

        var now = DateTimeOffset.UtcNow;
        var missingProfiles = users
            .Where(user => !existingUserIds.Contains(user.Id))
            .Select(user => new MinerProfileEntity
            {
                UserId = user.Id,
                Email = user.Email ?? user.UserName ?? string.Empty,
                CreatedAt = now,
                IsDeleted = false
            })
            .ToList();

        if (missingProfiles.Count == 0)
        {
            return 0;
        }

        db.MinerProfiles.AddRange(missingProfiles);
        await db.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Created {ProfileCount} missing Public miner profiles for existing Identity users.",
            missingProfiles.Count);
        return missingProfiles.Count;
    }

    public async Task<int> EnsureStandardSubscriptionsAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        var users = await db.Users
            .AsNoTracking()
            .Select(user => user.Id)
            .ToListAsync(cancellationToken);

        var existingUserIds = await db.MemberSubscriptions
            .Where(s => s.UserId != null && s.Status == SubscriptionConstants.Statuses.Active)
            .Select(s => s.UserId!)
            .ToHashSetAsync(cancellationToken);

        var standardPlan = await db.SubscriptionPlans
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Code == SubscriptionConstants.PlanCodes.Standard, cancellationToken);

        if (standardPlan is null)
        {
            logger.LogWarning("Standard subscription plan not found. Cannot backfill subscriptions.");
            return 0;
        }

        var missingUserIds = users.Where(id => !existingUserIds.Contains(id)).ToList();
        if (missingUserIds.Count == 0)
        {
            return 0;
        }

        var now = DateTimeOffset.UtcNow;
        foreach (var userId in missingUserIds)
        {
            var sub = new MemberSubscription
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
            db.MemberSubscriptions.Add(sub);
        }

        await db.SaveChangesAsync(cancellationToken);
        logger.LogInformation(
            "Provisioned initial Standard subscriptions for {Count} existing users without an active subscription.",
            missingUserIds.Count);
        return missingUserIds.Count;
    }
}
