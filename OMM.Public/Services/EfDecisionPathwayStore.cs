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

    public async Task<DecisionPathwayPage> GetPageAsync(string userId, int page, int pageSize, CancellationToken ct)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(ct);
        var pathways = dbContext.DecisionPathways
            .AsNoTracking()
            .Where(record => record.UserId == userId);
        var totalCount = await pathways.CountAsync(ct);
        var items = await pathways
            .OrderByDescending(record => record.CreatedAt)
            .ThenByDescending(record => record.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return new DecisionPathwayPage(items, page, pageSize, totalCount);
    }
}
