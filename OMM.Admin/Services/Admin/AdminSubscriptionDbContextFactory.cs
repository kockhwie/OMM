using Microsoft.EntityFrameworkCore;
using OMM.Admin.Data;
using OMM.Shared.Subscription;

namespace OMM.Admin.Services.Admin;

public sealed class AdminSubscriptionDbContextFactory(IDbContextFactory<MasterDataDbContext> dbContextFactory) : ISubscriptionDbContextFactory
{
    public async Task<ISubscriptionDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
    {
        var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        return db;
    }
}

