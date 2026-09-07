using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OMM.Shared.Infrastructure.Email.Providers;

namespace OMM.Shared.Infrastructure.Email;

public static class EmailServiceCollectionExtensions
{
    /// <summary>
    /// Registers shared email infrastructure (IEmailService, providers for Resend, Smtp, NoOp).
    /// </summary>
    public static IServiceCollection AddSharedEmail(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddOptions<EmailOptions>()
            .Bind(configuration.GetSection("Email"));

        // Register HTTP client for Resend
        services.AddHttpClient<ResendEmailProvider>();

        // Register all available providers
        services.AddTransient<IEmailProvider>(sp => sp.GetRequiredService<ResendEmailProvider>());
        services.AddTransient<IEmailProvider, SmtpEmailProvider>();
        services.AddTransient<IEmailProvider, NoOpEmailProvider>();

        // Register dispatcher service
        services.TryAddTransient<IEmailService, EmailService>();

        // Standard non-generic IEmailSender for components that inject Microsoft.AspNetCore.Identity.UI.Services.IEmailSender
        services.TryAddTransient<IEmailSender, SharedIdentityEmailSender<object>>();

        return services;
    }

    /// <summary>
    /// Registers shared email infrastructure plus Microsoft.AspNetCore.Identity.IEmailSender&lt;TUser&gt;.
    /// </summary>
    public static IServiceCollection AddSharedIdentityEmail<TUser>(
        this IServiceCollection services,
        IConfiguration configuration)
        where TUser : class
    {
        services.AddSharedEmail(configuration);
        services.AddTransient<IEmailSender<TUser>, SharedIdentityEmailSender<TUser>>();
        return services;
    }
}
