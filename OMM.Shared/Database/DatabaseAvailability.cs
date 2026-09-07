using System.Data.Common;
using System.Net.Sockets;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;
using OMM.Shared.Infrastructure;

namespace OMM.Shared.Database;

public sealed class DatabaseAvailability
{
    private const int UnavailableState = 0;
    private const int AvailableState = 1;

    private int _state = UnavailableState;

    public bool IsAvailable => Volatile.Read(ref _state) == AvailableState;

    public bool MarkAvailable()
    {
        return Interlocked.CompareExchange(ref _state, AvailableState, UnavailableState) == UnavailableState;
    }

    public bool MarkUnavailable()
    {
        return Interlocked.CompareExchange(ref _state, UnavailableState, AvailableState) == AvailableState;
    }
}

public static class DatabaseAvailabilityDefaults
{
    public const string UnavailableHtml = """
        <!doctype html>
        <html lang="en">
        <head>
            <meta charset="utf-8">
            <meta name="viewport" content="width=device-width, initial-scale=1">
            <title>OMM temporarily unavailable</title>
            <style>
                body { font-family: Arial, sans-serif; background: #f4f6f8; color: #1f2937; display: grid; place-items: center; min-height: 100vh; margin: 0; }
                main { background: white; max-width: 560px; margin: 24px; padding: 32px; border-radius: 12px; box-shadow: 0 8px 30px rgba(0,0,0,.08); text-align: center; }
                h1 { margin-top: 0; }
                a { display: inline-block; margin-top: 16px; padding: 10px 18px; background: #2563eb; color: white; text-decoration: none; border-radius: 6px; }
            </style>
        </head>
        <body>
            <main>
                <h1>We’ll be back shortly</h1>
                <p>The database is temporarily unavailable. Please try again later.</p>
                <a href="/">Try again</a>
            </main>
        </body>
        </html>
        """;

    public static async Task WriteUnavailableResponseAsync(HttpContext context)
    {
        context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
        var acceptsHtml = context.Request.Headers.Accept
            .ToString()
            .Contains("text/html", StringComparison.OrdinalIgnoreCase);

        if (acceptsHtml)
        {
            context.Response.ContentType = "text/html; charset=utf-8";
            await context.Response.WriteAsync(UnavailableHtml);
        }
        else
        {
            context.Response.ContentType = "application/json; charset=utf-8";
            await context.Response.WriteAsync("""{"status":"unavailable","message":"The database is temporarily unavailable. Please try again later."}""");
        }
    }
}

public sealed class DatabaseAvailabilityMonitorOptions
{
    public TimeSpan Interval { get; set; } = TimeSpan.FromSeconds(60);
    public int ProbeTimeoutSeconds { get; set; } = 10;
}

public sealed class DatabaseAvailabilityMonitor : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly DatabaseAvailability _availability;
    private readonly ILogger<DatabaseAvailabilityMonitor> _logger;
    private readonly IOptions<DatabaseAvailabilityMonitorOptions> _options;

    public DatabaseAvailabilityMonitor(
        IServiceProvider serviceProvider,
        DatabaseAvailability availability,
        ILogger<DatabaseAvailabilityMonitor> logger,
        IOptions<DatabaseAvailabilityMonitorOptions>? options = null)
    {
        _serviceProvider = serviceProvider;
        _availability = availability;
        _logger = logger;
        _options = options ?? Options.Create(new DatabaseAvailabilityMonitorOptions());
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // If the application started in degraded mode, perform an early probe after 5s
        // to rapidly recover if the database was simply waking up (e.g. Neon cold start).
        if (!_availability.IsAvailable)
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
                await ProbeAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Early database probe completed; scheduled monitoring will continue.");
            }
        }

        using var timer = new PeriodicTimer(_options.Value.Interval);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (!await timer.WaitForNextTickAsync(stoppingToken))
                {
                    break;
                }

                await ProbeAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error in database availability monitor.");
            }
        }
    }

    public async Task ProbeAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var dataSource = _serviceProvider.GetService<NpgsqlDataSource>();
            if (dataSource is null)
            {
                return;
            }

            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(_options.Value.ProbeTimeoutSeconds));

            await using var connection = await dataSource.OpenConnectionAsync(timeoutCts.Token);
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT 1";
            command.CommandTimeout = _options.Value.ProbeTimeoutSeconds;
            await command.ExecuteScalarAsync(timeoutCts.Token);

            if (_availability.MarkAvailable())
            {
                _logger.LogInformation("Database connection restored. Database marked available.");
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex) when (IsDatabaseException(ex))
        {
            if (_availability.MarkUnavailable())
            {
                _logger.LogError(ex, "Database connection failed during background probe. Database marked unavailable.");
            }
            else
            {
                _logger.LogDebug("Database probe failed; database remains unavailable.");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during database availability probe.");
        }
    }

    private static bool IsDatabaseException(Exception exception)
    {
        for (var current = exception; current is not null; current = current.InnerException)
        {
            if (current is DbException or NpgsqlException or TimeoutException or SocketException or IOException or ArgumentException)
            {
                return true;
            }
        }

        return false;
    }
}

