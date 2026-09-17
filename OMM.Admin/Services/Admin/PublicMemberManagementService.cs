using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace OMM.Admin.Services.Admin;

public sealed class PublicMemberDbContext(DbContextOptions<PublicMemberDbContext> options)
    : IdentityDbContext<PublicMemberUser>(options)
{
    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.HasDefaultSchema("public");
    }
}

public sealed class PublicMemberUser : IdentityUser
{
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public bool MustChangePassword { get; set; }
}

public sealed record PublicMemberListDto(
    string Id,
    string UserName,
    string Email,
    string? FirstName,
    string? LastName,
    bool EmailConfirmed,
    bool MustChangePassword,
    string? LockoutEnd,
    DateTimeOffset? LastLoginAt);

public sealed record PublicMemberUpdateDto(string UserId, string? FirstName, string? LastName);

public interface IPublicMemberManagementService
{
    Task<int> CountAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PublicMemberListDto>> ListAsync(string? searchTerm = null, CancellationToken cancellationToken = default);
    Task<IdentityResult> UpdateAsync(PublicMemberUpdateDto dto, CancellationToken cancellationToken = default);
    Task<IdentityResult> SetLockoutAsync(string userId, bool locked, CancellationToken cancellationToken = default);
    Task<IdentityResult> ForcePasswordResetAsync(string userId, CancellationToken cancellationToken = default);
    Task<IdentityResult> DeleteAsync(string userId, CancellationToken cancellationToken = default);
}

public sealed class PublicMemberManagementService(
    IDbContextFactory<PublicMemberDbContext> dbContextFactory) : IPublicMemberManagementService
{
    public async Task<int> CountAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await db.Users.CountAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<PublicMemberListDto>> ListAsync(
        string? searchTerm = null,
        CancellationToken cancellationToken = default)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var query = db.Users.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var term = searchTerm.Trim();
            query = query.Where(user =>
                (user.UserName != null && user.UserName.Contains(term)) ||
                (user.Email != null && user.Email.Contains(term)) ||
                (user.FirstName != null && user.FirstName.Contains(term)) ||
                (user.LastName != null && user.LastName.Contains(term)));
        }

        var users = await query
            .OrderBy(user => user.UserName)
            .Select(user => new
            {
                user.Id,
                UserName = user.UserName ?? string.Empty,
                Email = user.Email ?? string.Empty,
                user.FirstName,
                user.LastName,
                user.EmailConfirmed,
                user.MustChangePassword,
                user.LockoutEnd
            })
            .ToListAsync(cancellationToken);

        return users.Select(user => new PublicMemberListDto(
            user.Id,
            user.UserName,
            user.Email,
            user.FirstName,
            user.LastName,
            user.EmailConfirmed,
            user.MustChangePassword,
            user.LockoutEnd?.ToString("u"),
            null)).ToList();
    }

    public async Task<IdentityResult> UpdateAsync(PublicMemberUpdateDto dto, CancellationToken cancellationToken = default)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var user = await db.Users.SingleOrDefaultAsync(item => item.Id == dto.UserId, cancellationToken);
        if (user is null)
        {
            return IdentityResult.Failed(new IdentityError { Code = "NotFound", Description = "Public member was not found." });
        }

        user.FirstName = dto.FirstName?.Trim();
        user.LastName = dto.LastName?.Trim();
        await db.SaveChangesAsync(cancellationToken);
        return IdentityResult.Success;
    }

    public async Task<IdentityResult> SetLockoutAsync(string userId, bool locked, CancellationToken cancellationToken = default)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var user = await db.Users.SingleOrDefaultAsync(item => item.Id == userId, cancellationToken);
        if (user is null)
        {
            return IdentityResult.Failed(new IdentityError { Code = "NotFound", Description = "Public member was not found." });
        }

        user.LockoutEnabled = true;
        user.LockoutEnd = locked ? DateTimeOffset.MaxValue : null;
        await db.SaveChangesAsync(cancellationToken);
        return IdentityResult.Success;
    }

    public async Task<IdentityResult> ForcePasswordResetAsync(string userId, CancellationToken cancellationToken = default)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var user = await db.Users.SingleOrDefaultAsync(item => item.Id == userId, cancellationToken);
        if (user is null)
        {
            return IdentityResult.Failed(new IdentityError { Code = "NotFound", Description = "Public member was not found." });
        }

        user.MustChangePassword = true;
        user.SecurityStamp = Guid.NewGuid().ToString();
        await db.SaveChangesAsync(cancellationToken);
        return IdentityResult.Success;
    }

    public async Task<IdentityResult> DeleteAsync(string userId, CancellationToken cancellationToken = default)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var user = await db.Users.SingleOrDefaultAsync(item => item.Id == userId, cancellationToken);
        if (user is null)
        {
            return IdentityResult.Failed(new IdentityError { Code = "NotFound", Description = "Public member was not found." });
        }

        db.Users.Remove(user);
        await db.SaveChangesAsync(cancellationToken);
        return IdentityResult.Success;
    }
}
