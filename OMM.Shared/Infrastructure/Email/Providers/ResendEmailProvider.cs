using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace OMM.Shared.Infrastructure.Email.Providers;

public sealed class ResendEmailProvider : IEmailProvider
{
    public const string Name = "Resend";
    public string ProviderName => Name;

    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly IOptions<EmailOptions> _options;
    private readonly ILogger<ResendEmailProvider> _logger;

    public ResendEmailProvider(
        HttpClient httpClient,
        IConfiguration configuration,
        IOptions<EmailOptions> options,
        ILogger<ResendEmailProvider> logger)
    {
        _httpClient = httpClient;
        _configuration = configuration;
        _options = options;
        _logger = logger;
    }

    public async Task SendEmailAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        var options = _options.Value;
        var apiKey = !string.IsNullOrWhiteSpace(options.ApiKey)
            ? options.ApiKey
            : _configuration[options.ApiKeyConfigurationName];

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            var msg = $"Resend API key is not configured. Set the '{options.ApiKeyConfigurationName}' environment variable or user secret.";
            _logger.LogError("{ErrorMessage}", msg);
            throw new InvalidOperationException(msg);
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

        var formattedFrom = !string.IsNullOrWhiteSpace(fromName)
            ? $"{fromName} <{fromAddress}>"
            : fromAddress;

        var baseUrl = !string.IsNullOrWhiteSpace(options.Resend.BaseUrl)
            ? options.Resend.BaseUrl
            : "https://api.resend.com";

        using var request = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl.TrimEnd('/')}/emails");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        request.Content = JsonContent.Create(new ResendEmailRequest(
            formattedFrom,
            [message.To],
            message.Subject,
            message.HtmlBody));

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        if (response.IsSuccessStatusCode)
        {
            _logger.LogInformation("Successfully sent email via Resend to {Recipient} with subject '{Subject}'.", message.To, message.Subject);
            return;
        }

        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
        _logger.LogError("Resend email delivery to {Recipient} failed with status {StatusCode}: {ResponseBody}",
            message.To, response.StatusCode, responseBody);

        throw new InvalidOperationException(
            $"Resend email delivery failed with status code {(int)response.StatusCode}: {responseBody}");
    }

    private sealed record ResendEmailRequest(
        [property: JsonPropertyName("from")] string From,
        [property: JsonPropertyName("to")] string[] To,
        [property: JsonPropertyName("subject")] string Subject,
        [property: JsonPropertyName("html")] string Html);
}
