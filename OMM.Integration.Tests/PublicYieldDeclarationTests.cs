using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using OMM.Public.Data;
using OMM.Public.Data.Entities;
using OMM.Public.Models;
using OMM.Public.Services;

namespace OMM.Integration.Tests;

public sealed class PublicYieldDeclarationTests
{
    [Fact]
    public async Task Declaration_with_income_creates_both_records_atomically()
    {
        await using var fixture = await DeclarationFixture.CreateAsync();
        var service = fixture.CreateService();

        Assert.True(await service.RecordYieldDeclarationAsync(
            fixture.MineId.ToString(),
            effectiveYear: 2025,
            recordDate: new DateOnly(2025, 2, 28),
            ratePct: 6.00m,
            declaredAmount: 2400m,
            balanceAtTime: 40000m,
            notes: "Annual declaration",
            createIncomeRecord: true));

        await using var db = fixture.CreateContext();
        var history = await db.MineYieldHistories.SingleAsync();
        var income = await db.IncomeRecords.SingleAsync();

        Assert.Equal(fixture.MineId, history.MineId);
        Assert.Equal(2025, history.EffectiveYear);
        Assert.Equal(6.00m, history.RatePct);
        Assert.Equal(2400m, history.DeclaredAmount);
        Assert.Equal(fixture.MineId, income.MineId);
        Assert.Equal(IncomeClass.PassiveMineGenerated, income.Classification);
        Assert.Equal(RecordFrequency.Annual, income.Frequency);
        Assert.Equal(2400m, income.Amount);
        Assert.Equal(1, income.CurrencyId);
        Assert.Equal(new DateOnly(2025, 2, 28), income.RecordDate);
    }

    [Fact]
    public async Task Declaration_rolls_back_yield_snapshot_when_income_save_fails()
    {
        await using var fixture = await DeclarationFixture.CreateAsync(new ThrowOnIncomeSaveInterceptor());
        var service = fixture.CreateService();

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.RecordYieldDeclarationAsync(
            fixture.MineId.ToString(),
            effectiveYear: 2025,
            recordDate: new DateOnly(2025, 2, 28),
            ratePct: 6.00m,
            declaredAmount: 2400m,
            createIncomeRecord: true));

