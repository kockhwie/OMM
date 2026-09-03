using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OMM.Admin.Data;

namespace OMM.Admin.Components.Account;

internal sealed class ResendEmailSender : IEmailSender<ApplicationUser>
{
    private const string ApiKeyConfigurationName = "Resend_EmailOnboardingApi";
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<ResendEmailSender> _logger;

    public ResendEmailSender(
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<ResendEmailSender> logger)
    {
        _httpClient = httpClient;
        _configuration = configuration;
        _logger = logger;
    }

    public Task SendConfirmationLinkAsync(ApplicationUser user, string email, string confirmationLink) =>
        SendEmailAsync(email, "Confirm your OMM Admin email", $"Please confirm your email by <a href=\"{confirmationLink}\">clicking here</a>.");

    public Task SendPasswordResetLinkAsync(ApplicationUser user, string email, string resetLink) =>
        SendEmailAsync(email, "Your OMM Admin invitation", $"You have been invited to OMM Admin. Set your password by <a href=\"{resetLink}\">clicking here</a>.");

    public Task SendPasswordResetCodeAsync(ApplicationUser user, string email, string resetCode) =>
        SendEmailAsync(email, "Your OMM Admin password reset code", $"Your password reset code is: <strong>{resetCode}</strong>");

    public Task SendEmailAsync(string recipient, string subject, string html) =>
        SendEmailCoreAsync(recipient, subject, html);

    private async Task SendEmailCoreAsync(string recipient, string subject, string html)
    {
        var apiKey = _configuration[ApiKeyConfigurationName];
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new InvalidOperationException($"The {ApiKeyConfigurationName} configuration value is required to send email.");

        var fromAddress = _configuration["Email:FromAddress"] ?? "admin@codingdinos.asia";
        var fromName = _configuration["Email:FromName"] ?? "OMM Admin";
        var baseUrl = _configuration["Resend:BaseUrl"] ?? "https://api.resend.com";

        using var request = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl.TrimEnd('/')}/emails");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        request.Content = JsonContent.Create(new ResendEmailRequest(
            $"{fromName} <{fromAddress}>",
            [recipient],
            subject,
            html));

        using var response = await _httpClient.SendAsync(request);
        if (response.IsSuccessStatusCode)
            return;

        var responseBody = await response.Content.ReadAsStringAsync();
        _logger.LogError("Resend email delivery failed with status {StatusCode}: {ResponseBody}", response.StatusCode, responseBody);
        throw new InvalidOperationException($"Resend email delivery failed with status code {(int)response.StatusCode}.");
    }

    private sealed record ResendEmailRequest(
        [property: JsonPropertyName("from")] string From,
        [property: JsonPropertyName("to")] string[] To,
        [property: JsonPropertyName("subject")] string Subject,
        [property: JsonPropertyName("html")] string Html);
}
