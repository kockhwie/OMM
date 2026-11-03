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
    bool IsResolved,
    int? RelatedEntityId = null);

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
            .Select(s => new { s.Id, s.StockCode, Name = s.ShortName_EN, s.SectorId })
            .FirstOrDefaultAsync(cancellationToken);

        if (exactStock is not null)
        {
            return new ResolvedEntity("Stock", exactStock.StockCode, exactStock.Name, exactStock.Id, "ExactCode", 1.0m, true,
                RelatedEntityId: exactStock.SectorId);
        }

        // Names are accepted only on an exact match. Substring matches can resolve
        // a short/common word to an unrelated listed company.
        var exactNameStocks = await db.Stocks.AsNoTracking()
            .Where(s => s.IsActive && (EF.Functions.ILike(s.ShortName_EN, clean) ||
                (s.LegalName_EN != null && EF.Functions.ILike(s.LegalName_EN, clean))))
            .Select(s => new { s.Id, s.StockCode, Name = s.ShortName_EN, s.SectorId })
            .Take(2)
            .ToListAsync(cancellationToken);

        if (exactNameStocks.Count == 1)
        {
            var stock = exactNameStocks[0];
            return new ResolvedEntity("Stock", stock.StockCode, stock.Name, stock.Id, "ExactName", 0.95m, true,
                RelatedEntityId: stock.SectorId);
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

        // Avoid fuzzy sector matches: a partial name is not an approved link.
        var namedSectors = await db.Sectors.AsNoTracking()
            .Where(s => s.IsActive && EF.Functions.ILike(s.SectorName_EN, clean))
            .Select(s => new { s.Id, s.SectorCode, Name = s.SectorName_EN })
            .Take(2)
            .ToListAsync(cancellationToken);

        if (namedSectors.Count == 1)
        {
            var sector = namedSectors[0];
            return new ResolvedEntity("Sector", sector.SectorCode, sector.Name, sector.Id, "ExactName", 0.95m, true);
        }

        return new ResolvedEntity("Sector", clean, clean, null, "Unresolved", 0.5m, false);
    }

    public async Task<ResolvedEntity?> ResolveCountryAsync(string? rawText, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(rawText)) return null;
        var clean = rawText.Trim();

        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        var country = await db.Countries.AsNoTracking()
            .Where(c => c.IsActive && (EF.Functions.ILike(c.CountryCode, clean) || EF.Functions.ILike(c.CountryName_EN, clean)))
            .Select(c => new { c.Id, c.CountryCode, Name = c.CountryName_EN })
            .Take(2)
            .ToListAsync(cancellationToken);

        if (country.Count == 1)
        {
            var resolved = country[0];
            return new ResolvedEntity("Country", resolved.CountryCode, resolved.Name, resolved.Id, "ExactMatch", 1.0m, true);
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

        var resolvedStock = results.FirstOrDefault(entity => entity.EntityType == "Stock" && entity.IsResolved);
        var resolvedSector = results.FirstOrDefault(entity => entity.EntityType == "Sector" && entity.IsResolved);
        if (resolvedStock?.RelatedEntityId is int stockSectorId && resolvedSector?.EntityId is int mentionedSectorId &&
            stockSectorId != mentionedSectorId)
        {
            var sectorIndex = results.IndexOf(resolvedSector);
            results[sectorIndex] = resolvedSector with
            {
                IsResolved = false,
                ResolutionMethod = "UnverifiedStockRelationship",
                Confidence = 0.5m
            };
        }
        else if (resolvedStock?.RelatedEntityId is int relatedSectorId && resolvedSector is null)
        {
            await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
            var relatedSector = await db.Sectors.AsNoTracking()
                .Where(item => item.Id == relatedSectorId && item.IsActive)
                .Select(item => new { item.Id, item.SectorCode, Name = item.SectorName_EN })
                .SingleOrDefaultAsync(cancellationToken);
            if (relatedSector is not null)
            {
                results.Add(new ResolvedEntity("Sector", relatedSector.SectorCode, relatedSector.Name,
                    relatedSector.Id, "MasterDataRelationship", 1.0m, true));
            }
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

        // Match event taxonomy by exact code/name and effective date only.
        // Substring searches can silently select the wrong taxonomy row.
        Phase5EventTypeRule? matchedEventType = null;
        var now = DateTimeOffset.UtcNow;
        if (!string.IsNullOrWhiteSpace(eventName))
        {
            var cleanEventName = eventName.Trim();
            var eventTypeMatches = await db.Phase5EventTypeRules.AsNoTracking()
                .Where(r => r.IsActive && r.EffectiveFrom <= now && (r.EffectiveTo == null || r.EffectiveTo > now) &&
                    (EF.Functions.ILike(r.DisplayName, cleanEventName) || EF.Functions.ILike(r.Code, cleanEventName)))
                .OrderByDescending(r => r.Version)
                .Take(2)
                .ToListAsync(cancellationToken);
            if (eventTypeMatches.Count == 1) matchedEventType = eventTypeMatches[0];
        }

        if (matchedEventType is null && !string.IsNullOrWhiteSpace(signalType))
        {
            var eventTypeMatches = await db.Phase5EventTypeRules.AsNoTracking()
                .Where(r => r.IsActive && r.EffectiveFrom <= now && (r.EffectiveTo == null || r.EffectiveTo > now) &&
                    EF.Functions.ILike(r.Code, signalType.Trim()))
                .OrderByDescending(r => r.Version)
                .Take(2)
                .ToListAsync(cancellationToken);
            if (eventTypeMatches.Count == 1) matchedEventType = eventTypeMatches[0];
        }

        if (matchedEventType is null)
        {
            return new ImpactEvaluationResult(null, null, null, false, []);
        }

        // 2. Match active impact rules for this event type
        var activeRules = await db.Phase5ImpactRules.AsNoTracking()
            .Where(r => r.IsActive && r.EventTypeRuleId == matchedEventType.Id &&
                r.EffectiveFrom <= now && (r.EffectiveTo == null || r.EffectiveTo > now))
            .OrderByDescending(r => r.Version)
            .ToListAsync(cancellationToken);

        if (resolvedEntities.Count == 0)
        {
            // Event taxonomy may be known, but without a resolved entity there
            // is no safe endpoint to connect to a configured impact rule.
            return new ImpactEvaluationResult(
                matchedEventType.Id,
                matchedEventType.Code,
                matchedEventType.DisplayName,
                matchedEventType.IsSensitive,
                []);
        }

        // Rules are eligible only when an endpoint matches both an approved
        // entity type and its canonical master-data key. Malaysia relevance is
        // metadata; it does not establish an entity relationship.
        var resolved = resolvedEntities.Where(entity => entity.IsResolved).ToList();
        var filtered = activeRules.Where(rule => resolved.Any(entity =>
            EndpointMatches(rule.SourceEntityType, rule.SourceEntityKey, entity) ||
            EndpointMatches(rule.TargetEntityType, rule.TargetEntityKey, entity))).ToList();

        return new ImpactEvaluationResult(
            matchedEventType.Id,
            matchedEventType.Code,
            matchedEventType.DisplayName,
            matchedEventType.IsSensitive,
            filtered);
    }

    private static bool EndpointMatches(string ruleType, string ruleKey, ResolvedEntity entity)
    {
        var normalizedRuleType = ruleType.Trim().ToUpperInvariant() switch
        {
            "SECURITY" => "STOCK",
            _ => ruleType.Trim().ToUpperInvariant()
        };
        return string.Equals(normalizedRuleType, entity.EntityType.Trim().ToUpperInvariant(), StringComparison.Ordinal) &&
               string.Equals(ruleKey.Trim(), entity.EntityKey.Trim(), StringComparison.OrdinalIgnoreCase);
    }
}
