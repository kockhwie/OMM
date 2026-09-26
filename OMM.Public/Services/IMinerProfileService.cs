using OMM.Public.Data;

namespace OMM.Public.Services;

public interface IMinerProfileService
{
    Task<MinerProfileView?> GetCurrentAsync(CancellationToken cancellationToken = default);

    Task<MinerProfileView> CreateIfMissingAsync(CancellationToken cancellationToken = default);

    Task<MinerProfileView> CreateForUserAsync(ApplicationUser user, CancellationToken cancellationToken = default);

    Task<MinerProfileView> UpdateCurrentAsync(
        MinerProfileUpdate update,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ProfileCurrencyOption>> GetCurrencyOptionsAsync(
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ProfileCountryOption>> GetCountryOptionsAsync(
        CancellationToken cancellationToken = default);
}

public sealed record MinerProfileUpdate(
    string? DisplayName,
    int? CountryId,
    int? CurrencyId,
    string? Language);

public sealed record MinerProfileView(
    string UserId,
    string Email,
    string? DisplayName,
    int? CountryId,
    string? CountryName,
    int? CurrencyId,
    string? CurrencyCode,
    string? CurrencyName,
    string? Language,
    DateTimeOffset CreatedAt,
    bool IsComplete);

public sealed record ProfileCurrencyOption(int Id, string Code, string Name);

public sealed record ProfileCountryOption(int Id, string Code, string Name, int? DefaultCurrencyId);