public static class DatabaseAvailabilityExtensions
{
    public static IServiceCollection AddDatabaseAvailability(this IServiceCollection services)
    {
        services.AddSingleton<DatabaseAvailability>();
        return services;
    }

    public static IServiceCollection AddSafeNpgsqlDataSource(
        this IServiceCollection services,
        string? connectionString)
    {
        services.AddSingleton<NpgsqlDataSource>(sp =>
        {
            var availability = sp.GetRequiredService<DatabaseAvailability>();
            var logger = sp.GetRequiredService<ILoggerFactory>().CreateLogger("NpgsqlDataSource");

            if (string.IsNullOrWhiteSpace(connectionString))
            {
                availability.MarkUnavailable();
                logger.LogError("NpgsqlDataSource creation failed: connection string is not configured.");
                throw new NpgsqlException("Database connection string is not configured.");
            }

            try
            {
                return NpgsqlDataSource.Create(connectionString);
            }
            catch (Exception ex) when (ex is ArgumentException or FormatException)
            {
                availability.MarkUnavailable();
                logger.LogError(ex, "NpgsqlDataSource creation failed due to invalid connection string format.");
                throw new NpgsqlException("Database connection string format is invalid.", ex);
            }
        });

        return services;
    }

    public static IServiceCollection AddDatabaseAvailabilityMonitor(
        this IServiceCollection services,
        Action<DatabaseAvailabilityMonitorOptions>? configure = null)
    {
        if (configure is not null)
        {
            services.Configure(configure);
        }

        services.AddHostedService<DatabaseAvailabilityMonitor>();
        return services;
    }

