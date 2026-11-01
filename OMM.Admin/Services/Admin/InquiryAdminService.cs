using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using OMM.Admin.Data;
using OMM.Shared.Inquiry;
using OMM.Shared.Models.Phase5;

namespace OMM.Admin.Services.Admin;

public sealed record InquirySessionListItem(
    long Id,
    string? UserId,
    string EntryPoint,
    string RequestedMode,
    bool IsAnonymous,
    string FlowVersion,
    DateTimeOffset StartedAt,
    DateTimeOffset? CompletedAt,
    DateTimeOffset ExpiresAt,
    int TurnCount,
    int RawMessageCount);

public sealed record InquiryTurnAdminRecord(
    long Id,
    int TurnNumber,
    string? PromptHash,
    string? DetectedIntent,
    string? EventType,
    string? VerificationStatus,
    decimal? Confidence,
    int? PromptVersionId,
    bool UsedEmergencyPrompt,
    string RequestedMode,
    string EffectiveMode,
    string? FallbackReason,
    int AiCallAttemptCount,
    DateTimeOffset CreatedAt,
    string? MessageText,
    DateTimeOffset? MessageExpiresAt);

public sealed record InquirySessionAdminDetail(
    long Id,
    string? UserId,
    string EntryPoint,
    string RequestedMode,
    bool IsAnonymous,
    string FlowVersion,
    DateTimeOffset StartedAt,
    DateTimeOffset? CompletedAt,
    DateTimeOffset ExpiresAt,
    IReadOnlyList<InquiryTurnAdminRecord> Turns);

public interface IInquiryAdminService
{
    Task<IReadOnlyList<InquirySessionListItem>> GetSessionsAsync(string actorId, string actorName, int take = 50, CancellationToken cancellationToken = default);
    Task<InquirySessionAdminDetail?> GetSessionAsync(long sessionId, string actorId, string actorName, CancellationToken cancellationToken = default);
    Task<byte[]?> ExportSessionAsync(long sessionId, string actorId, string actorName, CancellationToken cancellationToken = default);
    Task<int> DeleteRawMessagesAsync(IReadOnlyCollection<long> turnIds, string actorId, string actorName, CancellationToken cancellationToken = default);
    Task<bool> DeleteSessionAsync(long sessionId, string actorId, string actorName, CancellationToken cancellationToken = default);
    Task<(int RawMessagesDeleted, int SessionsDeleted)> CleanupExpiredAsync(CancellationToken cancellationToken = default);
}

