using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace OMM.Shared.DecisionSupport;

public static class DecisionSupportServiceCollectionExtensions
{
    public static IServiceCollection AddSharedDecisionSupport(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddOptions<GoogleAiOptions>()
            .Bind(configuration.GetSection("GoogleAi"));

        services.AddHttpClient<IInquiryIntentInterpreter, GoogleAiIntentInterpreter>((serviceProvider, client) =>
        {
            var options = serviceProvider.GetRequiredService<Microsoft.Extensions.Options.IOptions<GoogleAiOptions>>().Value;
            client.Timeout = TimeSpan.FromSeconds(Math.Clamp(options.TimeoutSeconds, 5, 60));
        });

        services.AddSingleton<IDeterministicDecisionEngine, DeterministicDecisionEngine>();
        if (services.Any(descriptor => descriptor.ServiceType == typeof(IDecisionPathwayStore)))
        {
            services.AddScoped<IDecisionPathwayService, DecisionPathwayService>();
        }

        return services;
    }
}