    public static async Task<bool> TryInitializeDatabaseAsync<TContext>(
        this WebApplication app,
        Func<IServiceProvider, Task>? seedAsync = null)
        where TContext : DbContext
    {
        await using var scope = app.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<TContext>();
        var databaseAvailability = scope.ServiceProvider.GetRequiredService<DatabaseAvailability>();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>()
            .CreateLogger("DatabaseStartup");

        const int maxAttempts = 3;
        var retryDelay = TimeSpan.FromSeconds(2);

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                dbContext.Database.SetCommandTimeout(15);
                await dbContext.Database.MigrateAsync();

                if (seedAsync is not null)
                {
                    await seedAsync(scope.ServiceProvider);
                }

                databaseAvailability.MarkAvailable();
                logger.LogInformation("Database connection and initialization succeeded.");
                return true;
            }
            catch (Exception exception) when (attempt < maxAttempts && IsTransientStartupException(exception))
            {
                logger.LogWarning(
                    exception,
                    "Database initialization attempt {Attempt}/{MaxAttempts} failed with transient error. Retrying in {RetryDelay}s (e.g. cold start / compute resume)...",
                    attempt,
                    maxAttempts,
                    retryDelay.TotalSeconds);

                await Task.Delay(retryDelay);
            }
            catch (Exception exception) when (exception is DbException or NpgsqlException or TimeoutException or SocketException or IOException or ArgumentException or InvalidOperationException)
            {
                databaseAvailability.MarkUnavailable();
                await DatabaseFailureNotificationExtensions.NotifyStartupFailureAsync(
                    scope.ServiceProvider,
                    app,
                    exception);
                logger.LogError(
                    exception,
                    "The database is unavailable or configuration is invalid after {Attempts} attempt(s). The application will start in degraded mode.",
                    attempt);
                return false;
            }
        }

        return false;
    }

    public static async Task<bool> TryCheckDatabaseAsync<TContext>(this WebApplication app)
        where TContext : DbContext
    {
        await using var scope = app.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<TContext>();
        var databaseAvailability = scope.ServiceProvider.GetRequiredService<DatabaseAvailability>();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>()
            .CreateLogger("DatabaseStartup");

        const int maxAttempts = 3;
        var retryDelay = TimeSpan.FromSeconds(2);

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                var canConnect = await dbContext.Database.CanConnectAsync();

                if (!canConnect)
                {
                    if (attempt < maxAttempts)
                    {
                        logger.LogWarning(
                            "Database connection check returned false on attempt {Attempt}/{MaxAttempts}. Retrying in {RetryDelay}s...",
                            attempt,
                            maxAttempts,
                            retryDelay.TotalSeconds);
                        await Task.Delay(retryDelay);
                        continue;
                    }

                    databaseAvailability.MarkUnavailable();
                    await DatabaseFailureNotificationExtensions.NotifyStartupFailureAsync(
                        scope.ServiceProvider,
                        app,
                        new InvalidOperationException("The database connection check returned false."));
                    logger.LogError("The database connection check returned false.");
                    return false;
                }

                databaseAvailability.MarkAvailable();
                logger.LogInformation("Database connection check succeeded.");
                return true;
            }
            catch (Exception exception) when (attempt < maxAttempts && IsTransientStartupException(exception))
            {
                logger.LogWarning(
                    exception,
                    "Database connection check attempt {Attempt}/{MaxAttempts} failed with transient error. Retrying in {RetryDelay}s...",
                    attempt,
                    maxAttempts,
                    retryDelay.TotalSeconds);
                await Task.Delay(retryDelay);
            }
            catch (Exception exception) when (exception is DbException or NpgsqlException or TimeoutException or SocketException or IOException or ArgumentException or InvalidOperationException)
            {
                databaseAvailability.MarkUnavailable();
                await DatabaseFailureNotificationExtensions.NotifyStartupFailureAsync(
                    scope.ServiceProvider,
                    app,
                    exception);
                logger.LogError(exception, "The database connection check failed. The application will start in degraded mode.");
                return false;
            }
        }

        return false;
    }

    private static bool IsTransientStartupException(Exception exception)
    {
        for (var current = exception; current is not null; current = current.InnerException)
        {
            if (current is SocketException or TimeoutException or IOException or NpgsqlException)
            {
                return true;
            }
        }

        return false;
    }

    public static IApplicationBuilder UseDatabaseAvailabilityPage(this IApplicationBuilder app)
    {
        return app.Use(async (context, next) =>
        {
            var databaseAvailability = context.RequestServices
                .GetRequiredService<DatabaseAvailability>();
            var acceptsHtml = context.Request.Headers.Accept
                .ToString()
                .Contains("text/html", StringComparison.OrdinalIgnoreCase);
            var isHealthCheck = context.Request.Path.StartsWithSegments("/health/db");

            if (!databaseAvailability.IsAvailable && acceptsHtml && !isHealthCheck)
            {
                await DatabaseAvailabilityDefaults.WriteUnavailableResponseAsync(context);
                return;
            }

            await next();
        });
    }

    public static IEndpointRouteBuilder MapDatabaseHealthCheck(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/health/db", (DatabaseAvailability databaseAvailability) =>
        {
            if (databaseAvailability.IsAvailable)
            {
                return (IResult)Results.Ok(new { status = "healthy" });
            }

            return Results.Json(
                new { status = "unavailable" },
                statusCode: StatusCodes.Status503ServiceUnavailable);
        });

        return endpoints;
    }
}
