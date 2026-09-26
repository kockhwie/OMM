using Microsoft.EntityFrameworkCore;
using OMM.Public.Data.Entities;

namespace OMM.Public.Data;

public sealed class PublicMemberMigrationService(
    IDbContextFactory<ApplicationDbContext> dbContextFactory,
    ILogger<PublicMemberMigrationService> logger)
{
    public async Task<int> EnsureMinerProfilesAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        var users = await db.Users
            .AsNoTracking()
            .Where(user => user.Email != null)
            .Select(user => new { user.Id, user.Email, user.UserName })
            .ToListAsync(cancellationToken);

        var existingUserIds = await db.MinerProfiles
            .IgnoreQueryFilters()
            .Select(profile => profile.UserId)
            .ToHashSetAsync(cancellationToken);

        var now = DateTimeOffset.UtcNow;
        var missingProfiles = users
            .Where(user => !existingUserIds.Contains(user.Id))
            .Select(user => new MinerProfileEntity
            {
                UserId = user.Id,
                Email = user.Email ?? user.UserName ?? string.Empty,
                CreatedAt = now,
                IsDeleted = false
            })
            .ToList();

        if (missingProfiles.Count == 0)
        {
            return 0;
        }

        db.MinerProfiles.AddRange(missingProfiles);
        await db.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Created {ProfileCount} missing Public miner profiles for existing Identity users.",
            missingProfiles.Count);
        return missingProfiles.Count;
    }
}
