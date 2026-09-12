using System.Net.Sockets;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Npgsql;
using OMM.Shared.Components;
using OMM.Shared.Database;
using OMM.Shared.Infrastructure;
using Xunit;

namespace OMM.Shared.Tests.Database;

#pragma warning disable BL0005
public class DatabaseErrorBoundaryTests
{
    private sealed class TestHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Testing";
        public string ApplicationName { get; set; } = "TestApp";
        public string ContentRootPath { get; set; } = Path.GetTempPath();
        public IFileProvider ContentRootFileProvider { get; set; } = null!;
    }

    private sealed class TestAlertEmailSender : IDatabaseAlertEmailSender
    {
        public List<DatabaseFailureAlert> SentAlerts { get; } = [];
        public bool ShouldThrow { get; set; }

        public Task SendAsync(DatabaseFailureAlert alert, CancellationToken cancellationToken = default)
        {
            if (ShouldThrow)
            {
                throw new InvalidOperationException("Email provider failure");
            }

            lock (SentAlerts)
            {
                SentAlerts.Add(alert);
            }

            return Task.CompletedTask;
        }
    }

    private static (DatabaseErrorBoundary Boundary, DatabaseAvailability Availability, TestAlertEmailSender EmailSender)
        CreateTestBoundary(Action<TestAlertEmailSender>? configureSender = null)
    {
        var services = new ServiceCollection();
        var availability = new DatabaseAvailability();
        availability.MarkAvailable();

        var emailSender = new TestAlertEmailSender();
        configureSender?.Invoke(emailSender);

        var tempDir = Path.Combine(Path.GetTempPath(), "omm-boundary-tests-" + Guid.NewGuid().ToString("N"));
        var options = Options.Create(new DatabaseAlertOptions
        {
            Enabled = true,
            Recipients = ["alerts@example.com"],
            LogDirectory = tempDir
        });

        services.AddSingleton(availability);
        services.AddSingleton<IDatabaseAlertEmailSender>(emailSender);
        services.AddSingleton<IHostEnvironment>(new TestHostEnvironment());
        services.AddSingleton(options);
        services.AddSingleton<RollingDatabaseLogWriter>();
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        services.AddSingleton<ILoggerFactory, NullLoggerFactory>();
        services.AddTransient<DatabaseAlertNotifier>();

        var sp = services.BuildServiceProvider();

        var boundary = new DatabaseErrorBoundary
        {
            Availability = availability,
            Notifier = sp.GetRequiredService<DatabaseAlertNotifier>(),
            Logger = NullLogger<DatabaseErrorBoundary>.Instance,
            HostEnvironment = sp.GetRequiredService<IHostEnvironment>(),
            ApplicationName = "TestBlazorApp"
        };

        return (boundary, availability, emailSender);
    }

    [Fact]
    public async Task DatabaseException_MarksUnavailable_AndNotifies()
    {
        var (boundary, availability, emailSender) = CreateTestBoundary();

        var ex = new NpgsqlException("Server disconnected unexpectedly");
        await boundary.HandleTestExceptionAsync(ex);

        Assert.False(availability.IsAvailable);
        Assert.True(boundary.IsDatabaseError);
        Assert.Single(emailSender.SentAlerts);
        Assert.Equal("TestBlazorApp", emailSender.SentAlerts[0].Application);
        Assert.Equal("Server disconnected unexpectedly", emailSender.SentAlerts[0].Message);
    }

    [Fact]
    public async Task SubsequentDatabaseExceptions_DoNotSendDuplicateAlerts()
    {
        var (boundary, availability, emailSender) = CreateTestBoundary();

        var ex1 = new NpgsqlException("Error 1");
        await boundary.HandleTestExceptionAsync(ex1);

        Assert.False(availability.IsAvailable);
        Assert.Single(emailSender.SentAlerts);

        var ex2 = new NpgsqlException("Error 2");
        await boundary.HandleTestExceptionAsync(ex2);

        // Deduplication: Still only 1 alert
        Assert.Single(emailSender.SentAlerts);
    }

    [Fact]
    public async Task NonDatabaseException_DoesNotMarkUnavailable_AndDoesNotNotify()
    {
        var (boundary, availability, emailSender) = CreateTestBoundary();

        var ex = new InvalidOperationException("Component logic bug");
        await boundary.HandleTestExceptionAsync(ex);

        Assert.True(availability.IsAvailable);
        Assert.False(boundary.IsDatabaseError);
        Assert.Empty(emailSender.SentAlerts);
    }

    [Fact]
    public async Task NestedDatabaseException_IsRecognized()
    {
        var (boundary, availability, emailSender) = CreateTestBoundary();

        var nested = new InvalidOperationException("EF wrapper", new SocketException());
        await boundary.HandleTestExceptionAsync(nested);

        Assert.False(availability.IsAvailable);
        Assert.True(boundary.IsDatabaseError);
        Assert.Single(emailSender.SentAlerts);
    }

    [Fact]
    public async Task NotifierFailure_DoesNotThrow()
    {
        var (boundary, availability, _) = CreateTestBoundary(sender => sender.ShouldThrow = true);

        var ex = new NpgsqlException("Database failed");
        // Should not throw even though email notifier fails
        await boundary.HandleTestExceptionAsync(ex);

        Assert.False(availability.IsAvailable);
        Assert.True(boundary.IsDatabaseError);
    }
}
