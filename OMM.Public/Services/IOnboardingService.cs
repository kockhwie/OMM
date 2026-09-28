using OMM.Public.Data.Entities;

namespace OMM.Public.Services;

public interface IOnboardingService
{
    Task<OnboardingView> GetCurrentAsync(CancellationToken cancellationToken = default);

    Task<OnboardingView> SaveCurrencyAsync(int currencyId, CancellationToken cancellationToken = default);

    Task<OnboardingView> SaveCountryAsync(int? countryId, CancellationToken cancellationToken = default);

    Task<OnboardingView> SaveDisplayNameAsync(string? displayName, CancellationToken cancellationToken = default);

    Task<OnboardingView> CompleteFirstMineDecisionAsync(bool addMineNow, CancellationToken cancellationToken = default);
}

public sealed record OnboardingView(
    OnboardingStatus Status,
    OnboardingStep CurrentStep,
    int? CurrencyId,
    string? CurrencyCode,
    IReadOnlyList<ProfileCurrencyOption> Currencies,
    int? CountryId,
    string? DisplayName,
    DateTimeOffset? StartedAt,
    DateTimeOffset? SkippedAt,
    DateTimeOffset? CompletedAt);
