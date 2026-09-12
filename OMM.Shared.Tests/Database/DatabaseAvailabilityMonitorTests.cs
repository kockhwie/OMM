using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;
using OMM.Shared.Database;
using Xunit;

namespace OMM.Shared.Tests.Database;

public class DatabaseAvailabilityMonitorTests
{
    private sealed class TrackedAsyncDisposable : IAsyncDisposable
    {
        public bool IsDisposed { get; private set; }
        public int DisposeCallCount { get; private set; }

        public ValueTask DisposeAsync()
        {
            IsDisposed = true;
            DisposeCallCount++;
            return ValueTask.CompletedTask;
        }
    }

    private sealed class TestLogger<T> : ILogger<T>
    {
        public List<(LogLevel Level, string Message, Exception? Exception)> Logs { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            Logs.Add((logLevel, formatter(state, exception), exception));
        }
    }

    [Fact]
    public async Task SuccessfulProbe_CallsMarkAvailable()
    {
        var services = new ServiceCollection().BuildServiceProvider();
        var availability = new DatabaseAvailability();
        var logger = new TestLogger<DatabaseAvailabilityMonitor>();

        var monitor = new DatabaseAvailabilityMonitor(
            services,
            availability,
            logger,
            Options.Create(new DatabaseAvailabilityMonitorOptions()),
            connectionFactory: _ => Task.FromResult<IAsyncDisposable>(new TrackedAsyncDisposable()));

        await monitor.ProbeAsync();

        Assert.True(availability.IsAvailable);
        Assert.Contains(logger.Logs, l => l.Level == LogLevel.Information && l.Message.Contains("restored"));
    }

    [Fact]
    public async Task FailedProbe_CallsMarkUnavailable()
    {
        var services = new ServiceCollection().BuildServiceProvider();
        var availability = new DatabaseAvailability();
        availability.MarkAvailable();
        var logger = new TestLogger<DatabaseAvailabilityMonitor>();

        var monitor = new DatabaseAvailabilityMonitor(
            services,
            availability,
            logger,
            Options.Create(new DatabaseAvailabilityMonitorOptions()),
            connectionFactory: _ => throw new NpgsqlException("Connection refused"));

        await monitor.ProbeAsync();

        Assert.False(availability.IsAvailable);
        Assert.Contains(logger.Logs, l => l.Level == LogLevel.Error && l.Message.Contains("Database marked unavailable"));
    }

    [Fact]
    public async Task RepeatedProbeFailures_DoNotNotifyRepeatedly_DuplicateSuppression()
    {
        var services = new ServiceCollection().BuildServiceProvider();
        var availability = new DatabaseAvailability();
        availability.MarkAvailable();
        var logger = new TestLogger<DatabaseAvailabilityMonitor>();

        var monitor = new DatabaseAvailabilityMonitor(
            services,
            availability,
            logger,
            Options.Create(new DatabaseAvailabilityMonitorOptions()),
            connectionFactory: _ => throw new NpgsqlException("Connection timeout"));

        // First failure transition
        await monitor.ProbeAsync();
        Assert.False(availability.IsAvailable);
        var errorCountAfterFirst = logger.Logs.Count(l => l.Level == LogLevel.Error);
        Assert.Equal(1, errorCountAfterFirst);

        // Second failure (already unavailable)
        await monitor.ProbeAsync();
        Assert.False(availability.IsAvailable);
        var errorCountAfterSecond = logger.Logs.Count(l => l.Level == LogLevel.Error);
        // Error log should NOT increase; it logs at Debug level instead
        Assert.Equal(1, errorCountAfterSecond);
        Assert.Contains(logger.Logs, l => l.Level == LogLevel.Debug && l.Message.Contains("remains unavailable"));
    }