public sealed class InquiryAdminService(
    IDbContextFactory<MasterDataDbContext> dbContextFactory,
    IAuditLogger auditLogger,
    IInquiryMessageProtector messageProtector,
    TimeProvider timeProvider,
    ILogger<InquiryAdminService> logger) : IInquiryAdminService
{
    private const int MaximumDeleteBatch = 2_000;

    public async Task<IReadOnlyList<InquirySessionListItem>> GetSessionsAsync(
        string actorId,
        string actorName,
        int take = 50,
        CancellationToken cancellationToken = default)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var ids = await db.InquirySessions.AsNoTracking()
            .OrderByDescending(x => x.StartedAt)
            .ThenByDescending(x => x.Id)
            .Select(x => x.Id)
            .Take(Math.Clamp(take, 1, 100))
            .ToArrayAsync(cancellationToken);

        await auditLogger.LogInquiryDataActionAsync(actorId, actorName, "InquiryRead", ids, Array.Empty<long>(), ids.Length);
        if (ids.Length == 0)
        {
            return Array.Empty<InquirySessionListItem>();
        }

        var sessions = await db.InquirySessions.AsNoTracking()
            .Where(x => ids.Contains(x.Id))
            .Select(x => new
            {
                x.Id,
                x.UserId,
                x.EntryPoint,
                x.RequestedMode,
                x.IsAnonymous,
                x.FlowVersion,
                x.StartedAt,
                x.CompletedAt,
                x.ExpiresAt
            })
            .ToListAsync(cancellationToken);
        var turnCounts = await db.InquiryTurns.AsNoTracking()
            .Where(x => ids.Contains(x.InquirySessionId))
            .GroupBy(x => x.InquirySessionId)
            .Select(group => new { SessionId = group.Key, Count = group.Count() })
            .ToDictionaryAsync(x => x.SessionId, x => x.Count, cancellationToken);
        var rawMessageCounts = await (from raw in db.InquiryRawMessages.AsNoTracking()
                                      join turn in db.InquiryTurns.AsNoTracking() on raw.InquiryTurnId equals turn.Id
                                      where ids.Contains(turn.InquirySessionId)
                                      group raw by turn.InquirySessionId into groupedRows
                                      select new { SessionId = groupedRows.Key, Count = groupedRows.Count() })
            .ToDictionaryAsync(x => x.SessionId, x => x.Count, cancellationToken);

        return sessions
            .OrderByDescending(x => x.StartedAt)
            .ThenByDescending(x => x.Id)
            .Select(x => new InquirySessionListItem(
                x.Id,
                x.UserId,
                x.EntryPoint,
                x.RequestedMode,
                x.IsAnonymous,
                x.FlowVersion,
                x.StartedAt,
                x.CompletedAt,
                x.ExpiresAt,
                turnCounts.GetValueOrDefault(x.Id),
                rawMessageCounts.GetValueOrDefault(x.Id)))
            .ToArray();
    }

    public async Task<InquirySessionAdminDetail?> GetSessionAsync(
        long sessionId,
        string actorId,
        string actorName,
        CancellationToken cancellationToken = default)
    {
        return await LoadSessionAsync(sessionId, "InquiryRead", actorId, actorName, cancellationToken);
    }

    public async Task<byte[]?> ExportSessionAsync(
        long sessionId,
        string actorId,
        string actorName,
        CancellationToken cancellationToken = default)
    {
        var detail = await LoadSessionAsync(sessionId, "InquiryExport", actorId, actorName, cancellationToken);
        return detail is null
            ? null
            : JsonSerializer.SerializeToUtf8Bytes(detail, new JsonSerializerOptions { WriteIndented = true });
    }

    public async Task<int> DeleteRawMessagesAsync(
        IReadOnlyCollection<long> turnIds,
        string actorId,
        string actorName,
        CancellationToken cancellationToken = default)
    {
        var selectedIds = turnIds.Distinct().ToArray();
        if (selectedIds.Length == 0)
        {
            return 0;
        }
        if (selectedIds.Length > MaximumDeleteBatch)
        {
            throw new ArgumentOutOfRangeException(nameof(turnIds), $"A maximum of {MaximumDeleteBatch} raw messages can be deleted at once.");
        }

        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var existingIds = await db.InquiryRawMessages.AsNoTracking()
            .Where(x => selectedIds.Contains(x.InquiryTurnId))
            .Select(x => x.InquiryTurnId)
            .ToArrayAsync(cancellationToken);
        var sessionIds = await db.InquiryTurns.AsNoTracking()
            .Where(x => existingIds.Contains(x.Id))
            .Select(x => x.InquirySessionId)
            .Distinct()
            .ToArrayAsync(cancellationToken);

        await auditLogger.LogInquiryDataActionAsync(
            actorId,
            actorName,
            "InquiryRawMessageDelete",
            sessionIds,
            existingIds,
            existingIds.Length);

        int deleted;
        if (db.Database.IsRelational())
        {
            deleted = await db.InquiryRawMessages
                .Where(x => existingIds.Contains(x.InquiryTurnId))
                .ExecuteDeleteAsync(cancellationToken);
        }
        else
        {
            var messages = await db.InquiryRawMessages
                .Where(x => existingIds.Contains(x.InquiryTurnId))
                .ToListAsync(cancellationToken);
            db.InquiryRawMessages.RemoveRange(messages);
            deleted = await db.SaveChangesAsync(cancellationToken);
        }
        logger.LogInformation("Admin {ActorId} deleted {RawMessageCount} inquiry raw messages; message text was not logged.", actorId, deleted);
        return deleted;
    }

    public async Task<bool> DeleteSessionAsync(
        long sessionId,
        string actorId,
        string actorName,
        CancellationToken cancellationToken = default)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var turnIds = await db.InquiryTurns.AsNoTracking()
            .Where(x => x.InquirySessionId == sessionId)
            .Select(x => x.Id)
            .ToArrayAsync(cancellationToken);
        var exists = await db.InquirySessions.AsNoTracking().AnyAsync(x => x.Id == sessionId, cancellationToken);
        await auditLogger.LogInquiryDataActionAsync(
            actorId,
            actorName,
            "InquirySessionDelete",
            exists ? [sessionId] : Array.Empty<long>(),
            turnIds,
            exists ? 1 + turnIds.Length : 0);
        if (!exists)
        {
            return false;
        }

        if (db.Database.IsRelational())
        {
            var deleted = await db.InquirySessions.Where(x => x.Id == sessionId).ExecuteDeleteAsync(cancellationToken);
            logger.LogInformation("Admin {ActorId} deleted inquiry session {InquirySessionId} with {TurnCount} turns; message text was not logged.", actorId, sessionId, turnIds.Length);
            return deleted > 0;
        }
        else
        {
            var session = await db.InquirySessions.SingleOrDefaultAsync(x => x.Id == sessionId, cancellationToken);
            if (session is null)
            {
                return false;
            }
            var turns = await db.InquiryTurns.Where(x => x.InquirySessionId == sessionId).ToListAsync(cancellationToken);
            var tIds = turns.Select(x => x.Id).ToList();
            var rawMsgs = await db.InquiryRawMessages.Where(x => tIds.Contains(x.InquiryTurnId)).ToListAsync(cancellationToken);
            db.InquiryRawMessages.RemoveRange(rawMsgs);
            db.InquiryTurns.RemoveRange(turns);
            db.InquirySessions.Remove(session);
            await db.SaveChangesAsync(cancellationToken);
            logger.LogInformation("Admin {ActorId} deleted inquiry session {InquirySessionId} with {TurnCount} turns; message text was not logged.", actorId, sessionId, turnIds.Length);
            return true;
        }
    }

    public async Task<(int RawMessagesDeleted, int SessionsDeleted)> CleanupExpiredAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var now = timeProvider.GetUtcNow();
        int rawMessagesDeleted;
        int sessionsDeleted;

        if (db.Database.IsRelational())
        {
            rawMessagesDeleted = await db.InquiryRawMessages
                .Where(x => x.ExpiresAt <= now)
                .ExecuteDeleteAsync(cancellationToken);
            sessionsDeleted = await db.InquirySessions
                .Where(x => x.ExpiresAt <= now)
                .ExecuteDeleteAsync(cancellationToken);
        }
        else
        {
            var expiredMessages = await db.InquiryRawMessages.Where(x => x.ExpiresAt <= now).ToListAsync(cancellationToken);
            rawMessagesDeleted = expiredMessages.Count;
            db.InquiryRawMessages.RemoveRange(expiredMessages);

            var expiredSessions = await db.InquirySessions.Where(x => x.ExpiresAt <= now).ToListAsync(cancellationToken);
            sessionsDeleted = expiredSessions.Count;
            foreach (var s in expiredSessions)
            {
                var turns = await db.InquiryTurns.Where(x => x.InquirySessionId == s.Id).ToListAsync(cancellationToken);
                var tIds = turns.Select(x => x.Id).ToList();
                var msgs = await db.InquiryRawMessages.Where(x => tIds.Contains(x.InquiryTurnId)).ToListAsync(cancellationToken);
                db.InquiryRawMessages.RemoveRange(msgs);
                db.InquiryTurns.RemoveRange(turns);
            }
            db.InquirySessions.RemoveRange(expiredSessions);
            await db.SaveChangesAsync(cancellationToken);
        }

        if (rawMessagesDeleted > 0 || sessionsDeleted > 0)
        {
            logger.LogInformation("Inquiry retention cleanup deleted {RawMessageCount} expired raw messages and {SessionCount} expired sessions.", rawMessagesDeleted, sessionsDeleted);
        }
        return (rawMessagesDeleted, sessionsDeleted);
    }

    private async Task<InquirySessionAdminDetail?> LoadSessionAsync(
        long sessionId,
        string action,
        string actorId,
        string actorName,
        CancellationToken cancellationToken)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var turnIds = await db.InquiryTurns.AsNoTracking()
            .Where(x => x.InquirySessionId == sessionId)
            .Select(x => x.Id)
            .ToArrayAsync(cancellationToken);
        var exists = await db.InquirySessions.AsNoTracking().AnyAsync(x => x.Id == sessionId, cancellationToken);
        await auditLogger.LogInquiryDataActionAsync(
            actorId,
            actorName,
            action,
            exists ? [sessionId] : Array.Empty<long>(),
            turnIds,
            exists ? 1 + turnIds.Length : 0);
        if (!exists)
        {
            return null;
        }

        var session = await db.InquirySessions.AsNoTracking()
            .Where(x => x.Id == sessionId)
            .Select(x => new
            {
                x.Id,
                x.UserId,
                x.EntryPoint,
                x.RequestedMode,
                x.IsAnonymous,
                x.FlowVersion,
                x.StartedAt,
                x.CompletedAt,
                x.ExpiresAt
            })
            .SingleAsync(cancellationToken);
        var turns = await db.InquiryTurns.AsNoTracking()
            .Where(x => x.InquirySessionId == sessionId)
            .OrderBy(x => x.TurnNumber)
            .Select(x => new
            {
                x.Id,
                x.TurnNumber,
                x.PromptHash,
                x.DetectedIntent,
                x.EventType,
                x.VerificationStatus,
                x.Confidence,
                x.PromptVersionId,
                x.UsedEmergencyPrompt,
                x.RequestedMode,
                x.EffectiveMode,
                x.FallbackReason,
                x.AiCallAttemptCount,
                x.CreatedAt
            })
            .ToListAsync(cancellationToken);
        var rawMessages = turnIds.Length == 0
            ? new Dictionary<long, InquiryRawMessage>()
            : await db.InquiryRawMessages.AsNoTracking()
                .Where(x => turnIds.Contains(x.InquiryTurnId))
                .ToDictionaryAsync(x => x.InquiryTurnId, cancellationToken);

        var adminTurns = turns.Select(turn => new InquiryTurnAdminRecord(
            turn.Id,
            turn.TurnNumber,
            turn.PromptHash,
            turn.DetectedIntent,
            turn.EventType,
            turn.VerificationStatus,
            turn.Confidence,
            turn.PromptVersionId,
            turn.UsedEmergencyPrompt,
            turn.RequestedMode,
            turn.EffectiveMode,
            turn.FallbackReason,
            turn.AiCallAttemptCount,
            turn.CreatedAt,
            rawMessages.TryGetValue(turn.Id, out var raw) ? messageProtector.Unprotect(raw.MessageText) : null,
            raw?.ExpiresAt)).ToArray();

        return new InquirySessionAdminDetail(
            session.Id,
            session.UserId,
            session.EntryPoint,
            session.RequestedMode,
            session.IsAnonymous,
            session.FlowVersion,
            session.StartedAt,
            session.CompletedAt,
            session.ExpiresAt,
            adminTurns);
    }
}
