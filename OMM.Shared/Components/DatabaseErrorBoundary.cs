using System.Data.Common;
using System.Net.Sockets;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Npgsql;
using OMM.Shared.Database;
using OMM.Shared.Infrastructure;

namespace OMM.Shared.Components;

public class DatabaseErrorBoundary : ErrorBoundary
{
    [Inject] public DatabaseAvailability? Availability { get; set; }
    [Inject] public DatabaseAlertNotifier? Notifier { get; set; }
    [Inject] public ILogger<DatabaseErrorBoundary>? Logger { get; set; }
    [Inject] public IHostEnvironment? HostEnvironment { get; set; }
    [Inject] public NavigationManager? NavigationManager { get; set; }

    [Parameter] public string? ApplicationName { get; set; }
    [Parameter] public RenderFragment<Exception>? DatabaseErrorContent { get; set; }

    public bool IsDatabaseError { get; private set; }

    public Task HandleTestExceptionAsync(Exception exception)
    {
        return OnErrorAsync(exception);
    }

    protected override async Task OnErrorAsync(Exception exception)
    {
        if (IsDatabaseException(exception))
        {
            IsDatabaseError = true;
            var transitioned = Availability?.MarkUnavailable() ?? true;
            if (transitioned)
            {
                var appName = ApplicationName ?? HostEnvironment?.ApplicationName ?? "OMM";
                var path = NavigationManager?.Uri ?? "/blazor-circuit";
                var alert = new DatabaseFailureAlert(
                    DateTimeOffset.UtcNow,
                    appName,
                    path,
                    exception.GetType().FullName ?? exception.GetType().Name,
                    exception.Message);

                try
                {
                    if (Notifier is not null)
                    {
                        await Notifier.NotifyAsync(alert);
                    }
                }
                catch (Exception notifyEx)
                {
                    Logger?.LogError(notifyEx, "Could not send database alert notification from Blazor error boundary.");
                }
            }

            Logger?.LogError(exception, "Database error captured by Blazor error boundary.");
            return;
        }

        IsDatabaseError = false;
        try
        {
            await base.OnErrorAsync(exception);
        }
        catch (NullReferenceException)
        {
            // Base ErrorBoundary accesses internal logger which may be null when tested outside a full Blazor renderer
            Logger?.LogError(exception, "Non-database error captured by Blazor error boundary.");
        }
    }

    public new void Recover()
    {
        IsDatabaseError = false;
        base.Recover();
    }

    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        if (CurrentException is null)
        {
            builder.AddContent(0, ChildContent);
            return;
        }

        if (IsDatabaseError)
        {
            if (DatabaseErrorContent is not null)
            {
                builder.AddContent(1, DatabaseErrorContent(CurrentException));
                return;
            }

            RenderDefaultDatabaseError(builder);
            return;
        }

        if (ErrorContent is not null)
        {
            builder.AddContent(2, ErrorContent(CurrentException));
            return;
        }

        RenderDefaultError(builder);
    }

    private static void RenderDefaultDatabaseError(RenderTreeBuilder builder)
    {
        builder.OpenElement(0, "div");
        builder.AddAttribute(1, "class", "alert alert-warning d-flex align-items-center my-4 p-4 shadow-sm rounded-3");
        builder.AddAttribute(2, "role", "alert");

        builder.OpenElement(3, "i");
        builder.AddAttribute(4, "class", "ti ti-database-off fs-2 me-3 text-warning");
        builder.CloseElement(); // i

        builder.OpenElement(5, "div");
        builder.AddAttribute(6, "class", "flex-grow-1");

        builder.OpenElement(7, "h5");
        builder.AddAttribute(8, "class", "alert-heading mb-1");
        builder.AddContent(9, "Database temporarily unavailable");
        builder.CloseElement(); // h5

        builder.OpenElement(10, "p");
        builder.AddAttribute(11, "class", "mb-2");
        builder.AddContent(12, "We are having trouble connecting to the database right now. Your data is safe, but this operation could not be completed.");
        builder.CloseElement(); // p

        builder.OpenElement(13, "button");
        builder.AddAttribute(14, "type", "button");
        builder.AddAttribute(15, "class", "btn btn-sm btn-outline-warning");
        builder.AddAttribute(16, "onclick", "window.location.reload()");

        builder.OpenElement(17, "i");
        builder.AddAttribute(18, "class", "ti ti-refresh me-1");
        builder.CloseElement(); // i

        builder.AddContent(19, " Refresh page");
        builder.CloseElement(); // button

        builder.CloseElement(); // div flex-grow-1
        builder.CloseElement(); // div alert
    }

    private static void RenderDefaultError(RenderTreeBuilder builder)
    {
        builder.OpenElement(0, "div");
        builder.AddAttribute(1, "class", "alert alert-danger my-4 p-3 shadow-sm rounded-3");
        builder.AddAttribute(2, "role", "alert");
        builder.AddContent(3, "An unhandled error has occurred. Please reload the page.");
        builder.CloseElement();
    }

    public static bool IsDatabaseException(Exception exception)
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
}
