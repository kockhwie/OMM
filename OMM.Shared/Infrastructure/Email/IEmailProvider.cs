namespace OMM.Shared.Infrastructure.Email;

public interface IEmailProvider
{
    /// <summary>
    /// The unique name of the provider (e.g. "Resend", "Smtp", "NoOp").
    /// </summary>
    string ProviderName { get; }

    /// <summary>
    /// Transmits the email message.
    /// </summary>
    Task SendEmailAsync(EmailMessage message, CancellationToken cancellationToken = default);
}
