using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OMM.Shared.Models.Phase5;
using OMM.Shared.Subscription;

namespace OMM.Shared.DecisionSupport;

public sealed record ResolvedEntity(
    string EntityType,
    string EntityKey,
    string DisplayName,
    int? EntityId,
    string ResolutionMethod,
    decimal Confidence,
    bool IsResolved);

public sealed record ImpactEvaluationResult(
    int? EventTypeRuleId,
    string? EventTypeCode,
    string? EventDisplayName,
    bool IsSensitiveEvent,
    IReadOnlyList<Phase5ImpactRule> MatchedRules);

public interface IMasterDataResolutionService
{
    Task<ResolvedEntity?> ResolveSecurityAsync(string? rawText, CancellationToken cancellationToken = default);
    Task<ResolvedEntity?> ResolveSectorAsync(string? rawText, CancellationToken cancellationToken = default);
    Task<ResolvedEntity?> ResolveCountryAsync(string? rawText, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ResolvedEntity>> ResolveAllAsync(string? security, string? sector, string? country, CancellationToken cancellationToken = default);
    Task<ImpactEvaluationResult> EvaluateImpactRulesAsync(string? signalType, string? eventName, IReadOnlyList<ResolvedEntity> resolvedEntities, CancellationToken cancellationToken = default);
}

public sealed class MasterDataResolutionService(
    ISubscriptionDbContextFactory dbContextFactory,
    ILogger<MasterDataResolutionService> logger) : IMasterDataResolutionService
{
    public async Task<ResolvedEntity?> ResolveSecurityAsync(string? rawText, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(rawText)) return null;
        var clean = rawText.Trim();

        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        // 1. Exact match on StockCode
        var exactStock = await db.Stocks.AsNoTracking()
            .Where(s => s.IsActive && s.StockCode == clean)
            .Select(s => new { s.Id, s.StockCode, Name = s.ShortName_EN })
            .FirstOrDefaultAsync(cancellationToken);

        if (exactStock is not null)
        {
            return new ResolvedEntity("Stock", exactStock.StockCode, exactStock.Name, exactStock.Id, "ExactCode", 1.0m, true);
        }

        // 2. Exact match on ShortName_EN
        var exactNameStock = await db.Stocks.AsNoTracking()
            .Where(s => s.IsActive && EF.Functions.ILike(s.ShortName_EN, clean))
            .Select(s => new { s.Id, s.StockCode, Name = s.ShortName_EN })
            .FirstOrDefaultAsync(cancellationToken);

        if (exactNameStock is not null)
        {
            return new ResolvedEntity("Stock", exactNameStock.StockCode, exactNameStock.Name, exactNameStock.Id, "ExactName", 0.95m, true);
        }

        // 3. Substring match on ShortName_EN or LegalName_EN
        var pattern = $"%{clean}%";
        var fuzzyStock = await db.Stocks.AsNoTracking()
            .Where(s => s.IsActive && (EF.Functions.ILike(s.ShortName_EN, pattern) || (s.LegalName_EN != null && EF.Functions.ILike(s.LegalName_EN, pattern))))
            .Select(s => new { s.Id, s.StockCode, Name = s.ShortName_EN })
            .FirstOrDefaultAsync(cancellationToken);

        if (fuzzyStock is not null)
        {
            return new ResolvedEntity("Stock", fuzzyStock.StockCode, fuzzyStock.Name, fuzzyStock.Id, "NameMatch", 0.85m, true);
        }

        // 4. Retain unresolved entity
        return new ResolvedEntity("Stock", clean, clean, null, "Unresolved", 0.5m, false);
    }

    public async Task<ResolvedEntity?> ResolveSectorAsync(string? rawText, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(rawText)) return null;
        var clean = rawText.Trim();

        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        // 1. Exact match on SectorCode
        var exactSector = await db.Sectors.AsNoTracking()
            .Where(s => s.IsActive && s.SectorCode == clean)
            .Select(s => new { s.Id, s.SectorCode, Name = s.SectorName_EN })
            .FirstOrDefaultAsync(cancellationToken);

        if (exactSector is not null)
        {
            return new ResolvedEntity("Sector", exactSector.SectorCode, exactSector.Name, exactSector.Id, "ExactCode", 1.0m, true);
        }

        // 2. Exact or substring match on SectorName_EN
        var pattern = $"%{clean}%";
        var nameSector = await db.Sectors.AsNoTracking()
            .Where(s => s.IsActive && EF.Functions.ILike(s.SectorName_EN, pattern))
            .Select(s => new { s.Id, s.SectorCode, Name = s.SectorName_EN })
            .FirstOrDefaultAsync(cancellationToken);

        if (nameSector is not null)
        {
            return new ResolvedEntity("Sector", nameSector.SectorCode, nameSector.Name, nameSector.Id, "NameMatch", 0.9m, true);
        }

