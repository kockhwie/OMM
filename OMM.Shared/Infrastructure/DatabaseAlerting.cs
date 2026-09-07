using System.Data.Common;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Text.Encodings.Web;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;
using OMM.Shared.Database;

namespace OMM.Shared.Infrastructure;

public sealed class DatabaseAlertOptions
{
    public bool Enabled { get; set; } = true;
    public string[] Recipients { get; set; } = ["kockhwie@msn.com"];
    public string FromAddress { get; set; } = "jason.goh@codingdinos.com";
    public string FromName { get; set; } = "OMM Database Alerts";
    public string ResendBaseUrl { get; set; } = "https://api.resend.com";
    public string ResendApiKeyConfigurationName { get; set; } = "Resend_EmailOnboardingApi";
    public string LogDirectory { get; set; } = "App_Data/Logs";
    public long MaxLogBytes { get; set; } = 10 * 1024 * 1024;
}
public sealed record DatabaseFailureAlert(
    DateTimeOffset OccurredAt,
    string Application,
    string RequestPath,
    string ExceptionType,
    string Message);

public interface IDatabaseAlertEmailSender
{
    Task SendAsync(DatabaseFailureAlert alert, CancellationToken cancellationToken = default);
}

public sealed class DatabaseAlertNotifier
{
    private readonly RollingDatabaseLogWriter _logWriter;
    private readonly IDatabaseAlertEmailSender _emailSender;
    private readonly ILogger<DatabaseAlertNotifier> _logger;

    public DatabaseAlertNotifier(
        RollingDatabaseLogWriter logWriter,
        IDatabaseAlertEmailSender emailSender,
        ILogger<DatabaseAlertNotifier> logger)
    {
        _logWriter = logWriter;
        _emailSender = emailSender;
        _logger = logger;
    }

    public async Task NotifyAsync(DatabaseFailureAlert alert)
    {
        try
        {
            await _logWriter.WriteAsync(alert);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Could not write the database failure fallback log.");
        }

        try
        {
            await _emailSender.SendAsync(alert);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Could not send the database failure alert email.");
        }
    }
}

internal sealed class ResendDatabaseAlertEmailSender : IDatabaseAlertEmailSender
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly IOptions<DatabaseAlertOptions> _options;
    private readonly ILogger<ResendDatabaseAlertEmailSender> _logger;

    public ResendDatabaseAlertEmailSender(
        HttpClient httpClient,
        IConfiguration configuration,
        IOptions<DatabaseAlertOptions> options,
        ILogger<ResendDatabaseAlertEmailSender> logger)
    {
        _httpClient = httpClient;
        _configuration = configuration;
        _options = options;
        _logger = logger;
    }

    public async Task SendAsync(DatabaseFailureAlert alert, CancellationToken cancellationToken = default)
    {
        var options = _options.Value;
        var recipients = options.Recipients
            .SelectMany(value => value.Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (!options.Enabled || recipients.Length == 0)
        {
            return;
        }

        var apiKey = _configuration[options.ResendApiKeyConfigurationName];
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            _logger.LogWarning(
                "Database alert email was not sent because {ConfigurationName} is not configured.",
                options.ResendApiKeyConfigurationName);
            return;
        }

        var safeMessage = HtmlEncoder.Default.Encode(alert.Message);
        var safePath = HtmlEncoder.Default.Encode(alert.RequestPath);
        var html = $"<h2>OMM database failure</h2><p><strong>Application:</strong> {alert.Application}</p><p><strong>Time:</strong> {alert.OccurredAt:O}</p><p><strong>Path:</strong> {safePath}</p><p><strong>Exception:</strong> {alert.ExceptionType}</p><p><strong>Message:</strong> {safeMessage}</p>";

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"{options.ResendBaseUrl.TrimEnd('/')}/emails");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        request.Content = JsonContent.Create(new ResendAlertRequest(
            $"{options.FromName} <{options.FromAddress}>",
            recipients,
            "OMM database failure detected",
            html));

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError(
                "Database alert email delivery failed with status {StatusCode}.",
                response.StatusCode);
        }
    }

    private sealed record ResendAlertRequest(
        [property: JsonPropertyName("from")] string From,
        [property: JsonPropertyName("to")] string[] To,
        [property: JsonPropertyName("subject")] string Subject,
        [property: JsonPropertyName("html")] string Html);
}

public sealed class RollingDatabaseLogWriter
{
    private const string ActiveLogFileName = "database-current.log";
    private readonly IHostEnvironment _environment;
    private readonly IOptions<DatabaseAlertOptions> _options;
    private readonly SemaphoreSlim _lock = new(1, 1);

    public RollingDatabaseLogWriter(
        IHostEnvironment environment,
        IOptions<DatabaseAlertOptions> options)
    {
        _environment = environment;
        _options = options;
    }

    public string LogDirectoryPath => GetLogDirectory();

    public void EnsureLogDirectory()
    {
        Directory.CreateDirectory(GetLogDirectory());
    }

