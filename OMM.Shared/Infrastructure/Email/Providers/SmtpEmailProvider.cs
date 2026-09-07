using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace OMM.Shared.Infrastructure.Email.Providers;

public sealed class SmtpEmailProvider : IEmailProvider
{
    public const string Name = "Smtp";
    public string ProviderName => Name;

    private readonly IOptions<EmailOptions> _options;
    private readonly ILogger<SmtpEmailProvider> _logger;

    public SmtpEmailProvider(
        IOptions<EmailOptions> options,
        ILogger<SmtpEmailProvider> logger)
    {
        _options = options;
        _logger = logger;
    }

    public async Task SendEmailAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        var options = _options.Value;
        var smtp = options.Smtp;

        if (string.IsNullOrWhiteSpace(smtp.Host))
        {
            throw new InvalidOperationException("SMTP host is not configured in Email:Smtp:Host.");
        }

        var fromAddress = !string.IsNullOrWhiteSpace(message.FromAddress)
            ? message.FromAddress
            : options.FromAddress;

        if (string.IsNullOrWhiteSpace(fromAddress))
        {
            var msg = "Sender email address is not configured. Set 'Email:FromAddress' in appsettings.json or environment variables.";
            _logger.LogError("{ErrorMessage}", msg);
            throw new InvalidOperationException(msg);
        }

        var fromName = !string.IsNullOrWhiteSpace(message.FromName)
            ? message.FromName
            : options.FromName;

        using var client = new SmtpClient(smtp.Host, smtp.Port)
        {
            EnableSsl = smtp.EnableSsl
        };

        if (!string.IsNullOrWhiteSpace(smtp.Username))
        {
            client.Credentials = new NetworkCredential(smtp.Username, smtp.Password);
        }

        using var mailMessage = new MailMessage
        {
            From = new MailAddress(fromAddress, fromName),
            Subject = message.Subject,
            Body = message.HtmlBody,
            IsBodyHtml = true
        };
        mailMessage.To.Add(message.To);

        cancellationToken.ThrowIfCancellationRequested();

        _logger.LogInformation("Sending email via SMTP ({Host}:{Port}) to {Recipient}...", smtp.Host, smtp.Port, message.To);
        await client.SendMailAsync(mailMessage, cancellationToken);
        _logger.LogInformation("Successfully sent email via SMTP to {Recipient}.", message.To);
    }
}
