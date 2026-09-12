using System.IO.Pipelines;
using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Npgsql;
using OMM.Shared.Database;
using OMM.Shared.Infrastructure;
using Xunit;

namespace OMM.Shared.Tests.Database;

public class DatabaseFailureMiddlewareTests
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

    private static (RequestDelegate Pipeline, DatabaseAvailability Availability, TestAlertEmailSender EmailSender, IServiceProvider Services)
        CreateTestPipeline(RequestDelegate nextHandler, Action<TestAlertEmailSender>? configureSender = null)
    {
        var services = new ServiceCollection();
        var availability = new DatabaseAvailability();
        availability.MarkAvailable();

        var emailSender = new TestAlertEmailSender();
        configureSender?.Invoke(emailSender);

        var tempDir = Path.Combine(Path.GetTempPath(), "omm-tests-" + Guid.NewGuid().ToString("N"));
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

        var appBuilder = new ApplicationBuilder(sp);
        appBuilder.UseDatabaseFailureNotifications("TestApp");
        appBuilder.Run(nextHandler);
        var pipeline = appBuilder.Build();

        return (pipeline, availability, emailSender, sp);
    }

    private sealed class TestResponseFeature : IHttpResponseFeature
    {
        public int StatusCode { get; set; } = 200;
        public string? ReasonPhrase { get; set; }
        public IHeaderDictionary Headers { get; set; } = new HeaderDictionary();
        public Stream Body { get; set; } = Stream.Null;
        public bool HasStarted { get; set; }

        public void OnStarting(Func<object, Task> callback, object state) { }
        public void OnCompleted(Func<object, Task> callback, object state) { }
    }

    private sealed class TestResponseBodyFeature : IHttpResponseBodyFeature
    {
        private readonly StreamResponseBodyFeature _inner;
        private readonly TestResponseFeature _responseFeature;

        public TestResponseBodyFeature(Stream stream, TestResponseFeature responseFeature)
        {
            _inner = new StreamResponseBodyFeature(stream);
            _responseFeature = responseFeature;
        }

        public Stream Stream => _inner.Stream;
        public PipeWriter Writer => _inner.Writer;

        public Task StartAsync(CancellationToken cancellationToken = default)
        {
            _responseFeature.HasStarted = true;
            return _inner.StartAsync(cancellationToken);
        }

        public Task SendFileAsync(string path, long offset, long? count, CancellationToken cancellationToken = default) =>
            _inner.SendFileAsync(path, offset, count, cancellationToken);

        public Task CompleteAsync() => _inner.CompleteAsync();
        public void DisableBuffering() => _inner.DisableBuffering();
    }

    private static DefaultHttpContext CreateHttpContext(IServiceProvider sp, bool acceptHtml = true)
    {
        var context = new DefaultHttpContext
        {
            RequestServices = sp
        };
        context.Request.Path = "/api/test";
        if (acceptHtml)
        {
            context.Request.Headers.Accept = "text/html";
        }
        var memoryStream = new MemoryStream();
        var responseFeature = new TestResponseFeature { Body = memoryStream };
        context.Features.Set<IHttpResponseFeature>(responseFeature);
        context.Features.Set<IHttpResponseBodyFeature>(new TestResponseBodyFeature(memoryStream, responseFeature));
        return context;
    }

    [Fact]
    public async Task DatabaseException_CallsMarkUnavailable()
    {
        var (pipeline, availability, _, sp) = CreateTestPipeline(_ =>
            throw new NpgsqlException("PostgreSQL connection lost"));

        var context = CreateHttpContext(sp);
        await pipeline(context);

        Assert.False(availability.IsAvailable);
    }

    [Fact]
    public async Task FirstOutageRequest_GetsHttp503AndFriendlyAvailabilityHtml()
    {
        var (pipeline, _, emailSender, sp) = CreateTestPipeline(_ =>
            throw new NpgsqlException("PostgreSQL connection lost"));

        var context = CreateHttpContext(sp, acceptHtml: true);
        await pipeline(context);

        Assert.Equal(StatusCodes.Status503ServiceUnavailable, context.Response.StatusCode);
        Assert.Equal("text/html; charset=utf-8", context.Response.ContentType);

        context.Response.Body.Seek(0, SeekOrigin.Begin);
        using var reader = new StreamReader(context.Response.Body, Encoding.UTF8);
        var body = await reader.ReadToEndAsync();

        Assert.Contains("We’ll be back shortly", body);
        Assert.Contains("OMM temporarily unavailable", body);
        Assert.Single(emailSender.SentAlerts);
        Assert.Equal("TestApp", emailSender.SentAlerts[0].Application);
    }

    [Fact]
    public async Task SubsequentOutageRequests_DoNotSendDuplicateNotifications()
    {
        var (pipeline, availability, emailSender, sp) = CreateTestPipeline(_ =>
            throw new NpgsqlException("PostgreSQL connection lost"));

        // First request - state changes available -> unavailable
        var context1 = CreateHttpContext(sp);
        await pipeline(context1);

        Assert.Equal(StatusCodes.Status503ServiceUnavailable, context1.Response.StatusCode);
        Assert.Single(emailSender.SentAlerts);

        // Second request - already unavailable
        var context2 = CreateHttpContext(sp);
        await pipeline(context2);

        Assert.Equal(StatusCodes.Status503ServiceUnavailable, context2.Response.StatusCode);
        // Alert count should remain 1 (no duplicate email)
        Assert.Single(emailSender.SentAlerts);
    }

    [Fact]
    public async Task ConcurrentOutageRequests_SendExactlyOneNotification()
    {
        var (pipeline, availability, emailSender, sp) = CreateTestPipeline(_ =>
            throw new NpgsqlException("PostgreSQL connection lost"));

        const int requestCount = 50;
        var tasks = Enumerable.Range(0, requestCount).Select(async _ =>
        {
            var context = CreateHttpContext(sp);
            await pipeline(context);
            return context.Response.StatusCode;
        });

        var results = await Task.WhenAll(tasks);

        Assert.All(results, code => Assert.Equal(StatusCodes.Status503ServiceUnavailable, code));
        Assert.Single(emailSender.SentAlerts);
        Assert.False(availability.IsAvailable);
    }

    [Fact]
    public async Task NonDatabaseException_IsRethrownUnchanged()
    {
        var expectedException = new InvalidOperationException("Custom app exception");
        var (pipeline, availability, emailSender, sp) = CreateTestPipeline(_ =>
            throw expectedException);

        var context = CreateHttpContext(sp);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => pipeline(context));

        Assert.Same(expectedException, ex);
        Assert.True(availability.IsAvailable);
        Assert.Empty(emailSender.SentAlerts);
    }

    [Fact]
    public async Task ResponseAlreadyStarted_RethrowsDatabaseException()
    {
        var expectedException = new NpgsqlException("Fatal database disconnect");
        var (pipeline, availability, emailSender, sp) = CreateTestPipeline(async context =>
        {
            await context.Response.StartAsync();
            throw expectedException;
        });

        var context = CreateHttpContext(sp);
        var ex = await Assert.ThrowsAsync<NpgsqlException>(() => pipeline(context));

        Assert.Same(expectedException, ex);
        Assert.False(availability.IsAvailable);
        Assert.Single(emailSender.SentAlerts);
    }

    [Fact]
    public async Task NotifierFailure_DoesNotReplaceOriginalDatabaseException()
    {
        var expectedException = new NpgsqlException("Fatal database disconnect");
        var (pipeline, availability, _, sp) = CreateTestPipeline(async context =>
        {
            await context.Response.StartAsync();
            throw expectedException;
        }, sender => sender.ShouldThrow = true);

        var context = CreateHttpContext(sp);
        // Even though notifier throws, the original NpgsqlException is rethrown when response has started
        var ex = await Assert.ThrowsAsync<NpgsqlException>(() => pipeline(context));

        Assert.Same(expectedException, ex);
        Assert.False(availability.IsAvailable);
    }
}
