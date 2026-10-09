using Microsoft.EntityFrameworkCore;
using OMM.Public.Data;
using OMM.Shared.DecisionSupport;

namespace OMM.Public.Services;

public sealed class EfDecisionSignalCatalog(IDbContextFactory<ApplicationDbContext> dbContextFactory) : IDecisionSignalCatalog
{
    public async Task<IReadOnlyList<SignalTemplate>> GetActiveSignalsAsync(CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await dbContext.DecisionSignalTemplates
            .AsNoTracking()
            .Where(signal => signal.IsActive)
            .OrderBy(signal => signal.SortOrder)
            .ThenBy(signal => signal.Title)
            .ToListAsync(cancellationToken);
    }
}