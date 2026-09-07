namespace OMM.Shared.Infrastructure.Email;

public interface IEmailService
{
    /// <summary>
    /// Sends an email using the active provider.
    /// </summary>
    Task SendEmailAsync(EmailMessage message, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends an email with default sender settings.
    /// </summary>
    Task SendEmailAsync(string recipient, string subject, string htmlBody, CancellationToken cancellationToken = default);
}
