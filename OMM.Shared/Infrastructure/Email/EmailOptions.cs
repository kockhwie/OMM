namespace OMM.Shared.Infrastructure.Email;

public sealed class EmailOptions
{
    /// <summary>
    /// The active email provider. Supported values: "Resend", "Smtp", "NoOp".
    /// Defaults to "Resend".
    /// </summary>
    public string Provider { get; set; } = "Resend";

    /// <summary>
    /// Default sender email address (must be configured in appsettings.json or environment).
    /// </summary>
    public string FromAddress { get; set; } = string.Empty;

    /// <summary>
    /// Default sender display name (e.g. "OMM Admin" or "OMM").
    /// </summary>
    public string FromName { get; set; } = string.Empty;

    /// <summary>
    /// Configuration/Environment key name to read the API key from (e.g. for Resend).
    /// Defaults to "Resend_EmailOnboardingApi".
    /// </summary>
    public string ApiKeyConfigurationName { get; set; } = "Resend_EmailOnboardingApi";

    /// <summary>
    /// Direct API key fallback if specified in configuration instead of environment variable.
    /// </summary>
    public string? ApiKey { get; set; }

    /// <summary>
    /// Resend provider settings.
    /// </summary>
    public ResendOptions Resend { get; set; } = new();

    /// <summary>
    /// SMTP provider settings.
    /// </summary>
    public SmtpOptions Smtp { get; set; } = new();
}

public sealed class ResendOptions
{
    public string BaseUrl { get; set; } = "https://api.resend.com";
}

public sealed class SmtpOptions
{
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; } = 587;
    public string? Username { get; set; }
    public string? Password { get; set; }
    public bool EnableSsl { get; set; } = true;
}
