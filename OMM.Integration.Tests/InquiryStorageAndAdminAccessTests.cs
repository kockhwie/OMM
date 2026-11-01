using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using OMM.Admin.Data;
using OMM.Admin.Services.Admin;
using OMM.Public.Data;
using OMM.Public.Services;
using OMM.Shared.Infrastructure.Settings;
using OMM.Shared.Inquiry;
using OMM.Shared.Models.Phase5;
using OMM.Shared.Subscription;
using Xunit;

namespace OMM.Integration.Tests;

public class InquiryStorageAndAdminAccessTests
{
    private sealed class TestTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        private DateTimeOffset _utcNow = utcNow;
        public override DateTimeOffset GetUtcNow() => _utcNow;
        public void Advance(TimeSpan timeSpan) => _utcNow = _utcNow.Add(timeSpan);
    }

    private sealed class TestInquiryMessageProtector : IInquiryMessageProtector
    {
        public bool IsConfigured => true;
        public string Protect(string plaintext) => $"PROTECTED[{plaintext}]";
        public string Unprotect(string protectedMessage)
        {
            if (protectedMessage.StartsWith("PROTECTED[") && protectedMessage.EndsWith("]"))
            {
                return protectedMessage[10..^1];
            }
            return protectedMessage;
        }
    }

    private sealed class TestSystemSettingsService(int retentionYears = 3) : ISystemSettingsService
    {
        public Task<string?> GetSettingAsync(string key, CancellationToken ct = default)
        {
            if (key == SystemSettingKeys.InquiryRetentionYears)
            {
                return Task.FromResult<string?>(retentionYears.ToString());
            }
            return Task.FromResult<string?>(null);
        }
        public Task<bool> GetBoolSettingAsync(string key, bool defaultValue = false, CancellationToken ct = default) => Task.FromResult(defaultValue);
        public Task SetSettingAsync(string key, string value, string? userId = null, string? description = null, CancellationToken ct = default) => Task.CompletedTask;
        public Task<bool> IsGoogleAuthEnabledAsync(CancellationToken ct = default) => Task.FromResult(false);
        public Task SetGoogleAuthEnabledAsync(bool enabled, string? userId = null, CancellationToken ct = default) => Task.CompletedTask;
        public Task<bool> IsAiClassificationEnabledAsync(CancellationToken ct = default) => Task.FromResult(true);
        public Task SetAiClassificationEnabledAsync(bool enabled, string? userId = null, CancellationToken ct = default) => Task.CompletedTask;
        public Task<bool> IsPublicProEnabledAsync(CancellationToken ct = default) => Task.FromResult(true);
        public Task SetPublicProEnabledAsync(bool enabled, string? userId = null, CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class TestAuditLogger(IDbContextFactory<OMM.Admin.Data.ApplicationDbContext> adminDbFactory) : IAuditLogger
    {
        public async Task LogAsync(string actorUserId, string actorUserName, string action, string? targetUserId = null, string? detail = null)
        {
            await using var db = await adminDbFactory.CreateDbContextAsync();
            db.AdminAuditLogs.Add(new AdminAuditLog
            {
                ActorUserId = actorUserId,
                ActorUserName = actorUserName,
                Action = action,
                TargetUserId = targetUserId,
                Detail = detail,
                OccurredAt = DateTimeOffset.UtcNow
            });
            await db.SaveChangesAsync();
        }

        public async Task LogInquiryDataActionAsync(
            string actorUserId,
            string actorUserName,
            string action,
            IReadOnlyCollection<long> sessionIds,
            IReadOnlyCollection<long> turnIds,
            int recordCount)
        {
            await using var db = await adminDbFactory.CreateDbContextAsync();
            var rows = string.Join(',', sessionIds.Select(id => $"S:{id}").Concat(turnIds.Select(id => $"T:{id}")));
            db.AdminAuditLogs.Add(new AdminAuditLog
            {
                ActorUserId = actorUserId,
                ActorUserName = actorUserName,
                Action = action,
                Detail = $"Rows={rows}; Count={recordCount}; raw message text omitted.",
                OccurredAt = DateTimeOffset.UtcNow
            });
            await db.SaveChangesAsync();
        }

        public Task<IReadOnlyList<AdminAuditLog>> ListRecentAsync(string? searchTerm = null, int limit = 100) =>
            Task.FromResult<IReadOnlyList<AdminAuditLog>>(Array.Empty<AdminAuditLog>());
    }

    private static (
        IDbContextFactory<Public.Data.ApplicationDbContext> PublicFactory,
        IDbContextFactory<MasterDataDbContext> MasterDataFactory,
        IDbContextFactory<OMM.Admin.Data.ApplicationDbContext> AdminAppFactory,
        TestTimeProvider TimeProvider,
        TestInquiryMessageProtector MessageProtector)
        CreateTestHarness()
    {
        var dbName = Guid.NewGuid().ToString();
        var publicOptions = new DbContextOptionsBuilder<Public.Data.ApplicationDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;
        var masterDataOptions = new DbContextOptionsBuilder<MasterDataDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;
        var adminAppOptions = new DbContextOptionsBuilder<OMM.Admin.Data.ApplicationDbContext>()
            .UseInMemoryDatabase(dbName + "-admin")
            .Options;

        // Initialize databases
        using (var pubContext = new Public.Data.ApplicationDbContext(publicOptions))
        {
            pubContext.Database.EnsureCreated();
        }
        using (var adminContext = new OMM.Admin.Data.ApplicationDbContext(adminAppOptions))
        {
            adminContext.Database.EnsureCreated();
        }

        var services = new ServiceCollection();
        services.AddDbContextFactory<Public.Data.ApplicationDbContext>(opt => opt.UseInMemoryDatabase(dbName));
        services.AddDbContextFactory<MasterDataDbContext>(opt => opt.UseInMemoryDatabase(dbName));
        services.AddDbContextFactory<OMM.Admin.Data.ApplicationDbContext>(opt => opt.UseInMemoryDatabase(dbName + "-admin"));

        var provider = services.BuildServiceProvider();

        return (
            provider.GetRequiredService<IDbContextFactory<Public.Data.ApplicationDbContext>>(),
            provider.GetRequiredService<IDbContextFactory<MasterDataDbContext>>(),
            provider.GetRequiredService<IDbContextFactory<OMM.Admin.Data.ApplicationDbContext>>(),
            new TestTimeProvider(new DateTimeOffset(2026, 11, 1, 10, 0, 0, TimeSpan.Zero)),
            new TestInquiryMessageProtector());
    }

    [Fact]
    public async Task StartSessionAsync_ValidatesInput_AndCalculatesConfiguredRetention()
    {
        var (publicFactory, _, _, timeProvider, messageProtector) = CreateTestHarness();
        var settings = new TestSystemSettingsService(retentionYears: 3);
        var captureService = new InquiryCaptureService(
            publicFactory,
            settings,
            messageProtector,
            timeProvider,
            NullLogger<InquiryCaptureService>.Instance);

        // 1. Anonymous session with user ID must fail
        await Assert.ThrowsAsync<ArgumentException>(() => captureService.StartSessionAsync(
            new InquirySessionStartRequest("user-1", "DirectQuestion", "STANDARD", IsAnonymous: true, "v1")));

        // 2. Member session without user ID must fail
        await Assert.ThrowsAsync<ArgumentException>(() => captureService.StartSessionAsync(
            new InquirySessionStartRequest(null, "DirectQuestion", "STANDARD", IsAnonymous: false, "v1")));

        // 3. Valid member session
        var sessionId = await captureService.StartSessionAsync(
            new InquirySessionStartRequest("user-123", "DirectQuestion", "STANDARD", IsAnonymous: false, "v1"));

        Assert.True(sessionId > 0);

        await using var db = await publicFactory.CreateDbContextAsync();
        var session = await db.InquirySessions.FindAsync(sessionId);
        Assert.NotNull(session);
        Assert.Equal("user-123", session.UserId);
        Assert.Equal("DirectQuestion", session.EntryPoint);
        Assert.Equal("STANDARD", session.RequestedMode);
        Assert.False(session.IsAnonymous);
        Assert.Equal(timeProvider.GetUtcNow(), session.StartedAt);
        Assert.Equal(timeProvider.GetUtcNow().AddYears(3), session.ExpiresAt);
    }

    [Fact]
    public async Task PersistTurnAsync_ProtectsRawMessage_StoresTurn_AndEnforcesMemberAuthorization()
    {
        var (publicFactory, _, _, timeProvider, messageProtector) = CreateTestHarness();
        var settings = new TestSystemSettingsService(retentionYears: 3);
        var captureService = new InquiryCaptureService(
            publicFactory,
            settings,
            messageProtector,
            timeProvider,
            NullLogger<InquiryCaptureService>.Instance);

        var sessionId = await captureService.StartSessionAsync(
            new InquirySessionStartRequest("user-owner", "DirectQuestion", "STANDARD", IsAnonymous: false, "v1"));

        var turnData = new InquiryTurnCapture(
            DetectedIntent: "PortfolioImpact",
            EventType: "COMMODITY_MOVE",
            VerificationStatus: "Verified",
            Confidence: 0.95m,
            PromptVersionId: 1,
            UsedEmergencyPrompt: false,
            RequestedMode: "STANDARD",
            EffectiveMode: "STANDARD",
            FallbackReason: null,
            AiCallAttemptCount: 1);

        // Unauthorized member cannot persist turn to another member's session
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => captureService.PersistTurnAsync(
            sessionId,
            "another-user",
            1,
            turnData,
            "How does oil impact Tenaga?"));

        // Turn number must be positive
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => captureService.PersistTurnAsync(
            sessionId,
            "user-owner",
            0,
            turnData,
            "How does oil impact Tenaga?"));

        // Valid turn persistence
        var turnId = await captureService.PersistTurnAsync(
            sessionId,
            "user-owner",
            1,
            turnData,
            "How does oil price impact Tenaga Nasional?");

        Assert.True(turnId > 0);

        await using var db = await publicFactory.CreateDbContextAsync();
        var turn = await db.InquiryTurns.FindAsync(turnId);
        Assert.NotNull(turn);
        Assert.Equal(1, turn.TurnNumber);
        Assert.Equal("PortfolioImpact", turn.DetectedIntent);
        Assert.Equal("COMMODITY_MOVE", turn.EventType);
        Assert.NotNull(turn.PromptHash);

        var rawMessage = await db.InquiryRawMessages.FindAsync(turnId);
        Assert.NotNull(rawMessage);
        Assert.Equal("PROTECTED[How does oil price impact Tenaga Nasional?]", rawMessage.MessageText);
        Assert.Equal(timeProvider.GetUtcNow().AddYears(3), rawMessage.ExpiresAt);
    }

    [Fact]
    public async Task CompleteSessionAsync_RecordsCompletionTimestamp()
    {
        var (publicFactory, _, _, timeProvider, messageProtector) = CreateTestHarness();
        var settings = new TestSystemSettingsService();
        var captureService = new InquiryCaptureService(
            publicFactory,
            settings,
            messageProtector,
            timeProvider,
            NullLogger<InquiryCaptureService>.Instance);

        var sessionId = await captureService.StartSessionAsync(
            new InquirySessionStartRequest("user-1", "DirectQuestion", "STANDARD", false, "v1"));

        timeProvider.Advance(TimeSpan.FromMinutes(5));
        await captureService.CompleteSessionAsync(sessionId, "user-1");

        await using var db = await publicFactory.CreateDbContextAsync();
        var session = await db.InquirySessions.FindAsync(sessionId);
        Assert.NotNull(session);
        Assert.Equal(timeProvider.GetUtcNow(), session.CompletedAt);
    }

    [Fact]
    public async Task AdminService_GetSessionsAndGetSessionDetail_AuditsAccess_AndNeverLogsMessageText()
    {
        var (publicFactory, masterDataFactory, adminAppFactory, timeProvider, messageProtector) = CreateTestHarness();
        var settings = new TestSystemSettingsService();
        var captureService = new InquiryCaptureService(
            publicFactory,
            settings,
            messageProtector,
            timeProvider,
            NullLogger<InquiryCaptureService>.Instance);

        var auditLogger = new TestAuditLogger(adminAppFactory);
        var adminService = new InquiryAdminService(
            masterDataFactory,
            auditLogger,
            messageProtector,
            timeProvider,
            NullLogger<InquiryAdminService>.Instance);

        var sessionId = await captureService.StartSessionAsync(
            new InquirySessionStartRequest("user-1", "DirectQuestion", "PRO", false, "v1"));
        await captureService.PersistTurnAsync(
            sessionId,
            "user-1",
            1,
            new InquiryTurnCapture("Explain", "RATE_CHANGE", "Verified", 0.9m, 2, false, "PRO", "PRO", null, 1),
            "Confidential trade question with sensitive data.");

        // 1. GetSessionsAsync
        var sessions = await adminService.GetSessionsAsync("admin-99", "SuperAdmin");
        Assert.NotEmpty(sessions);
        Assert.Equal(sessionId, sessions[0].Id);
        Assert.Equal(1, sessions[0].TurnCount);
        Assert.Equal(1, sessions[0].RawMessageCount);

        // 2. GetSessionAsync detail
        var detail = await adminService.GetSessionAsync(sessionId, "admin-99", "SuperAdmin");
        Assert.NotNull(detail);
        Assert.Single(detail.Turns);
        Assert.Equal("Confidential trade question with sensitive data.", detail.Turns[0].MessageText);

        // 3. Verify audit log: reads are audited, but message text is omitted
        await using var adminDb = await adminAppFactory.CreateDbContextAsync();
        var logs = await adminDb.AdminAuditLogs.ToListAsync();
        Assert.NotEmpty(logs);
        Assert.All(logs, log =>
        {
            Assert.Contains("raw message text omitted", log.Detail);
            Assert.DoesNotContain("Confidential trade question", log.Detail);
        });
    }

    [Fact]
    public async Task AdminService_ExportSessionAsync_GeneratesValidJson_AndAuditsExport()
    {
        var (publicFactory, masterDataFactory, adminAppFactory, timeProvider, messageProtector) = CreateTestHarness();
        var settings = new TestSystemSettingsService();
        var captureService = new InquiryCaptureService(
            publicFactory,
            settings,
            messageProtector,
            timeProvider,
            NullLogger<InquiryCaptureService>.Instance);

        var auditLogger = new TestAuditLogger(adminAppFactory);
        var adminService = new InquiryAdminService(
            masterDataFactory,
            auditLogger,
            messageProtector,
            timeProvider,
            NullLogger<InquiryAdminService>.Instance);

        var sessionId = await captureService.StartSessionAsync(
            new InquirySessionStartRequest("user-1", "DirectQuestion", "STANDARD", false, "v1"));
        await captureService.PersistTurnAsync(
            sessionId,
            "user-1",
            1,
            new InquiryTurnCapture("Explain", "COMMODITY_MOVE", "Verified", 0.9m, 1, false, "STANDARD", "STANDARD", null, 1),
            "What is the outlook for palm oil?");

        var exportBytes = await adminService.ExportSessionAsync(sessionId, "admin-1", "Admin");
        Assert.NotNull(exportBytes);

        using var jsonDoc = JsonDocument.Parse(exportBytes);
        var root = jsonDoc.RootElement;
        Assert.Equal(sessionId, root.GetProperty("Id").GetInt64());
        Assert.Equal("user-1", root.GetProperty("UserId").GetString());
        Assert.Equal(1, root.GetProperty("Turns").GetArrayLength());

        await using var adminDb = await adminAppFactory.CreateDbContextAsync();
        var exportLog = await adminDb.AdminAuditLogs.FirstOrDefaultAsync(l => l.Action == "InquiryExport");
        Assert.NotNull(exportLog);
        Assert.DoesNotContain("palm oil", exportLog.Detail);
    }

    [Fact]
    public async Task AdminService_DeleteRawMessagesAsync_DeletesMessageText_AndPreservesTurnAnalytics()
    {
        var (publicFactory, masterDataFactory, adminAppFactory, timeProvider, messageProtector) = CreateTestHarness();
        var settings = new TestSystemSettingsService();
        var captureService = new InquiryCaptureService(
            publicFactory,
            settings,
            messageProtector,
            timeProvider,
            NullLogger<InquiryCaptureService>.Instance);

        var auditLogger = new TestAuditLogger(adminAppFactory);
        var adminService = new InquiryAdminService(
            masterDataFactory,
            auditLogger,
            messageProtector,
            timeProvider,
            NullLogger<InquiryAdminService>.Instance);

        var sessionId = await captureService.StartSessionAsync(
            new InquirySessionStartRequest("user-1", "DirectQuestion", "STANDARD", false, "v1"));
        var turnId = await captureService.PersistTurnAsync(
            sessionId,
            "user-1",
            1,
            new InquiryTurnCapture("Explain", "COMMODITY_MOVE", "Verified", 0.9m, 1, false, "STANDARD", "STANDARD", null, 1),
            "Delete my sensitive message text please.");

        // Delete raw message
        var deletedCount = await adminService.DeleteRawMessagesAsync([turnId], "admin-1", "Admin");
        Assert.Equal(1, deletedCount);

        // Turn should still exist with all metadata
        await using var db = await masterDataFactory.CreateDbContextAsync();
        var turn = await db.InquiryTurns.FindAsync(turnId);
        Assert.NotNull(turn);
        Assert.Equal("Explain", turn.DetectedIntent);
        Assert.Equal("COMMODITY_MOVE", turn.EventType);

        // Raw message text should be gone
        var rawMessage = await db.InquiryRawMessages.FindAsync(turnId);
        Assert.Null(rawMessage);

        // Detail view shows turn with null MessageText
        var sessionDetail = await adminService.GetSessionAsync(sessionId, "admin-1", "Admin");
        Assert.NotNull(sessionDetail);
        Assert.Single(sessionDetail.Turns);
        Assert.Null(sessionDetail.Turns[0].MessageText);

        // Audit check
        await using var adminDb = await adminAppFactory.CreateDbContextAsync();
        var deleteLog = await adminDb.AdminAuditLogs.FirstOrDefaultAsync(l => l.Action == "InquiryRawMessageDelete");
        Assert.NotNull(deleteLog);
        Assert.DoesNotContain("sensitive message", deleteLog.Detail);
    }

    [Fact]
    public async Task AdminService_DeleteSessionAsync_DeletesEntireSession_Turns_AndMessages()
    {
        var (publicFactory, masterDataFactory, adminAppFactory, timeProvider, messageProtector) = CreateTestHarness();
        var settings = new TestSystemSettingsService();
        var captureService = new InquiryCaptureService(
            publicFactory,
            settings,
            messageProtector,
            timeProvider,
            NullLogger<InquiryCaptureService>.Instance);

        var auditLogger = new TestAuditLogger(adminAppFactory);
        var adminService = new InquiryAdminService(
            masterDataFactory,
            auditLogger,
            messageProtector,
            timeProvider,
            NullLogger<InquiryAdminService>.Instance);

        var sessionId = await captureService.StartSessionAsync(
            new InquirySessionStartRequest("user-1", "DirectQuestion", "STANDARD", false, "v1"));
        var turnId = await captureService.PersistTurnAsync(
            sessionId,
            "user-1",
            1,
            new InquiryTurnCapture("Explain", "COMMODITY_MOVE", "Verified", 0.9m, 1, false, "STANDARD", "STANDARD", null, 1),
            "Message to be deleted.");

        var deleted = await adminService.DeleteSessionAsync(sessionId, "admin-1", "Admin");
        Assert.True(deleted);

        await using var db = await masterDataFactory.CreateDbContextAsync();
        Assert.Null(await db.InquirySessions.FindAsync(sessionId));
        Assert.Null(await db.InquiryTurns.FindAsync(turnId));
        Assert.Null(await db.InquiryRawMessages.FindAsync(turnId));
    }

    [Fact]
    public async Task AdminService_CleanupExpiredAsync_DeletesOnlyExpiredRecords()
    {
        var (publicFactory, masterDataFactory, adminAppFactory, timeProvider, messageProtector) = CreateTestHarness();
        var settings = new TestSystemSettingsService(retentionYears: 1);
        var captureService = new InquiryCaptureService(
            publicFactory,
            settings,
            messageProtector,
            timeProvider,
            NullLogger<InquiryCaptureService>.Instance);

        var auditLogger = new TestAuditLogger(adminAppFactory);
        var adminService = new InquiryAdminService(
            masterDataFactory,
            auditLogger,
            messageProtector,
            timeProvider,
            NullLogger<InquiryAdminService>.Instance);

        // Session 1: created at t0, expires at t0 + 1 year
        var session1Id = await captureService.StartSessionAsync(
            new InquirySessionStartRequest("user-1", "DirectQuestion", "STANDARD", false, "v1"));
        await captureService.PersistTurnAsync(
            session1Id, "user-1", 1,
            new InquiryTurnCapture("Explain", "COMMODITY_MOVE", "Verified", 0.9m, 1, false, "STANDARD", "STANDARD", null, 1),
            "Old message to be expired.");

        // Advance 2 years: session 1 is now expired
        timeProvider.Advance(TimeSpan.FromDays(730));

        // Session 2: created at t0 + 2 years, expires at t0 + 3 years
        var session2Id = await captureService.StartSessionAsync(
            new InquirySessionStartRequest("user-2", "DirectQuestion", "STANDARD", false, "v1"));
        var session2TurnId = await captureService.PersistTurnAsync(
            session2Id, "user-2", 1,
            new InquiryTurnCapture("Explain", "COMMODITY_MOVE", "Verified", 0.9m, 1, false, "STANDARD", "STANDARD", null, 1),
            "Active new message.");

        // Run cleanup
        var (rawDeleted, sessionsDeleted) = await adminService.CleanupExpiredAsync();

        Assert.Equal(1, rawDeleted);
        Assert.Equal(1, sessionsDeleted);

        await using var db = await masterDataFactory.CreateDbContextAsync();
        Assert.Null(await db.InquirySessions.FindAsync(session1Id));
        Assert.NotNull(await db.InquirySessions.FindAsync(session2Id));
        Assert.NotNull(await db.InquiryTurns.FindAsync(session2TurnId));
        Assert.NotNull(await db.InquiryRawMessages.FindAsync(session2TurnId));
    }
}

