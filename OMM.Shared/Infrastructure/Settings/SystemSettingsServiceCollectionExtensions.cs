using Microsoft.Extensions.DependencyInjection;

namespace OMM.Shared.Infrastructure.Settings;

public static class SystemSettingsServiceCollectionExtensions
{
    public static IServiceCollection AddSharedSystemSettings(this IServiceCollection services)
    {
        services.AddMemoryCache();
        services.AddSingleton<ISystemSettingsService, SystemSettingsService>();
        return services;
    }
}
