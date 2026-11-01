using Microsoft.Extensions.DependencyInjection;
using OMM.Shared.Prompts;

namespace OMM.Shared.Subscription;

public static class SubscriptionServiceCollectionExtensions
{
    public static IServiceCollection AddSharedSubscriptionServices(this IServiceCollection services)
    {
        services.AddSingleton(TimeProvider.System);
        services.AddScoped<ISubscriptionEntitlementService, SubscriptionEntitlementService>();
        services.AddScoped<ISubscriptionProvisioningService, SubscriptionProvisioningService>();
        services.AddScoped<IAccountRetentionService, AccountRetentionService>();
        services.AddScoped<ISubscriptionUpgradeService, SubscriptionUpgradeService>();
        services.AddScoped<ISubscriptionAdminService, SubscriptionAdminService>();
        services.AddScoped<ISubscriptionNotificationDispatcher, DefaultSubscriptionNotificationDispatcher>();
        services.AddScoped<ISubscriptionLifecycleProcessor, SubscriptionLifecycleProcessor>();
        services.AddSharedPromptServices();
        return services;
    }
}