        await using var db = fixture.CreateContext();
        Assert.Empty(await db.MineYieldHistories.ToListAsync());
        Assert.Empty(await db.IncomeRecords.ToListAsync());
    }

    private sealed class DeclarationFixture : IAsyncDisposable
    {
        private readonly string databaseName;
        private readonly string adminConnectionString;
        private readonly DbContextOptions<ApplicationDbContext> options;
        private readonly TestAuthenticationStateProvider authenticationStateProvider;

        private DeclarationFixture(
            string databaseName,
            string adminConnectionString,
            DbContextOptions<ApplicationDbContext> options,
            TestAuthenticationStateProvider authenticationStateProvider,
            Guid mineId)
        {
            this.databaseName = databaseName;
            this.adminConnectionString = adminConnectionString;
            this.options = options;
            this.authenticationStateProvider = authenticationStateProvider;
            MineId = mineId;
        }

        public Guid MineId { get; }

        public static async Task<DeclarationFixture> CreateAsync(ISaveChangesInterceptor? interceptor = null)
        {
            var configuredConnectionString = Environment.GetEnvironmentVariable("OMM_TEST_CONNECTION_STRING");
            if (string.IsNullOrWhiteSpace(configuredConnectionString))
            {
                throw new InvalidOperationException(
                    "OMM_TEST_CONNECTION_STRING must point to a local PostgreSQL server. " +
                    "The integration tests will create and remove a temporary database on that server.");
            }

            var configured = new NpgsqlConnectionStringBuilder(configuredConnectionString);
            if (string.IsNullOrWhiteSpace(configured.Host) || string.IsNullOrWhiteSpace(configured.Username))
            {
                throw new InvalidOperationException("OMM_TEST_CONNECTION_STRING must include a PostgreSQL host and username.");
            }

            var databaseName = $"ommv2_integration_{Guid.NewGuid():N}";
            var adminBuilder = new NpgsqlConnectionStringBuilder(configuredConnectionString)
            {
                Database = "postgres"
            };
            var adminConnectionString = adminBuilder.ConnectionString;

            await using (var adminConnection = new NpgsqlConnection(adminConnectionString))
            {
                await adminConnection.OpenAsync();
                await using var createCommand = adminConnection.CreateCommand();
                createCommand.CommandText = $"CREATE DATABASE {new NpgsqlCommandBuilder().QuoteIdentifier(databaseName)}";
                await createCommand.ExecuteNonQueryAsync();
            }

            try
            {
                var databaseBuilder = new NpgsqlConnectionStringBuilder(configuredConnectionString)
                {
                    Database = databaseName
                };
                var optionsBuilder = new DbContextOptionsBuilder<ApplicationDbContext>()
                    .UseNpgsql(databaseBuilder.ConnectionString);
                if (interceptor is not null)
                {
                    optionsBuilder.AddInterceptors(interceptor);
                }

                var options = optionsBuilder.Options;
                var auth = CreateAuthProvider("user-1");
                var mineId = Guid.NewGuid();

                await using (var db = new ApplicationDbContext(options))
                {
                    await db.Database.MigrateAsync();
                    db.Users.Add(new ApplicationUser
                    {
                        Id = "user-1",
                        UserName = "user-1@example.com",
                        Email = "user-1@example.com"
                    });
                    db.MinerProfiles.Add(new MinerProfileEntity
                    {
                        UserId = "user-1",
                        Email = "user-1@example.com",
                        CurrencyId = 1,
                        CreatedAt = DateTimeOffset.UtcNow
                    });
                    db.Mines.Add(new MineEntity
                    {
                        Id = mineId,
                        UserId = "user-1",
                        Name = "Test mine",
                        Category = MineCategory.Investments,
                        Type = MineType.Stocks,
                        CurrencyId = 1,
                        Status = "active",
                        UpdatedOn = DateOnly.FromDateTime(DateTime.UtcNow),
                        CreatedAt = DateTimeOffset.UtcNow
                    });
                    await db.SaveChangesAsync();
                }

                return new DeclarationFixture(databaseName, adminConnectionString, options, auth, mineId);
            }
            catch
            {
                await DropDatabaseAsync(adminConnectionString, databaseName);
                throw;
            }
        }

        public DatabaseMineService CreateService()
        {
            var factory = new TestDbContextFactory(options);
            var profileService = new MinerProfileService(factory, authenticationStateProvider, NullLogger<MinerProfileService>.Instance);
            return new DatabaseMineService(new MineRepository(factory, authenticationStateProvider), factory, profileService);
        }

        public ApplicationDbContext CreateContext() => new(options);

        public async ValueTask DisposeAsync() => await DropDatabaseAsync(adminConnectionString, databaseName);

        private static async Task DropDatabaseAsync(string adminConnectionString, string databaseName)
        {
            await using var adminConnection = new NpgsqlConnection(adminConnectionString);
            await adminConnection.OpenAsync();
            await using var dropCommand = adminConnection.CreateCommand();
            dropCommand.CommandText = $"DROP DATABASE IF EXISTS {new NpgsqlCommandBuilder().QuoteIdentifier(databaseName)} WITH (FORCE)";
            await dropCommand.ExecuteNonQueryAsync();
        }

        private static TestAuthenticationStateProvider CreateAuthProvider(string userId) => new(
            new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim(ClaimTypes.NameIdentifier, userId)], "Test")));
    }

    private sealed class TestDbContextFactory(DbContextOptions<ApplicationDbContext> options)
        : IDbContextFactory<ApplicationDbContext>
    {
        public ApplicationDbContext CreateDbContext() => new(options);

        public Task<ApplicationDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new ApplicationDbContext(options));
    }

    private sealed class TestAuthenticationStateProvider(ClaimsPrincipal initialUser) : AuthenticationStateProvider
    {
        private readonly ClaimsPrincipal user = initialUser;

        public override Task<AuthenticationState> GetAuthenticationStateAsync() =>
            Task.FromResult(new AuthenticationState(user));
    }

    private sealed class ThrowOnIncomeSaveInterceptor : SaveChangesInterceptor
    {
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            if (eventData.Context?.ChangeTracker.Entries<IncomeRecordEntity>().Any(entry => entry.State == EntityState.Added) == true)
            {
                throw new InvalidOperationException("Injected income save failure.");
            }

            return base.SavingChangesAsync(eventData, result, cancellationToken);
        }
    }
}
