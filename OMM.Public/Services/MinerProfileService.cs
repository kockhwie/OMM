using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.EntityFrameworkCore;
using OMM.Public.Data;
using OMM.Public.Data.Entities;

namespace OMM.Public.Services;

public sealed class MinerProfileService(
    IDbContextFactory<ApplicationDbContext> dbContextFactory,
    AuthenticationStateProvider authenticationStateProvider,
    ILogger<MinerProfileService> logger) : IMinerProfileService
{
    public async Task<MinerProfileView?> GetCurrentAsync(CancellationToken cancellationToken = default)
    {
        var userId = await GetCurrentUserIdAsync(cancellationToken);
        if (userId is null)
        {
            return null;
        }

        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var profile = await db.MinerProfiles
            .AsNoTracking()
            .Include(item => item.Country)
            .Include(item => item.Currency)
            .SingleOrDefaultAsync(item => item.UserId == userId, cancellationToken);

        return profile is null ? null : Map(profile);
    }

    public async Task<MinerProfileView> CreateIfMissingAsync(CancellationToken cancellationToken = default)
    {
        var userId = await GetCurrentUserIdAsync(cancellationToken)
            ?? throw new InvalidOperationException("An authenticated Public user is required to create a profile.");

        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var user = await db.Users.SingleAsync(item => item.Id == userId, cancellationToken);
        return await CreateForUserAsync(db, user, cancellationToken);
    }

    public async Task<MinerProfileView> CreateForUserAsync(
        ApplicationUser user,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(user);

        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await CreateForUserAsync(db, user, cancellationToken);
    }

    public async Task<MinerProfileView> UpdateCurrentAsync(
        MinerProfileUpdate update,
        CancellationToken cancellationToken = default)
    {
        var userId = await GetCurrentUserIdAsync(cancellationToken)
            ?? throw new InvalidOperationException("An authenticated Public user is required to update a profile.");

        var displayName = string.IsNullOrWhiteSpace(update.DisplayName)
            ? null
            : update.DisplayName.Trim();

        if (displayName is { Length: > 200 })
        {
            throw new ArgumentException("Display name cannot exceed 200 characters.", nameof(update));
        }

        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var profile = await db.MinerProfiles
            .SingleOrDefaultAsync(item => item.UserId == userId, cancellationToken);

        if (profile is null)
        {
            var user = await db.Users.SingleAsync(item => item.Id == userId, cancellationToken);
            await CreateForUserAsync(db, user, cancellationToken, saveChanges: false);
            profile = await db.MinerProfiles.SingleAsync(item => item.UserId == userId, cancellationToken);
        }

        profile.DisplayName = displayName;
        profile.CountryId = await ValidateCountryAsync(db, update.CountryId, cancellationToken);
        profile.CurrencyId = await ValidateCurrencyAsync(db, update.CurrencyId, cancellationToken);
        profile.Language = string.IsNullOrWhiteSpace(update.Language) ? null : update.Language.Trim();
        profile.ModifiedAt = DateTimeOffset.UtcNow;

        await db.SaveChangesAsync(cancellationToken);

        await db.Entry(profile).Reference(item => item.Country).LoadAsync(cancellationToken);
        await db.Entry(profile).Reference(item => item.Currency).LoadAsync(cancellationToken);
        return Map(profile);
    }

    public async Task<IReadOnlyList<ProfileCurrencyOption>> GetCurrencyOptionsAsync(
        CancellationToken cancellationToken = default)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await db.Currencies
            .AsNoTracking()
            .Where(item => item.IsActive)
            .OrderBy(item => item.Code)
            .Select(item => new ProfileCurrencyOption(item.Id, item.Code, item.Name_EN))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ProfileCountryOption>> GetCountryOptionsAsync(
        CancellationToken cancellationToken = default)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var currencies = await db.Currencies
            .AsNoTracking()
            .Where(item => item.IsActive)
            .ToDictionaryAsync(item => item.Code, item => item.Id, cancellationToken);

        var countries = await db.Countries
            .AsNoTracking()
            .Where(item => item.IsActive)
            .OrderBy(item => item.CountryName_EN)
            .Select(item => new { item.Id, item.CountryCode, item.CountryName_EN, item.DefaultCurrencyCode })
            .ToListAsync(cancellationToken);

        return countries
            .Select(item => new ProfileCountryOption(
                item.Id,
                item.CountryCode,
                item.CountryName_EN,
                currencies.TryGetValue(item.DefaultCurrencyCode, out var currencyId) ? currencyId : null))
            .ToList();
    }

    private async Task<MinerProfileView> CreateForUserAsync(
        ApplicationDbContext db,
        ApplicationUser user,
        CancellationToken cancellationToken,
        bool saveChanges = true)
    {
        var profile = await db.MinerProfiles
            .IgnoreQueryFilters()
            .SingleOrDefaultAsync(item => item.UserId == user.Id, cancellationToken);

        if (profile is null)
        {
            profile = new MinerProfileEntity
            {
                UserId = user.Id,
                Email = user.Email ?? user.UserName ?? string.Empty,
                CreatedAt = DateTimeOffset.UtcNow,
                IsDeleted = false
            };
            db.MinerProfiles.Add(profile);
        }
        else
        {
            profile.Email = user.Email ?? user.UserName ?? profile.Email;
            profile.IsDeleted = false;
            profile.DeletedAt = null;
            profile.ModifiedAt ??= DateTimeOffset.UtcNow;
        }

        if (saveChanges)
        {
            await db.SaveChangesAsync(cancellationToken);
        }

        logger.LogInformation("Miner profile ensured for Public user {UserId}.", user.Id);
        return Map(profile);
    }

    private static async Task<int?> ValidateCurrencyAsync(
        ApplicationDbContext db,
        int? currencyId,
        CancellationToken cancellationToken)
    {
        if (currencyId is null)
        {
            return null;
        }

        var exists = await db.Currencies.AnyAsync(
            item => item.Id == currencyId && item.IsActive,
            cancellationToken);
        return exists
            ? currencyId
            : throw new ArgumentException("The selected currency is not available.", nameof(currencyId));
    }

    private static async Task<int?> ValidateCountryAsync(
        ApplicationDbContext db,
        int? countryId,
        CancellationToken cancellationToken)
    {
        if (countryId is null)
        {
            return null;
        }

        var exists = await db.Countries.AnyAsync(
            item => item.Id == countryId && item.IsActive,
            cancellationToken);
        return exists
            ? countryId
            : throw new ArgumentException("The selected country is not available.", nameof(countryId));
    }

    private async Task<string?> GetCurrentUserIdAsync(CancellationToken cancellationToken)
    {
        var state = await authenticationStateProvider.GetAuthenticationStateAsync();
        return state.User.Identity?.IsAuthenticated == true
            ? state.User.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? state.User.FindFirstValue("sub")
            : null;
    }

    private static MinerProfileView Map(MinerProfileEntity profile) => new(
        profile.UserId,
        profile.Email,
        profile.DisplayName,
        profile.CountryId,
        profile.Country?.CountryName_EN,
        profile.CurrencyId,
        profile.Currency?.Code,
        profile.Currency?.Name_EN,
        profile.Language,
        profile.CreatedAt,
        !string.IsNullOrWhiteSpace(profile.DisplayName)
            && profile.CountryId.HasValue
            && profile.CurrencyId.HasValue
            && !string.IsNullOrWhiteSpace(profile.Language));
}