    public async Task WriteAsync(DatabaseFailureAlert alert, CancellationToken cancellationToken = default)
    {
        var options = _options.Value;
        var directory = Path.IsPathRooted(options.LogDirectory)
            ? options.LogDirectory
            : Path.Combine(_environment.ContentRootPath, options.LogDirectory);
        var logPath = Path.Combine(directory, ActiveLogFileName);
        var line = $"{alert.OccurredAt:O}\t{alert.Application}\t{alert.RequestPath}\t{alert.ExceptionType}\t{alert.Message}{Environment.NewLine}";

        await _lock.WaitAsync(cancellationToken);
        try
        {
            Directory.CreateDirectory(directory);

            if (File.Exists(logPath))
            {
                var currentLength = new FileInfo(logPath).Length;
                if (currentLength + System.Text.Encoding.UTF8.GetByteCount(line) > options.MaxLogBytes)
                {
                    var archivedPath = Path.Combine(
                        directory,
                        $"database-{DateTime.UtcNow:yyyy-MM-dd-HHmmss-fff}.log");
                    File.Move(logPath, archivedPath, overwrite: true);
                }
            }

            await File.AppendAllTextAsync(logPath, line, cancellationToken);
        }
        finally
        {
            _lock.Release();
        }
    }

    private string GetLogDirectory()
    {
        var configuredDirectory = _options.Value.LogDirectory;
        return Path.IsPathRooted(configuredDirectory)
            ? configuredDirectory
            : Path.Combine(_environment.ContentRootPath, configuredDirectory);
    }

}

internal sealed class DatabaseLogDirectoryInitializer : IHostedService
{
    private readonly RollingDatabaseLogWriter _logWriter;
    private readonly ILogger<DatabaseLogDirectoryInitializer> _logger;

    public DatabaseLogDirectoryInitializer(
        RollingDatabaseLogWriter logWriter,
        ILogger<DatabaseLogDirectoryInitializer> logger)
    {
        _logWriter = logWriter;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            _logWriter.EnsureLogDirectory();
            _logger.LogInformation(
                "Database fallback log directory: {LogDirectory}",
                _logWriter.LogDirectoryPath);
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "Could not create the database fallback log directory: {LogDirectory}",
                _logWriter.LogDirectoryPath);
        }

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}

public static class DatabaseAlertingServiceCollectionExtensions
{
    public static IServiceCollection AddDatabaseAlerting(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddOptions<DatabaseAlertOptions>()
            .Bind(configuration.GetSection("DatabaseAlerts"));
        services.AddSingleton<RollingDatabaseLogWriter>();
        services.AddHostedService<DatabaseLogDirectoryInitializer>();
        services.AddTransient<DatabaseAlertNotifier>();
        services.AddHttpClient<IDatabaseAlertEmailSender, ResendDatabaseAlertEmailSender>();
        return services;
    }
}

public static class DatabaseFailureNotificationExtensions
{
    public static IApplicationBuilder UseDatabaseFailureNotifications(
        this IApplicationBuilder app,
        string applicationName)
    {
        return app.Use(async (context, next) =>
        {
            try
            {
                await next();
            }
            catch (Exception exception) when (IsDatabaseFailure(exception))
            {
                var databaseAvailability = context.RequestServices.GetService<DatabaseAvailability>();
                var transitioned = databaseAvailability?.MarkUnavailable() ?? true;

                if (transitioned)
                {
                    var alert = new DatabaseFailureAlert(
                        DateTimeOffset.UtcNow,
                        applicationName,
                        context.Request.Path.ToString(),
                        exception.GetType().FullName ?? exception.GetType().Name,
                        exception.Message);

                    try
                    {
                        var notifier = context.RequestServices.GetService<DatabaseAlertNotifier>();
                        if (notifier is not null)
                        {
                            await notifier.NotifyAsync(alert);
                        }
                    }
                    catch (Exception notifyException)
                    {
                        var logger = context.RequestServices.GetService<ILoggerFactory>()?
                            .CreateLogger("DatabaseFailureNotification");
                        logger?.LogError(notifyException, "Could not send database alert notification.");
                    }
                }

                if (!context.Response.HasStarted)
                {
                    await DatabaseAvailabilityDefaults.WriteUnavailableResponseAsync(context);
                    return;
                }

                throw;
            }
        });
    }

    private static bool IsDatabaseFailure(Exception exception)
    {
        for (var current = exception; current is not null; current = current.InnerException)
        {
            if (current is DbException or NpgsqlException or DbUpdateException or TimeoutException or SocketException)
            {
                return true;
            }
        }

        return false;
    }

    internal static Task NotifyStartupFailureAsync(
        IServiceProvider services,
        WebApplication app,
        Exception exception)
    {
        var alert = new DatabaseFailureAlert(
            DateTimeOffset.UtcNow,
            app.Environment.ApplicationName,
            "/startup",
            exception.GetType().FullName ?? exception.GetType().Name,
            exception.Message);

        return services.GetRequiredService<DatabaseAlertNotifier>().NotifyAsync(alert);
    }
}
