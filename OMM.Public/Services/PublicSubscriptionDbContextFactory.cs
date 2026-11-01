using Microsoft.EntityFrameworkCore;
using OMM.Public.Data;
using OMM.Shared.Subscription;

namespace OMM.Public.Services;

public sealed class PublicSubscriptionDbContextFactory(IDbContextFactory<ApplicationDbContext> dbContextFactory) : ISubscriptionDbContextFactory
{
    public async Task<ISubscriptionDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
    {
        var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        return db;
    }
}

