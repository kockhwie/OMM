using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.EntityFrameworkCore;
using OMM.Public.Data;
using OMM.Public.Data.Entities;

namespace OMM.Public.Services;

public sealed class OnboardingService(
    IDbContextFactory<ApplicationDbContext> dbContextFactory,
    AuthenticationStateProvider authenticationStateProvider,
    ILogger<OnboardingService> logger) : IOnboardingService
{
    public async Task<OnboardingView> GetCurrentAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var profile = await GetOrCreateProfileAsync(db, cancellationToken);
        if (db.Entry(profile).State == EntityState.Added)
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        return await MapAsync(db, profile, cancellationToken);
    }

    public Task<OnboardingView> SaveCurrencyAsync(int currencyId, CancellationToken cancellationToken = default) =>
        UpdateAsync(async (db, profile, token) =>
        {
            if (!await db.Currencies.AnyAsync(item => item.Id == currencyId && item.IsActive, token))
            {
                throw new ArgumentException("The selected currency is not available.", nameof(currencyId));
            }

            profile.CurrencyId = currencyId;
            BeginIfNeeded(profile);
            profile.OnboardingStep = OnboardingStep.Country;
        }, cancellationToken);

    public Task<OnboardingView> SaveCountryAsync(int? countryId, CancellationToken cancellationToken = default) =>
        UpdateAsync(async (db, profile, token) =>
        {
            if (!countryId.HasValue)
            {
                throw new ArgumentException("Choose a country before continuing onboarding.", nameof(countryId));
            }

            if (!await db.Countries.AnyAsync(item => item.Id == countryId.Value && item.IsActive, token))
            {
                throw new ArgumentException("The selected country is not available.", nameof(countryId));
            }

            RequireCurrency(profile);
            profile.CountryId = countryId;
            BeginIfNeeded(profile);
            profile.OnboardingStep = OnboardingStep.DisplayName;
        }, cancellationToken);

    public Task<OnboardingView> SaveDisplayNameAsync(string? displayName, CancellationToken cancellationToken = default) =>
        UpdateAsync((_, profile, _) =>
        {
            RequireCurrency(profile);
            if (string.IsNullOrWhiteSpace(displayName))
            {
                throw new ArgumentException("Enter a display name before finishing onboarding.", nameof(displayName));
            }

            var value = displayName.Trim();
            if (value.Length > 200)
            {
                throw new ArgumentException("Display name cannot exceed 200 characters.", nameof(displayName));
            }

            RequireCountry(profile);
            profile.DisplayName = value;
            BeginIfNeeded(profile);
            profile.OnboardingStatus = OnboardingStatus.Completed;
            profile.OnboardingStep = OnboardingStep.Complete;
            profile.OnboardingCompletedAt ??= DateTimeOffset.UtcNow;
            return Task.CompletedTask;
        }, cancellationToken);

    public Task<OnboardingView> CompleteFirstMineDecisionAsync(bool addMineNow, CancellationToken cancellationToken = default) =>
        UpdateAsync((_, profile, _) =>
        {
            RequireCompleteProfile(profile);
            profile.OnboardingStatus = OnboardingStatus.Completed;
            profile.OnboardingStep = OnboardingStep.Complete;
            profile.OnboardingCompletedAt ??= DateTimeOffset.UtcNow;
            return Task.CompletedTask;
        }, cancellationToken);

    private async Task<OnboardingView> UpdateAsync(
        Func<ApplicationDbContext, MinerProfileEntity, CancellationToken, Task> update,
        CancellationToken cancellationToken)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var profile = await GetOrCreateProfileAsync(db, cancellationToken);
        await update(db, profile, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Onboarding state {Status}/{Step} saved for Public user {UserId}.", profile.OnboardingStatus, profile.OnboardingStep, profile.UserId);
        return await MapAsync(db, profile, cancellationToken);
    }

    private async Task<MinerProfileEntity> GetOrCreateProfileAsync(ApplicationDbContext db, CancellationToken cancellationToken)
    {
        var userId = await GetCurrentUserIdAsync(cancellationToken)
            ?? throw new InvalidOperationException("An authenticated Public user is required for onboarding.");
        var profile = await db.MinerProfiles
            .SingleOrDefaultAsync(item => item.UserId == userId, cancellationToken);
        if (profile is not null)
        {
            return profile;
        }

        var user = await db.Users.SingleAsync(item => item.Id == userId, cancellationToken);
        profile = new MinerProfileEntity
        {
            UserId = user.Id,
            Email = user.Email ?? user.UserName ?? string.Empty,
            CreatedAt = DateTimeOffset.UtcNow,
            OnboardingStatus = OnboardingStatus.NotStarted,
            OnboardingStep = OnboardingStep.Welcome
        };
        db.MinerProfiles.Add(profile);
        return profile;
    }

    private static async Task<OnboardingView> MapAsync(
        ApplicationDbContext db,
        MinerProfileEntity profile,
        CancellationToken cancellationToken)
    {
        await db.Entry(profile).Reference(item => item.Currency).LoadAsync(cancellationToken);
        var currencies = await db.Currencies
            .AsNoTracking()
            .Where(item => item.IsActive)
            .OrderBy(item => item.Code)
            .Select(item => new ProfileCurrencyOption(item.Id, item.Code, item.Name_EN))
            .ToListAsync(cancellationToken);

        return new OnboardingView(
            profile.OnboardingStatus,
            profile.OnboardingStep,
            profile.CurrencyId,
            profile.Currency?.Code,
            currencies,
            profile.CountryId,
            profile.DisplayName,
            profile.OnboardingStartedAt,
            profile.OnboardingSkippedAt,
            profile.OnboardingCompletedAt);
    }

    private static void BeginIfNeeded(MinerProfileEntity profile)
    {
        if (profile.OnboardingStatus == OnboardingStatus.NotStarted)
        {
            profile.OnboardingStatus = OnboardingStatus.InProgress;
            profile.OnboardingStartedAt = DateTimeOffset.UtcNow;
        }
    }

    private static void RequireCurrency(MinerProfileEntity profile)
    {
        if (!profile.CurrencyId.HasValue)
        {
            throw new ArgumentException("Choose a currency before continuing onboarding.", nameof(profile));
        }
    }

    private static void RequireCountry(MinerProfileEntity profile)
    {
        if (!profile.CountryId.HasValue)
        {
            throw new ArgumentException("Choose a country before continuing onboarding.", nameof(profile));
        }
    }

    private static void RequireCompleteProfile(MinerProfileEntity profile)
    {
        RequireCurrency(profile);
        RequireCountry(profile);
        if (string.IsNullOrWhiteSpace(profile.DisplayName))
        {
            throw new ArgumentException("Enter a display name before finishing onboarding.", nameof(profile));
        }
    }

    private async Task<string?> GetCurrentUserIdAsync(CancellationToken cancellationToken)
    {
        var state = await authenticationStateProvider.GetAuthenticationStateAsync();
        return state.User.Identity?.IsAuthenticated == true
            ? state.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? state.User.FindFirstValue("sub")
            : null;
    }
}
