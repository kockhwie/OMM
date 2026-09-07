using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.Extensions.Options;

namespace OMM.Shared.Infrastructure.Email;

public class SharedIdentityEmailSender<TUser> : IEmailSender<TUser>, IEmailSender where TUser : class
{
    private readonly IEmailService _emailService;
    private readonly IOptions<EmailOptions> _options;

    public SharedIdentityEmailSender(
        IEmailService emailService,
        IOptions<EmailOptions> options)
    {
        _emailService = emailService;
        _options = options;
    }

    public Task SendConfirmationLinkAsync(TUser user, string email, string confirmationLink)
    {
        var appPrefix = !string.IsNullOrWhiteSpace(_options.Value.FromName) ? $"{_options.Value.FromName} " : "";
        return _emailService.SendEmailAsync(
            email,
            $"Confirm your {appPrefix}account",
            $"Please confirm your email by <a href=\"{confirmationLink}\">clicking here</a>.");
    }

    public Task SendPasswordResetLinkAsync(TUser user, string email, string resetLink)
    {
        var appPrefix = !string.IsNullOrWhiteSpace(_options.Value.FromName) ? $"{_options.Value.FromName} " : "";
        return _emailService.SendEmailAsync(
            email,
            $"Reset your {appPrefix}password",
            $"Please reset your password by <a href=\"{resetLink}\">clicking here</a>.");
    }

    public Task SendPasswordResetCodeAsync(TUser user, string email, string resetCode)
    {
        var appPrefix = !string.IsNullOrWhiteSpace(_options.Value.FromName) ? $"{_options.Value.FromName} " : "";
        return _emailService.SendEmailAsync(
            email,
            $"{appPrefix}Password reset code",
            $"Your password reset code is: <strong>{resetCode}</strong>");
    }

    public Task SendEmailAsync(string email, string subject, string htmlMessage)
    {
        return _emailService.SendEmailAsync(email, subject, htmlMessage);
    }
}
