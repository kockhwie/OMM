using Microsoft.Extensions.Logging;

namespace OMM.Shared.Infrastructure.Email.Providers;

public sealed class NoOpEmailProvider : IEmailProvider
{
    public const string Name = "NoOp";
    public string ProviderName => Name;

    private readonly ILogger<NoOpEmailProvider> _logger;

    public NoOpEmailProvider(ILogger<NoOpEmailProvider> logger)
    {
        _logger = logger;
    }

    public Task SendEmailAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "[NoOpEmailProvider] Email to '{Recipient}' with subject '{Subject}' was not sent because the NoOp provider is active.",
            message.To,
            message.Subject);

        return Task.CompletedTask;
    }
}
