using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Npgsql;
using OMM.Shared.Infrastructure;

namespace OMM.Shared.Database;

public sealed class DatabaseAvailability
{
    public bool IsAvailable { get; private set; }

    public void MarkAvailable() => IsAvailable = true;

    public void MarkUnavailable() => IsAvailable = false;
}

public static class DatabaseAvailabilityExtensions
{
    public static IServiceCollection AddDatabaseAvailability(this IServiceCollection services)
    {
        services.AddSingleton<DatabaseAvailability>();
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
        catch (NpgsqlException exception)
        {
            databaseAvailability.MarkUnavailable();
            await DatabaseFailureNotificationExtensions.NotifyStartupFailureAsync(
                scope.ServiceProvider,
                app,
                exception);
            logger.LogError(
                exception,
                "The database is unavailable. The application will start in degraded mode.");
            return false;
        }
        catch (TimeoutException exception)
        {
            databaseAvailability.MarkUnavailable();
            await DatabaseFailureNotificationExtensions.NotifyStartupFailureAsync(
                scope.ServiceProvider,
                app,
                exception);
            logger.LogError(
                exception,
                "The database connection timed out. The application will start in degraded mode.");
            return false;
        }
    }

    public static async Task<bool> TryCheckDatabaseAsync<TContext>(this WebApplication app)
        where TContext : DbContext
    {
        await using var scope = app.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<TContext>();
        var databaseAvailability = scope.ServiceProvider.GetRequiredService<DatabaseAvailability>();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>()
            .CreateLogger("DatabaseStartup");

        try
        {
            var canConnect = await dbContext.Database.CanConnectAsync();

            if (!canConnect)
            {
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
        catch (NpgsqlException exception)
        {
            databaseAvailability.MarkUnavailable();
            await DatabaseFailureNotificationExtensions.NotifyStartupFailureAsync(
                scope.ServiceProvider,
                app,
                exception);
            logger.LogError(exception, "The database connection check failed.");
            return false;
        }
        catch (TimeoutException exception)
        {
            databaseAvailability.MarkUnavailable();
            await DatabaseFailureNotificationExtensions.NotifyStartupFailureAsync(
                scope.ServiceProvider,
                app,
                exception);
            logger.LogError(exception, "The database connection check timed out.");
            return false;
        }
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
                context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
                context.Response.ContentType = "text/html; charset=utf-8";

                await context.Response.WriteAsync("""
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
                    """);
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
