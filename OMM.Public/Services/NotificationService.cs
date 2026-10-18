using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.EntityFrameworkCore;
using OMM.Public.Data;
using OMM.Public.Data.Entities;
using OMM.Public.Models;
using OMM.Shared.DecisionSupport;

namespace OMM.Public.Services;

public sealed class NotificationService(
    IDbContextFactory<ApplicationDbContext> dbContextFactory,
    AuthenticationStateProvider authenticationStateProvider) : INotificationService
{
    public async Task<IReadOnlyList<Notification>> GetAsync(CancellationToken cancellationToken = default)
    {
        var userId = await RequireUserIdAsync();
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await db.Notifications.AsNoTracking()
            .Where(item => item.UserId == userId && !item.IsDeleted && item.DismissedAt == null)
            .OrderByDescending(item => item.OccurredOn).ThenByDescending(item => item.CreatedAt)
            .Select(item => new Notification
            {
                Id = item.Id.ToString(), Title = item.Title, Body = item.Body, Type = item.Type,
                Date = item.OccurredOn.ToString("yyyy-MM-dd"), Read = item.ReadAt != null
            })
            .ToListAsync(cancellationToken);
    }

    public async Task GenerateAsync(CancellationToken cancellationToken = default)
    {
        var userId = await RequireUserIdAsync();
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        var profile = await db.MinerProfiles.AsNoTracking()
            .FirstOrDefaultAsync(item => item.UserId == userId, cancellationToken);
        var notifyFd = profile?.NotifyFdMaturity ?? true;
        var notifyDecision = profile?.NotifyDecisionReview ?? true;

        var existingKeys = await db.Notifications.Where(item => item.UserId == userId).Select(item => item.DedupeKey).ToHashSetAsync(cancellationToken);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        if (notifyFd)
        {
            var cutoff = today.AddDays(30);
            var positions = await db.MinePositions.AsNoTracking()
                .Where(item => item.UserId == userId && !item.IsDeleted && item.MaturityDate != null && item.MaturityDate >= today && item.MaturityDate <= cutoff)
                .Select(item => new { item.Id, item.Label, item.MaturityDate })
                .ToListAsync(cancellationToken);

            foreach (var position in positions)
            {
                var key = $"maturity:{position.Id}:{position.MaturityDate:yyyy-MM-dd}";
                if (!existingKeys.Add(key)) continue;
                db.Notifications.Add(new NotificationEntity
                {
                    UserId = userId, Title = "Position maturity coming up", Body = $"{position.Label} matures on {position.MaturityDate:dd MMM yyyy}.",
                    Type = "maturity", OccurredOn = position.MaturityDate!.Value, DedupeKey = key, CreatedAt = DateTimeOffset.UtcNow
                });
            }
        }

        if (notifyDecision)
        {
            var decisionCutoff = today.AddDays(7);
            var pathways = await db.DecisionPathways.AsNoTracking()
                .Where(item => item.UserId == userId && item.Status == DecisionPathwayStatus.Open && item.ReviewDate <= decisionCutoff)
                .Select(item => new { item.Id, item.SignalTitle, item.StockCode, item.ReviewDate })
                .ToListAsync(cancellationToken);

            foreach (var pathway in pathways)
            {
                var key = $"decision-review:{pathway.Id}:{pathway.ReviewDate:yyyy-MM-dd}";
                if (!existingKeys.Add(key)) continue;

                var stockPrefix = !string.IsNullOrWhiteSpace(pathway.StockCode) ? $"[{pathway.StockCode}] " : string.Empty;
                var isOverdue = pathway.ReviewDate < today;
                var title = isOverdue ? "Decision review overdue" : "Decision review due soon";
                var body = $"{stockPrefix}{pathway.SignalTitle} review is due on {pathway.ReviewDate:dd MMM yyyy}.";

                db.Notifications.Add(new NotificationEntity
                {
                    UserId = userId,
                    Title = title,
                    Body = body,
                    Type = "decision",
                    OccurredOn = pathway.ReviewDate,
                    DedupeKey = key,
                    CreatedAt = DateTimeOffset.UtcNow
                });
            }
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    public Task<bool> MarkReadAsync(string id, CancellationToken cancellationToken = default) => UpdateAsync(id, item => item.ReadAt ??= DateTimeOffset.UtcNow, cancellationToken);

    public Task<bool> DismissAsync(string id, CancellationToken cancellationToken = default) => UpdateAsync(id, item => item.DismissedAt ??= DateTimeOffset.UtcNow, cancellationToken);

    private async Task<bool> UpdateAsync(string id, Action<NotificationEntity> update, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(id, out var notificationId)) return false;
        var userId = await RequireUserIdAsync();
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var item = await db.Notifications.SingleOrDefaultAsync(notification => notification.Id == notificationId && notification.UserId == userId, cancellationToken);
        if (item is null) return false;
        update(item);
        item.ModifiedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    private async Task<string> RequireUserIdAsync()
    {
        var state = await authenticationStateProvider.GetAuthenticationStateAsync();
        return state.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? state.User.FindFirstValue("sub")
            ?? throw new InvalidOperationException("An authenticated Public user is required.");
    }
}
