using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace OMM.Shared.Prompts;

public static class PromptServiceCollectionExtensions
{
    public static IServiceCollection AddSharedPromptServices(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<EmergencyPromptCatalog>();
        services.AddHostedService<EmergencyPromptValidationHostedService>();
        services.AddScoped<PromptAdministrationService>();
        services.AddScoped<IPromptProvider>(provider => provider.GetRequiredService<PromptAdministrationService>());
        services.AddScoped<IPromptAdministrationService>(provider => provider.GetRequiredService<PromptAdministrationService>());
        return services;
    }
}

internal sealed class EmergencyPromptValidationHostedService(
    EmergencyPromptCatalog catalog,
    ILogger<EmergencyPromptValidationHostedService> logger) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("Validated {PromptCount} embedded Phase 5 emergency prompt baselines.", catalog.GetAll().Count);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
