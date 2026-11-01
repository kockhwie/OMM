using Microsoft.EntityFrameworkCore;
using OMM.Public.Data;
using OMM.Public.Data.Entities;
using OMM.Shared.Models.Phase5;
using OMM.Shared.Subscription;

namespace OMM.Public.Services;

public sealed class PublicSubscriptionNotificationDispatcher(
    IDbContextFactory<ApplicationDbContext> dbContextFactory) : ISubscriptionNotificationDispatcher
{
    public async Task DispatchDowngradeNotificationAsync(
        SubscriptionDowngradeEvent downgradeEvent,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(downgradeEvent.UserId))
        {
            return;
        }

        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        var dedupeKey = $"downgrade_{downgradeEvent.SourceSubscriptionId}";
        var alreadyExists = await db.Notifications
            .AnyAsync(n => n.UserId == downgradeEvent.UserId && n.DedupeKey == dedupeKey, cancellationToken);

        if (alreadyExists)
        {
            return;
        }

        var notification = new NotificationEntity
        {
            UserId = downgradeEvent.UserId,
            Title = "Pro Subscription Expired",
            Body = "Your Pro subscription period has ended and your account has been converted to lifetime Standard access. You can renew Pro at any time.",
            Type = "SubscriptionDowngrade",
            DedupeKey = dedupeKey,
            OccurredOn = DateOnly.FromDateTime(downgradeEvent.DetectedAt.UtcDateTime),
            CreatedAt = DateTimeOffset.UtcNow,
            IsDeleted = false
        };

        db.Notifications.Add(notification);
        await db.SaveChangesAsync(cancellationToken);
    }
}

