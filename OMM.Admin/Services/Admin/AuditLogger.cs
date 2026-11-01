using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OMM.Admin.Data;

namespace OMM.Admin.Services.Admin;

public class AuditLogger : IAuditLogger
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<AuditLogger> _logger;

    public AuditLogger(IServiceScopeFactory scopeFactory, ILogger<AuditLogger> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task LogAsync(string actorUserId, string actorUserName, string action, string? targetUserId = null, string? detail = null)
    {
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var logEntry = new AdminAuditLog
            {
                ActorUserId = string.IsNullOrWhiteSpace(actorUserId) ? "System" : actorUserId,
                ActorUserName = string.IsNullOrWhiteSpace(actorUserName) ? "System" : actorUserName,
                Action = action,
                TargetUserId = targetUserId,
                Detail = detail,
                OccurredAt = DateTimeOffset.UtcNow
            };

            db.AdminAuditLogs.Add(logEntry);
            await db.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            // Logging audit failures should never crash the main user action, but must be logged
            _logger.LogError(ex, "Failed to persist audit log entry for Action '{Action}' by Actor '{ActorUserName}' ({ActorUserId})",
                action, actorUserName, actorUserId);
        }
    }

    public async Task LogInquiryDataActionAsync(
        string actorUserId,
        string actorUserName,
        string action,
        IReadOnlyCollection<long> sessionIds,
        IReadOnlyCollection<long> turnIds,
        int recordCount)
    {
        var rowReferences = sessionIds.Select(id => $"S:{id}")
            .Concat(turnIds.Select(id => $"T:{id}"))
            .ToArray();
        var chunks = rowReferences.Chunk(12).ToArray();
        if (chunks.Length == 0)
        {
            chunks = [Array.Empty<string>()];
        }

        await using var scope = _scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        foreach (var chunk in chunks)
        {
            db.AdminAuditLogs.Add(new AdminAuditLog
            {
                ActorUserId = string.IsNullOrWhiteSpace(actorUserId) ? "System" : actorUserId,
                ActorUserName = string.IsNullOrWhiteSpace(actorUserName) ? "System" : actorUserName,
                Action = action,
                Detail = $"Rows={string.Join(',', chunk)}; Count={recordCount}; raw message text omitted.",
                OccurredAt = DateTimeOffset.UtcNow
            });
        }

        await db.SaveChangesAsync();
    }

    public async Task<IReadOnlyList<AdminAuditLog>> ListRecentAsync(string? searchTerm = null, int limit = 100)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var query = db.AdminAuditLogs.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var term = searchTerm.Trim();
            query = query.Where(x =>
                EF.Functions.ILike(x.ActorUserName, $"%{term}%") ||
                EF.Functions.ILike(x.Action, $"%{term}%") ||
                (x.Detail != null && EF.Functions.ILike(x.Detail, $"%{term}%")) ||
                (x.TargetUserId != null && EF.Functions.ILike(x.TargetUserId, $"%{term}%")));
        }

        return await query
            .OrderByDescending(x => x.OccurredAt)
            .Take(limit)
            .ToListAsync();
    }
}
