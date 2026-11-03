using Microsoft.EntityFrameworkCore;
using OMM.Admin.Data;
using OMM.Shared.Models.Phase5;

namespace OMM.Admin.Services.Admin;

public sealed class Phase5RuleAdministrationService(
    IDbContextFactory<MasterDataDbContext> dbContextFactory,
    IAuditLogger auditLogger,
    TimeProvider timeProvider)
{
    private static readonly HashSet<string> Directions = ["Positive", "Negative", "Mixed", "Neutral"];
    private static readonly HashSet<string> Strengths = ["Low", "Moderate", "Material"];
    private static readonly HashSet<string> Horizons = ["ShortTerm", "MediumTerm", "LongTerm"];

    public async Task<Phase5EventTypeRule> CreateEventTypeAsync(string code, string displayName, bool sensitive, string actorId, string actorName, CancellationToken cancellationToken = default)
    {
        var cleanCode = Required(code, 80, nameof(code)).ToUpperInvariant();
        var cleanName = Required(displayName, 160, nameof(displayName));
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        if (await db.Phase5EventTypeRules.AnyAsync(item => item.Code == cleanCode && item.IsActive, cancellationToken))
            throw new InvalidOperationException($"An active event type with code '{cleanCode}' already exists.");

        var now = timeProvider.GetUtcNow();
        var version = (await db.Phase5EventTypeRules.Where(item => item.Code == cleanCode).Select(item => (int?)item.Version).MaxAsync(cancellationToken) ?? 0) + 1;
        var rule = new Phase5EventTypeRule { Code = cleanCode, DisplayName = cleanName, IsSensitive = sensitive, Version = version, EffectiveFrom = now, CreatedAt = now };
        db.Phase5EventTypeRules.Add(rule);
        await db.SaveChangesAsync(cancellationToken);
        await auditLogger.LogAsync(actorId, actorName, "Phase5EventTypeCreated", detail: $"Code={cleanCode}; Version={version}; Sensitive={sensitive}");
        return rule;
    }

    public async Task DeactivateEventTypeAsync(int eventTypeRuleId, string actorId, string actorName, CancellationToken cancellationToken = default)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var eventType = await db.Phase5EventTypeRules.SingleOrDefaultAsync(item => item.Id == eventTypeRuleId && item.IsActive, cancellationToken)
            ?? throw new InvalidOperationException("The selected event type is no longer active.");

        var now = timeProvider.GetUtcNow();
        eventType.IsActive = false;
        eventType.EffectiveTo = now;
        var relatedRules = await db.Phase5ImpactRules
            .Where(item => item.EventTypeRuleId == eventTypeRuleId && item.IsActive)
            .ToListAsync(cancellationToken);
        foreach (var rule in relatedRules)
        {
            rule.IsActive = false;
            rule.EffectiveTo = now;
        }

        await db.SaveChangesAsync(cancellationToken);
        await auditLogger.LogAsync(actorId, actorName, "Phase5EventTypeDeactivated",
            detail: $"EventTypeId={eventType.Id}; Code={eventType.Code}; Version={eventType.Version}; ImpactRulesRetired={relatedRules.Count}");
    }

    public async Task<Phase5ImpactRule> CreateImpactRuleAsync(
        int eventTypeRuleId, string sourceType, string sourceKey, string targetType, string targetKey,
        string transmission, string direction, string strength, string timeHorizon, bool malaysiaRelevant,
        string? assumptions, string actorId, string actorName, CancellationToken cancellationToken = default)
    {
        var cleanSourceType = Required(sourceType, 40, nameof(sourceType));
        var cleanSourceKey = Required(sourceKey, 160, nameof(sourceKey)).ToUpperInvariant();
        var cleanTargetType = Required(targetType, 40, nameof(targetType));
        var cleanTargetKey = Required(targetKey, 160, nameof(targetKey)).ToUpperInvariant();
        var cleanTransmission = Required(transmission, 100, nameof(transmission));
        if (!Directions.Contains(direction) || !Strengths.Contains(strength) || !Horizons.Contains(timeHorizon))
            throw new ArgumentException("The rule direction, strength, or time horizon is not supported.");

        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var eventType = await db.Phase5EventTypeRules.SingleOrDefaultAsync(item => item.Id == eventTypeRuleId && item.IsActive, cancellationToken)
            ?? throw new InvalidOperationException("The selected event type is not active.");
        var duplicate = await db.Phase5ImpactRules.AnyAsync(item => item.EventTypeRuleId == eventTypeRuleId && item.SourceEntityType == cleanSourceType && item.SourceEntityKey == cleanSourceKey && item.TargetEntityType == cleanTargetType && item.TargetEntityKey == cleanTargetKey && item.IsActive, cancellationToken);
        if (duplicate) throw new InvalidOperationException("An active impact rule already connects these entities.");

        var now = timeProvider.GetUtcNow();
        var version = (await db.Phase5ImpactRules.Where(item => item.EventTypeRuleId == eventTypeRuleId && item.SourceEntityType == cleanSourceType && item.SourceEntityKey == cleanSourceKey && item.TargetEntityType == cleanTargetType && item.TargetEntityKey == cleanTargetKey).Select(item => (int?)item.Version).MaxAsync(cancellationToken) ?? 0) + 1;
        var rule = new Phase5ImpactRule
        {
            EventTypeRuleId = eventTypeRuleId, SourceEntityType = cleanSourceType, SourceEntityKey = cleanSourceKey,
            TargetEntityType = cleanTargetType, TargetEntityKey = cleanTargetKey, TransmissionMechanism = cleanTransmission,
            Direction = direction, Strength = strength, TimeHorizon = timeHorizon, MalaysiaRelevant = malaysiaRelevant,
            Assumptions = string.IsNullOrWhiteSpace(assumptions) ? null : assumptions.Trim()[..Math.Min(assumptions.Trim().Length, 1000)],
            Version = version, EffectiveFrom = now, CreatedAt = now, CreatedByUserId = actorId
        };
        db.Phase5ImpactRules.Add(rule);
        await db.SaveChangesAsync(cancellationToken);
        await auditLogger.LogAsync(actorId, actorName, "Phase5ImpactRuleCreated", detail: $"EventType={eventType.Code}; Source={cleanSourceType}:{cleanSourceKey}; Target={cleanTargetType}:{cleanTargetKey}; Version={version}");
        return rule;
    }

    public async Task DeactivateImpactRuleAsync(long ruleId, string actorId, string actorName, CancellationToken cancellationToken = default)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var rule = await db.Phase5ImpactRules.SingleOrDefaultAsync(item => item.Id == ruleId && item.IsActive, cancellationToken)
            ?? throw new InvalidOperationException("The selected impact rule is no longer active.");

        var now = timeProvider.GetUtcNow();
        rule.IsActive = false;
        rule.EffectiveTo = now;
        await db.SaveChangesAsync(cancellationToken);
        await auditLogger.LogAsync(actorId, actorName, "Phase5ImpactRuleDeactivated",
            detail: $"RuleId={rule.Id}; EventTypeId={rule.EventTypeRuleId}; Version={rule.Version}; Source={rule.SourceEntityType}:{rule.SourceEntityKey}; Target={rule.TargetEntityType}:{rule.TargetEntityKey}");
    }

    private static string Required(string? value, int maxLength, string name)
    {
        var clean = value?.Trim();
        if (string.IsNullOrWhiteSpace(clean) || clean.Length > maxLength) throw new ArgumentException($"{name} is required and must be at most {maxLength} characters.", name);
        return clean;
    }
}