        // 3. Fallback for common aliases (e.g. Banking -> Financial Services)
        if (clean.Contains("bank", StringComparison.OrdinalIgnoreCase) || clean.Contains("finance", StringComparison.OrdinalIgnoreCase))
        {
            var finSector = await db.Sectors.AsNoTracking()
                .Where(s => s.IsActive && EF.Functions.ILike(s.SectorName_EN, "%financial%"))
                .Select(s => new { s.Id, s.SectorCode, Name = s.SectorName_EN })
                .FirstOrDefaultAsync(cancellationToken);

            if (finSector is not null)
            {
                return new ResolvedEntity("Sector", finSector.SectorCode, finSector.Name, finSector.Id, "AliasMatch", 0.85m, true);
            }
        }

        return new ResolvedEntity("Sector", clean, clean, null, "Unresolved", 0.5m, false);
    }

    public async Task<ResolvedEntity?> ResolveCountryAsync(string? rawText, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(rawText)) return null;
        var clean = rawText.Trim();

        // Common Malaysia alias resolution
        if (clean.Equals("Malaysia", StringComparison.OrdinalIgnoreCase) ||
            clean.Equals("MY", StringComparison.OrdinalIgnoreCase) ||
            clean.Equals("MYS", StringComparison.OrdinalIgnoreCase) ||
            clean.Equals("Malaysian", StringComparison.OrdinalIgnoreCase))
        {
            return new ResolvedEntity("Country", "MY", "Malaysia", null, "ExactAlias", 1.0m, true);
        }

        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        var country = await db.Countries.AsNoTracking()
            .Where(c => c.IsActive && (c.CountryCode == clean || EF.Functions.ILike(c.CountryName_EN, clean)))
            .Select(c => new { c.Id, c.CountryCode, Name = c.CountryName_EN })
            .FirstOrDefaultAsync(cancellationToken);

        if (country is not null)
        {
            return new ResolvedEntity("Country", country.CountryCode, country.Name, country.Id, "ExactMatch", 1.0m, true);
        }

        return new ResolvedEntity("Country", clean, clean, null, "Unresolved", 0.5m, false);
    }

    public async Task<IReadOnlyList<ResolvedEntity>> ResolveAllAsync(
        string? security,
        string? sector,
        string? country,
        CancellationToken cancellationToken = default)
    {
        var results = new List<ResolvedEntity>();

        if (!string.IsNullOrWhiteSpace(security))
        {
            var sec = await ResolveSecurityAsync(security, cancellationToken);
            if (sec is not null) results.Add(sec);
        }

        if (!string.IsNullOrWhiteSpace(sector))
        {
            var sec = await ResolveSectorAsync(sector, cancellationToken);
            if (sec is not null) results.Add(sec);
        }

        if (!string.IsNullOrWhiteSpace(country))
        {
            var ctry = await ResolveCountryAsync(country, cancellationToken);
            if (ctry is not null) results.Add(ctry);
        }

        logger.LogDebug("Resolved entities for security='{Security}', sector='{Sector}', country='{Country}'. Found {Count} entities.",
            security, sector, country, results.Count);

        return results;
    }

    public async Task<ImpactEvaluationResult> EvaluateImpactRulesAsync(
        string? signalType,
        string? eventName,
        IReadOnlyList<ResolvedEntity> resolvedEntities,
        CancellationToken cancellationToken = default)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        // 1. Locate matching active event type rule
        Phase5EventTypeRule? matchedEventType = null;
        if (!string.IsNullOrWhiteSpace(eventName))
        {
            matchedEventType = await db.Phase5EventTypeRules.AsNoTracking()
                .Where(r => r.IsActive && (EF.Functions.ILike(r.DisplayName, $"%{eventName.Trim()}%") || EF.Functions.ILike(r.Code, $"%{eventName.Trim()}%")))
                .FirstOrDefaultAsync(cancellationToken);
        }

        if (matchedEventType is null && !string.IsNullOrWhiteSpace(signalType))
        {
            matchedEventType = await db.Phase5EventTypeRules.AsNoTracking()
                .Where(r => r.IsActive && EF.Functions.ILike(r.Code, $"%{signalType.Trim()}%"))
                .FirstOrDefaultAsync(cancellationToken);
        }

        if (matchedEventType is null)
        {
            return new ImpactEvaluationResult(null, null, null, false, []);
        }

        // 2. Match active impact rules for this event type
        var activeRules = await db.Phase5ImpactRules.AsNoTracking()
            .Where(r => r.IsActive && r.EventTypeRuleId == matchedEventType.Id)
            .ToListAsync(cancellationToken);

        if (resolvedEntities.Count == 0)
        {
            return new ImpactEvaluationResult(
                matchedEventType.Id,
                matchedEventType.Code,
                matchedEventType.DisplayName,
                matchedEventType.IsSensitive,
                activeRules);
        }

        // 3. Filter rules that connect to the resolved entities or Malaysia
        var entityKeys = resolvedEntities.Select(e => e.EntityKey).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var filtered = activeRules
            .Where(r => r.MalaysiaRelevant || entityKeys.Contains(r.SourceEntityKey) || entityKeys.Contains(r.TargetEntityKey))
            .ToList();

        return new ImpactEvaluationResult(
            matchedEventType.Id,
            matchedEventType.Code,
            matchedEventType.DisplayName,
            matchedEventType.IsSensitive,
            filtered.Count > 0 ? filtered : activeRules);
    }
}
