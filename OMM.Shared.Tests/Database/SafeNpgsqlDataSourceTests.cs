using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using OMM.Shared.Database;
using Xunit;

namespace OMM.Shared.Tests.Database;

public class SafeNpgsqlDataSourceTests
{
    [Fact]
    public void RegistrationDoesNotThrow_WhenConnectionStringIsMissingOrInvalid()
    {
        var services = new ServiceCollection();
        services.AddDatabaseAvailability();
        services.AddSafeNpgsqlDataSource("   invalid   ;; ===");

        // Building the service provider should not throw during startup
        var sp = services.BuildServiceProvider();
        Assert.NotNull(sp);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ResolutionThrowsNpgsqlException_AndMarksUnavailable_WhenConnectionStringIsMissing(string? connectionString)
    {
        var services = new ServiceCollection();
        var availability = new DatabaseAvailability();
        availability.MarkAvailable();

        services.AddSingleton(availability);
        services.AddSingleton<ILoggerFactory, NullLoggerFactory>();
        services.AddSafeNpgsqlDataSource(connectionString);

        var sp = services.BuildServiceProvider();

        var ex = Assert.Throws<NpgsqlException>(() => sp.GetRequiredService<NpgsqlDataSource>());
        Assert.Contains("connection string is not configured", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.False(availability.IsAvailable);
    }

    [Fact]
    public void ResolutionThrowsNpgsqlException_AndMarksUnavailable_WhenConnectionStringIsInvalid()
    {
        var services = new ServiceCollection();
        var availability = new DatabaseAvailability();
        availability.MarkAvailable();

        services.AddSingleton(availability);
        services.AddSingleton<ILoggerFactory, NullLoggerFactory>();
        services.AddSafeNpgsqlDataSource("not a valid connection string: ;;====");

        var sp = services.BuildServiceProvider();

        var ex = Assert.Throws<NpgsqlException>(() => sp.GetRequiredService<NpgsqlDataSource>());
        Assert.Contains("format is invalid", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.False(availability.IsAvailable);
    }

    [Fact]
    public void ResolutionSucceeds_WhenConnectionStringIsValid()
    {
        var services = new ServiceCollection();
        var availability = new DatabaseAvailability();
        availability.MarkAvailable();

        services.AddSingleton(availability);
        services.AddSingleton<ILoggerFactory, NullLoggerFactory>();
        services.AddSafeNpgsqlDataSource("Host=localhost;Database=test;Username=test;Password=test");

        var sp = services.BuildServiceProvider();

        using var dataSource1 = sp.GetRequiredService<NpgsqlDataSource>();
        using var dataSource2 = sp.GetRequiredService<NpgsqlDataSource>();

        Assert.NotNull(dataSource1);
        Assert.Same(dataSource1, dataSource2);
        Assert.True(availability.IsAvailable);
    }
}