    [Fact]
    public async Task LaterSuccessfulProbe_MarksAvailableAgain()
    {
        var services = new ServiceCollection().BuildServiceProvider();
        var availability = new DatabaseAvailability();
        var logger = new TestLogger<DatabaseAvailabilityMonitor>();
        var shouldSucceed = false;

        var monitor = new DatabaseAvailabilityMonitor(
            services,
            availability,
            logger,
            Options.Create(new DatabaseAvailabilityMonitorOptions()),
            connectionFactory: _ =>
            {
                if (!shouldSucceed)
                {
                    throw new TimeoutException("Database timeout");
                }

                return Task.FromResult<IAsyncDisposable>(new TrackedAsyncDisposable());
            });

        // 1. Initial failed probe
        await monitor.ProbeAsync();
        Assert.False(availability.IsAvailable);

        // 2. Database recovers
        shouldSucceed = true;
        await monitor.ProbeAsync();

        Assert.True(availability.IsAvailable);
        Assert.Contains(logger.Logs, l => l.Level == LogLevel.Information && l.Message.Contains("restored"));
    }

    [Fact]
    public async Task ProbeConnection_IsDisposedAfterIteration()
    {
        var services = new ServiceCollection().BuildServiceProvider();
        var availability = new DatabaseAvailability();
        var logger = new TestLogger<DatabaseAvailabilityMonitor>();
        var trackedConnection = new TrackedAsyncDisposable();

        var monitor = new DatabaseAvailabilityMonitor(
            services,
            availability,
            logger,
            Options.Create(new DatabaseAvailabilityMonitorOptions()),
            connectionFactory: _ => Task.FromResult<IAsyncDisposable>(trackedConnection));

        await monitor.ProbeAsync();

        Assert.True(trackedConnection.IsDisposed);
        Assert.Equal(1, trackedConnection.DisposeCallCount);
    }

    [Fact]
    public async Task UnexpectedException_DoesNotTerminateHostedService()
    {
        var services = new ServiceCollection().BuildServiceProvider();
        var availability = new DatabaseAvailability();
        var logger = new TestLogger<DatabaseAvailabilityMonitor>();
        var options = new DatabaseAvailabilityMonitorOptions
        {
            EarlyProbeDelay = TimeSpan.Zero,
            Interval = TimeSpan.FromMilliseconds(20)
        };

        var attemptCount = 0;
        var secondAttemptReached = new TaskCompletionSource<bool>();

        var monitor = new DatabaseAvailabilityMonitor(
            services,
            availability,
            logger,
            Options.Create(options),
            connectionFactory: _ =>
            {
                var current = Interlocked.Increment(ref attemptCount);
                if (current == 1)
                {
                    // Unexpected non-database exception
                    throw new InvalidOperationException("Unexpected internal state");
                }

                secondAttemptReached.TrySetResult(true);
                return Task.FromResult<IAsyncDisposable>(new TrackedAsyncDisposable());
            });

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        await monitor.StartAsync(cts.Token);

        // Wait for second attempt to prove loop survived first unexpected exception
        var reached = await Task.WhenAny(secondAttemptReached.Task, Task.Delay(2000, cts.Token)) == secondAttemptReached.Task;
        await monitor.StopAsync(CancellationToken.None);

        Assert.True(reached, "The hosted service should continue running and execute subsequent probes even after an unexpected exception.");
        Assert.True(attemptCount >= 2);
    }

    [Fact]
    public async Task Cancellation_StopsTimerLoopCleanly()
    {
        var services = new ServiceCollection().BuildServiceProvider();
        var availability = new DatabaseAvailability();
        var logger = new TestLogger<DatabaseAvailabilityMonitor>();
        var options = new DatabaseAvailabilityMonitorOptions
        {
            EarlyProbeDelay = TimeSpan.Zero,
            Interval = TimeSpan.FromMilliseconds(50)
        };

        var monitor = new DatabaseAvailabilityMonitor(
            services,
            availability,
            logger,
            Options.Create(options),
            connectionFactory: _ => Task.FromResult<IAsyncDisposable>(new TrackedAsyncDisposable()));

        using var cts = new CancellationTokenSource();
        await monitor.StartAsync(cts.Token);

        // Cancel and stop
        cts.Cancel();
        await monitor.StopAsync(CancellationToken.None);

        // Should complete without unhandled OperationCanceledException bubbling up
        Assert.True(true);
    }
}
