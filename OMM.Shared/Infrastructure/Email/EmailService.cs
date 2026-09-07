using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace OMM.Shared.Infrastructure.Email;

public sealed class EmailService : IEmailService
{
    private readonly IEnumerable<IEmailProvider> _providers;
    private readonly IOptions<EmailOptions> _options;
    private readonly ILogger<EmailService> _logger;

    public EmailService(
        IEnumerable<IEmailProvider> providers,
        IOptions<EmailOptions> options,
        ILogger<EmailService> logger)
    {
        _providers = providers;
        _options = options;
        _logger = logger;
    }

    public Task SendEmailAsync(string recipient, string subject, string htmlBody, CancellationToken cancellationToken = default)
    {
        var options = _options.Value;
        var message = new EmailMessage(
            To: recipient,
            Subject: subject,
            HtmlBody: htmlBody,
            FromAddress: options.FromAddress,
            FromName: options.FromName);

        return SendEmailAsync(message, cancellationToken);
    }

    public async Task SendEmailAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        var options = _options.Value;
        var providerName = options.Provider;

        var provider = _providers.FirstOrDefault(p =>
            string.Equals(p.ProviderName, providerName, StringComparison.OrdinalIgnoreCase));

        if (provider == null)
        {
            var available = string.Join(", ", _providers.Select(p => p.ProviderName));
            _logger.LogError(
                "Email provider '{ProviderName}' requested in Email:Provider configuration was not found. Available providers: {Available}",
                providerName,
                available);

            throw new InvalidOperationException(
                $"Configured email provider '{providerName}' is not registered. Available providers: {available}");
        }

        // Apply global defaults if not specified on the message
        var resolvedMessage = message with
        {
            FromAddress = string.IsNullOrWhiteSpace(message.FromAddress) ? options.FromAddress : message.FromAddress,
            FromName = string.IsNullOrWhiteSpace(message.FromName) ? options.FromName : message.FromName
        };

        await provider.SendEmailAsync(resolvedMessage, cancellationToken);
    }
}
