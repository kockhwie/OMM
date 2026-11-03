using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using OMM.Public.Data;
using OMM.Shared.Models;
using OMM.Shared.Models.Phase5;
using OMM.Shared.Prompts;
using OMM.Shared.Subscription;
using Xunit;

namespace OMM.Integration.Tests;

public class PromptAdministrationTests
{
    private sealed class TestTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        private DateTimeOffset _utcNow = utcNow;
        public override DateTimeOffset GetUtcNow() => _utcNow;
        public void Advance(TimeSpan timeSpan) => _utcNow = _utcNow.Add(timeSpan);
    }

    private sealed class TestSubscriptionDbContextFactory(DbContextOptions<ApplicationDbContext> options) : ISubscriptionDbContextFactory
    {
        public Task<ISubscriptionDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult<ISubscriptionDbContext>(new ApplicationDbContext(options));
        }
    }

    private sealed class FaultySubscriptionDbContextFactory : ISubscriptionDbContextFactory
    {
        public Task<ISubscriptionDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("Simulated database outage.");
        }
    }

    private sealed class TestHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "IntegrationTest";
        public string ApplicationName { get; set; } = "OMM.Tests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } = null!;
    }

    private static (DbContextOptions<ApplicationDbContext> Options, ISubscriptionDbContextFactory Factory, EmergencyPromptCatalog Catalog) CreateEnvironment()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        using (var context = new ApplicationDbContext(options))
        {
            context.Database.EnsureCreated();

            if (!context.AiPromptModes.Any())
            {
                context.AiPromptModes.AddRange(
                    new AiPromptMode { Code = "STANDARD", DisplayName = "Standard", IsAdvisory = false, IsActive = true, SchemaVersion = "v1" },
                    new AiPromptMode { Code = "PRO", DisplayName = "Pro", IsAdvisory = true, IsActive = true, SchemaVersion = "v1" });
            }

            if (!context.SystemSettings.Any(x => x.Key == PromptAdministrationService.SettingsRevisionKey))
            {
                context.SystemSettings.Add(new SystemSetting
                {
                    Key = PromptAdministrationService.SettingsRevisionKey,
                    Value = "1",
                    Description = "Monotonic Phase 5 prompt and settings revision."
                });
            }

            context.SaveChanges();
        }

        var factory = new TestSubscriptionDbContextFactory(options);
        var catalog = new EmergencyPromptCatalog();
        return (options, factory, catalog);
    }

    private static PromptAdministrationService CreateService(
        ISubscriptionDbContextFactory factory,
        EmergencyPromptCatalog catalog,
        TimeProvider timeProvider,
        IMemoryCache? cache = null)
    {
        cache ??= new MemoryCache(new MemoryCacheOptions());
        var config = new ConfigurationBuilder().Build();
        var hostEnv = new TestHostEnvironment();
        var logger = NullLogger<PromptAdministrationService>.Instance;

        return new PromptAdministrationService(
            factory,
            cache,
            catalog,
            config,
            hostEnv,
            timeProvider,
            logger);
    }

    private static async Task DeactivateActivePromptsAsync(
        DbContextOptions<ApplicationDbContext> options,
        string promptModeCode)
    {
        await using var db = new ApplicationDbContext(options);
        var activeVersions = await db.AiPromptVersions
            .Where(version => version.PromptModeCode == promptModeCode && version.Status == "Active")
            .ToListAsync();

        foreach (var version in activeVersions)
        {
            version.Status = "Inactive";
        }

        await db.SaveChangesAsync();
    }

    [Fact]
    public void EmergencyPromptCatalog_LoadsAndValidatesStandardAndProBaselines()
    {
        var catalog = new EmergencyPromptCatalog();
        var prompts = catalog.GetAll();

        Assert.Equal(2, prompts.Count);
        Assert.All(prompts, p => Assert.True(p.IsValid, $"Emergency prompt {p.PromptModeCode} has invalid content hash."));

        var standard = catalog.Get("STANDARD");
        Assert.NotNull(standard);
        Assert.Equal("STANDARD", standard.PromptModeCode);
        Assert.Equal("v1", standard.SchemaVersion);
        Assert.Contains("{memberText}", standard.UserMessageTemplate);

        var pro = catalog.Get("PRO");
        Assert.NotNull(pro);
        Assert.Equal("PRO", pro.PromptModeCode);
        Assert.Equal("v1", pro.SchemaVersion);
        Assert.Contains("{memberText}", pro.UserMessageTemplate);
    }

    [Fact]
    public async Task GetActivePromptAsync_ReturnsDatabaseActivePrompt_WhenConfigured()
    {
        var (options, factory, catalog) = CreateEnvironment();
        var timeProvider = new TestTimeProvider(DateTimeOffset.UtcNow);
        await DeactivateActivePromptsAsync(options, "STANDARD");

        using (var db = new ApplicationDbContext(options))
        {
            db.AiPromptVersions.Add(new AiPromptVersion
            {
                Id = 10,
                PromptModeCode = "STANDARD",
                VersionLabel = "Custom Active Standard v1",
                SystemInstruction = "Custom system instructions for test.",
                UserMessageTemplate = "Member text: <memberText>{memberText}</memberText>",
                SchemaVersion = "v1",
                ContentHash = "hash10",
                Status = "Active",
                CreatedAt = timeProvider.GetUtcNow(),
                CreatedByUserId = "admin-1"
            });
            await db.SaveChangesAsync();
        }

        var service = CreateService(factory, catalog, timeProvider);
        var prompt = await service.GetActivePromptAsync("STANDARD");

        Assert.NotNull(prompt);
        Assert.Equal(10, prompt.Id);
        Assert.Equal("Custom Active Standard v1", prompt.VersionLabel);
        Assert.False(prompt.IsEmergency);
    }

    [Fact]
    public async Task GetActivePromptAsync_FallsBackToEmergencyBaseline_WhenNoActiveVersionInDatabase()
    {
        var (options, factory, catalog) = CreateEnvironment();
        var timeProvider = new TestTimeProvider(DateTimeOffset.UtcNow);
        await DeactivateActivePromptsAsync(options, "STANDARD");
        var service = CreateService(factory, catalog, timeProvider);

        // The seeded Standard version is inactive, so no database prompt is active for this mode.
        var prompt = await service.GetActivePromptAsync("STANDARD");

        Assert.NotNull(prompt);
        Assert.Equal(0, prompt.Id);
        Assert.True(prompt.IsEmergency);
        Assert.Equal("STANDARD", prompt.PromptModeCode);
        Assert.Equal("Standard Emergency v1", prompt.VersionLabel);
    }

    [Fact]
    public async Task GetActivePromptAsync_FallsBackToEmergencyBaseline_WhenDatabaseOutageOccurs()
    {
        var catalog = new EmergencyPromptCatalog();
        var timeProvider = new TestTimeProvider(DateTimeOffset.UtcNow);
        var faultyFactory = new FaultySubscriptionDbContextFactory();
        var service = CreateService(faultyFactory, catalog, timeProvider);

        var prompt = await service.GetActivePromptAsync("PRO");

        Assert.NotNull(prompt);
        Assert.Equal(0, prompt.Id);
        Assert.True(prompt.IsEmergency);
        Assert.Equal("PRO", prompt.PromptModeCode);
        Assert.Equal("Pro Emergency v1", prompt.VersionLabel);
    }

    [Fact]
    public async Task ResolvePromptAsync_StandardMode_ReturnsStandardPrompt()
    {
        var (_, factory, catalog) = CreateEnvironment();
        var timeProvider = new TestTimeProvider(DateTimeOffset.UtcNow);
        var service = CreateService(factory, catalog, timeProvider);

        var resolution = await service.ResolvePromptAsync("STANDARD");

        Assert.NotNull(resolution.Prompt);
        Assert.Equal("STANDARD", resolution.EffectiveMode);
        Assert.False(resolution.IsFallback);
        Assert.False(resolution.IsUnavailable);
    }

    [Fact]
    public async Task ResolvePromptAsync_ProMode_ReturnsProPrompt_WhenProEnabled()
    {
        var (_, factory, catalog) = CreateEnvironment();
        var timeProvider = new TestTimeProvider(DateTimeOffset.UtcNow);
        var service = CreateService(factory, catalog, timeProvider);

        var resolution = await service.ResolvePromptAsync("PRO", proEnabled: true);

        Assert.NotNull(resolution.Prompt);
        Assert.Equal("PRO", resolution.EffectiveMode);
        Assert.False(resolution.IsFallback);
        Assert.False(resolution.IsUnavailable);
    }

    [Fact]
    public async Task ResolvePromptAsync_ProMode_FallsBackToStandard_WhenPublicProIsDisabled()
    {
        var (_, factory, catalog) = CreateEnvironment();
        var timeProvider = new TestTimeProvider(DateTimeOffset.UtcNow);
        var service = CreateService(factory, catalog, timeProvider);

        // When proEnabled is false (admin toggled off PublicProEnabled)
        var resolution = await service.ResolvePromptAsync("PRO", proEnabled: false);

        Assert.NotNull(resolution.Prompt);
        Assert.Equal("STANDARD", resolution.EffectiveMode);
        Assert.True(resolution.IsFallback);
        Assert.Equal("PublicProDisabled", resolution.FallbackReason);
        Assert.False(resolution.IsUnavailable);
    }

    [Fact]
    public async Task ResolvePromptAsync_SimulatorOverride_ReturnsRequestedVersion()
    {
        var (options, factory, catalog) = CreateEnvironment();
        var timeProvider = new TestTimeProvider(DateTimeOffset.UtcNow);

        using (var db = new ApplicationDbContext(options))
        {
            db.AiPromptVersions.Add(new AiPromptVersion
            {
                Id = 99,
                PromptModeCode = "PRO",
                VersionLabel = "Experimental Draft Pro",
                SystemInstruction = "Experimental instruction",
                UserMessageTemplate = "User template: <memberText>{memberText}</memberText>",
                SchemaVersion = "v1",
                ContentHash = "hash99",
                Status = "Draft", // Note: It's Draft, not Active
                CreatedAt = timeProvider.GetUtcNow(),
                CreatedByUserId = "admin"
            });
            await db.SaveChangesAsync();
        }

        var service = CreateService(factory, catalog, timeProvider);
        var resolution = await service.ResolvePromptAsync("PRO", proEnabled: true, overrideVersionId: 99);

        Assert.NotNull(resolution.Prompt);
        Assert.Equal(99, resolution.Prompt.Id);
        Assert.Equal("Experimental Draft Pro", resolution.Prompt.VersionLabel);
        Assert.Equal("Draft", resolution.Prompt.Status);
        Assert.False(resolution.IsFallback);
    }

    [Fact]
    public async Task CreateDraftAsync_ValidatesPlaceholders_AndAuditsCreation()
    {
        var (_, factory, catalog) = CreateEnvironment();
        var timeProvider = new TestTimeProvider(DateTimeOffset.UtcNow);
        var service = CreateService(factory, catalog, timeProvider);

        // 1. Missing {memberText} must fail
        await Assert.ThrowsAsync<ArgumentException>(() => service.CreateDraftAsync(
            new PromptDraftInput("STANDARD", "Draft 1", "System instructions", "No placeholder here", null),
            "admin-1"));

        // 2. Unsupported placeholder like {injectedPlaceholder} must fail
        await Assert.ThrowsAsync<ArgumentException>(() => service.CreateDraftAsync(
            new PromptDraftInput("STANDARD", "Draft 2", "System instructions", "Text: {memberText} and {unauthorizedPlaceholder}", null),
            "admin-1"));

        // 3. Valid draft creation
        var draft = await service.CreateDraftAsync(
            new PromptDraftInput("STANDARD", "Standard v2", "New system instruction", "Message: {memberText}", "Test draft creation"),
            "admin-1");

        Assert.True(draft.Id > 0);
        Assert.Equal("Draft", draft.Status);
        Assert.Equal("Standard v2", draft.VersionLabel);

        // Audit events verify
        var audits = await service.GetAuditEventsAsync("STANDARD");
        Assert.Contains(audits, a => a.Action == "Created" && a.PromptVersionId == draft.Id);
    }

    [Fact]
    public async Task ActivateAsync_TransitionsStates_AndAuditsBothVersions()
    {
        var (_, factory, catalog) = CreateEnvironment();
        var timeProvider = new TestTimeProvider(DateTimeOffset.UtcNow);
        var service = CreateService(factory, catalog, timeProvider);

        var draft1 = await service.CreateDraftAsync(
            new PromptDraftInput("STANDARD", "Standard v10", "System 10", "{memberText}", "First draft"),
            "admin-1");
        var draft2 = await service.CreateDraftAsync(
            new PromptDraftInput("STANDARD", "Standard v11", "System 11", "{memberText}", "Second draft"),
            "admin-1");

        // Activate v10
        var activated10 = await service.ActivateAsync(draft1.Id, "admin-1", "Activating v10");
        Assert.Equal("Active", activated10.Status);

        // Activate v11: v10 should become Inactive, v11 should become Active
        var activated11 = await service.ActivateAsync(draft2.Id, "admin-1", "Activating v11 to replace v10");
        Assert.Equal("Active", activated11.Status);

        var v10After = await service.GetVersionAsync(draft1.Id);
        Assert.NotNull(v10After);
        Assert.Equal("Inactive", v10After.Status);

        // Audit events verify
        var audits = await service.GetAuditEventsAsync("STANDARD");
        Assert.Contains(audits, a => a.Action == "Activated" && a.PromptVersionId == draft2.Id);
        Assert.Contains(audits, a => a.Action == "Deactivated" && a.PromptVersionId == draft1.Id);
    }

    [Fact]
    public async Task RollbackAndArchive_EnforcesStatusRules()
    {
        var (_, factory, catalog) = CreateEnvironment();
        var timeProvider = new TestTimeProvider(DateTimeOffset.UtcNow);
        var service = CreateService(factory, catalog, timeProvider);

        var draft1 = await service.CreateDraftAsync(
            new PromptDraftInput("PRO", "Pro v10", "System 10", "{memberText}", null),
            "admin-1");
        var draft2 = await service.CreateDraftAsync(
            new PromptDraftInput("PRO", "Pro v11", "System 11", "{memberText}", null),
            "admin-1");

        await service.ActivateAsync(draft1.Id, "admin-1");
        await service.ActivateAsync(draft2.Id, "admin-1");

        // draft1 is now Inactive, draft2 is Active
        // 1. Rollback to draft1 should succeed
        var rolledBack = await service.RollbackAsync(draft1.Id, "admin-1", "Rolling back to previous stable prompt");
        Assert.Equal("Active", rolledBack.Status);

        // draft2 is now Inactive
        // 2. Archiving active prompt (draft1) must fail
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.ArchiveAsync(draft1.Id, "admin-1"));

        // 3. Archiving inactive prompt (draft2) should succeed
        await service.ArchiveAsync(draft2.Id, "admin-1", "Archiving unused draft");
        var draft2After = await service.GetVersionAsync(draft2.Id);
        Assert.NotNull(draft2After);
        Assert.Equal("Archived", draft2After.Status);

        // 4. Activating archived prompt must fail
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.ActivateAsync(draft2.Id, "admin-1"));
    }
}

