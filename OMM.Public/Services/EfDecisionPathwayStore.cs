using Microsoft.EntityFrameworkCore;
using OMM.Public.Data;
using OMM.Shared.DecisionSupport;

namespace OMM.Public.Services;

public sealed class EfDecisionPathwayStore(IDbContextFactory<ApplicationDbContext> dbContextFactory) : IDecisionPathwayStore
{
    public async Task SaveAsync(DecisionPathwayRecord record, CancellationToken ct)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(ct);
        dbContext.DecisionPathways.Add(record);
        await dbContext.SaveChangesAsync(ct);
    }

    public async Task<DecisionPathwayPage> GetPageAsync(string userId, int page, int pageSize, string? statusFilter = null, CancellationToken ct = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(ct);
        var pathways = dbContext.DecisionPathways
            .AsNoTracking()
            .Where(record => record.UserId == userId);

        if (!string.IsNullOrWhiteSpace(statusFilter) && !string.Equals(statusFilter, "All", StringComparison.OrdinalIgnoreCase))
        {
            pathways = pathways.Where(record => record.Status == statusFilter);
        }

        var totalCount = await pathways.CountAsync(ct);
        var items = await pathways
            .OrderByDescending(record => record.CreatedAt)
            .ThenByDescending(record => record.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return new DecisionPathwayPage(items, page, pageSize, totalCount);
    }

    public async Task<bool> UpdateStatusAsync(Guid id, string userId, string newStatus, CancellationToken ct = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(ct);
        var record = await dbContext.DecisionPathways
            .FirstOrDefaultAsync(r => r.Id == id && r.UserId == userId, ct);

        if (record is null)
        {
            return false;
        }

        record.Status = newStatus;
        if (string.Equals(newStatus, DecisionPathwayStatus.Reviewed, StringComparison.OrdinalIgnoreCase))
        {
            record.ReviewedAt = DateTimeOffset.UtcNow;
        }
        else if (string.Equals(newStatus, DecisionPathwayStatus.Open, StringComparison.OrdinalIgnoreCase))
        {
            record.ReviewedAt = null;
        }

        await dbContext.SaveChangesAsync(ct);
        return true;
    }
}
