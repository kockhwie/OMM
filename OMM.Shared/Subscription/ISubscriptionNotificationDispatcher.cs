using OMM.Shared.Models.Phase5;

namespace OMM.Shared.Subscription;

public interface ISubscriptionNotificationDispatcher
{
    Task DispatchDowngradeNotificationAsync(SubscriptionDowngradeEvent downgradeEvent, CancellationToken cancellationToken = default);
}

public sealed class DefaultSubscriptionNotificationDispatcher : ISubscriptionNotificationDispatcher
{
    public Task DispatchDowngradeNotificationAsync(SubscriptionDowngradeEvent downgradeEvent, CancellationToken cancellationToken = default)
    {
        // Default no-op dispatcher when external notification system is not configured
        return Task.CompletedTask;
    }
}

