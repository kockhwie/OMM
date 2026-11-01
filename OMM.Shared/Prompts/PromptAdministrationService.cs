using System.Data;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OMM.Shared.Models;
using OMM.Shared.Models.Phase5;
using OMM.Shared.Subscription;

namespace OMM.Shared.Prompts;

public sealed class PromptAdministrationService(
    ISubscriptionDbContextFactory dbContextFactory,
    IMemoryCache cache,
    EmergencyPromptCatalog emergencyPrompts,
    IConfiguration configuration,
    IHostEnvironment hostEnvironment,
    TimeProvider timeProvider,
    ILogger<PromptAdministrationService> logger) : IPromptProvider, IPromptAdministrationService
{
    public const string SettingsRevisionKey = "Phase5:SettingsRevision";
    private static readonly TimeSpan RevisionPollInterval = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan PromptCacheDuration = TimeSpan.FromMinutes(5);
    private static readonly Regex PlaceholderPattern = new(@"\{([A-Za-z][A-Za-z0-9_]*)\}", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private const string RevisionCacheKey = "phase5:settings-revision";

    public async Task<PromptVersionDto?> GetActivePromptAsync(string promptModeCode, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(promptModeCode);
        try
        {
            await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
            var revision = await GetRevisionAsync(db, cancellationToken);
            var mode = await db.AiPromptModes.AsNoTracking()
                .SingleOrDefaultAsync(x => x.Code == promptModeCode && x.IsActive, cancellationToken);
            if (mode is not null)
            {
                var active = await db.AiPromptVersions.AsNoTracking()
                    .Where(x => x.PromptModeCode == promptModeCode && x.SchemaVersion == mode.SchemaVersion && x.Status == "Active")
                    .SingleOrDefaultAsync(cancellationToken);
                if (active is not null)
                {
                    return CacheVersion(active, revision);
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to load active database prompt for mode '{PromptModeCode}'; attempting emergency baseline fallback.", promptModeCode);
        }

        return GetEmergencyFallback(promptModeCode);
    }

    public async Task<PromptResolutionResult> ResolvePromptAsync(
        string requestedMode,
        bool proEnabled = true,
        int? overrideVersionId = null,
        CancellationToken cancellationToken = default)
    {
        if (overrideVersionId.HasValue && overrideVersionId.Value > 0)
        {
            var overridePrompt = await GetVersionAsync(overrideVersionId.Value, cancellationToken);
            if (overridePrompt is not null)
            {
                return new PromptResolutionResult(overridePrompt, overridePrompt.PromptModeCode, false, null, false);
            }
        }

        if (string.Equals(requestedMode, "PRO", StringComparison.OrdinalIgnoreCase))
        {
            if (!proEnabled)
            {
                var standardFallback = await GetActivePromptAsync("STANDARD", cancellationToken);
                return standardFallback is not null
                    ? new PromptResolutionResult(standardFallback, "STANDARD", IsFallback: true, FallbackReason: "PublicProDisabled", IsUnavailable: false)
                    : new PromptResolutionResult(null, "STANDARD", IsFallback: true, FallbackReason: "PublicProDisabled", IsUnavailable: true);
            }

            var proPrompt = await GetActivePromptAsync("PRO", cancellationToken);
            if (proPrompt is not null)
            {
                return new PromptResolutionResult(proPrompt, "PRO", IsFallback: false, FallbackReason: null, IsUnavailable: false);
            }

            var safeStandard = await GetActivePromptAsync("STANDARD", cancellationToken);
            return safeStandard is not null
                ? new PromptResolutionResult(safeStandard, "STANDARD", IsFallback: true, FallbackReason: "ProPromptUnavailable", IsUnavailable: false)
                : new PromptResolutionResult(null, "STANDARD", IsFallback: true, FallbackReason: "ProPromptUnavailable", IsUnavailable: true);
        }

        var standard = await GetActivePromptAsync("STANDARD", cancellationToken);
        return standard is not null
            ? new PromptResolutionResult(standard, "STANDARD", IsFallback: false, FallbackReason: null, IsUnavailable: false)
            : new PromptResolutionResult(null, "STANDARD", IsFallback: false, FallbackReason: "StandardPromptUnavailable", IsUnavailable: true);
    }

    public async Task<PromptVersionDto?> GetVersionAsync(int promptVersionId, CancellationToken cancellationToken = default)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var revision = await GetRevisionAsync(db, cancellationToken);
        var version = await db.AiPromptVersions.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == promptVersionId, cancellationToken);

        return version is null ? null : CacheVersion(version, revision);
    }

    public async Task<IReadOnlyList<PromptVersionDto>> GetVersionsAsync(string promptModeCode, CancellationToken cancellationToken = default)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var revision = await GetRevisionAsync(db, cancellationToken);
        var versions = await db.AiPromptVersions.AsNoTracking()
            .Where(x => x.PromptModeCode == promptModeCode)
            .OrderByDescending(x => x.CreatedAt)
            .ThenByDescending(x => x.Id)
            .ToListAsync(cancellationToken);
        return versions.Select(version => CacheVersion(version, revision)).ToArray();
    }

    public async Task<IReadOnlyList<AiPromptAuditEventDto>> GetAuditEventsAsync(
        string promptModeCode,
        int take = 100,
        CancellationToken cancellationToken = default)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var events = await db.AiPromptAuditEvents.AsNoTracking()
            .Join(db.AiPromptVersions.AsNoTracking().Where(x => x.PromptModeCode == promptModeCode),
                audit => audit.PromptVersionId,
                prompt => prompt.Id,
                (audit, prompt) => new AiPromptAuditEventDto(
                    audit.Id,
                    audit.PromptVersionId,
                    prompt.PromptModeCode,
                    audit.Action,
                    audit.ActorType,
                    audit.ActorId,
                    audit.OccurredAt,
                    audit.Notes,
                    audit.ChangeMetadataJson))
            .OrderByDescending(x => x.OccurredAt)
            .Take(Math.Clamp(take, 1, 500))
            .ToListAsync(cancellationToken);
        return events;
    }

    public async Task<PromptVersionDto> CreateDraftAsync(
        PromptDraftInput input,
        string? actorId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        ValidatePromptText(input.VersionLabel, input.SystemInstruction, input.UserMessageTemplate);

        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var mode = await db.AiPromptModes.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Code == input.PromptModeCode && x.IsActive, cancellationToken)
            ?? throw new InvalidOperationException($"Prompt mode '{input.PromptModeCode}' is not configured or active.");
        var version = new AiPromptVersion
        {
            PromptModeCode = mode.Code,
            VersionLabel = input.VersionLabel.Trim(),
            SystemInstruction = input.SystemInstruction.Trim(),
            UserMessageTemplate = input.UserMessageTemplate.Trim(),
            SchemaVersion = mode.SchemaVersion,
            ContentHash = PromptContentHash.Compute(mode.Code, mode.SchemaVersion, input.SystemInstruction.Trim(), input.UserMessageTemplate.Trim()),
            Status = "Draft",
            Notes = input.Notes,
            CreatedAt = timeProvider.GetUtcNow(),
            CreatedByUserId = actorId
        };

        await using var transaction = await BeginTransactionAsync(db, cancellationToken);
        db.AiPromptVersions.Add(version);
        await db.SaveChangesAsync(cancellationToken);
        AddAudit(db, version, "Created", actorId, input.Notes, new { version.ContentHash, version.Status });
        await BumpRevisionAsync(db, actorId, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        await CommitAsync(transaction, cancellationToken);
        InvalidateRevisionCache();
        return ToDto(version);
    }

    public async Task<PromptVersionDto> ActivateAsync(
        int promptVersionId,
        string? actorId,
        string? notes = null,
        CancellationToken cancellationToken = default)
    {
        return await SetActiveVersionAsync(promptVersionId, actorId, notes, "Activated", cancellationToken);
    }

    public async Task<PromptVersionDto> RollbackAsync(
        int promptVersionId,
        string? actorId,
        string? notes = null,
        CancellationToken cancellationToken = default)
    {
        return await SetActiveVersionAsync(promptVersionId, actorId, notes, "Rollback", cancellationToken);
    }

    public async Task DeactivateAsync(
        int promptVersionId,
        string? actorId,
        string? notes = null,
        CancellationToken cancellationToken = default)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await BeginTransactionAsync(db, cancellationToken);
        var version = await db.AiPromptVersions.SingleOrDefaultAsync(x => x.Id == promptVersionId, cancellationToken)
            ?? throw new InvalidOperationException($"Prompt version {promptVersionId} was not found.");
        if (version.Status != "Active")
        {
            throw new InvalidOperationException("Only an active prompt version can be deactivated.");
        }

        version.Status = "Inactive";
        AddAudit(db, version, "Deactivated", actorId, notes, new { version.Status });
        AddAudit(db, version, "CacheInvalidated", actorId, notes, new { version.ContentHash });
        await BumpRevisionAsync(db, actorId, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        await CommitAsync(transaction, cancellationToken);
        InvalidateRevisionCache();
        logger.LogInformation("Prompt version {PromptVersionId} was deactivated by {ActorId}.", promptVersionId, actorId ?? "System");
    }

    public async Task ArchiveAsync(
        int promptVersionId,
        string? actorId,
        string? notes = null,
        CancellationToken cancellationToken = default)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await BeginTransactionAsync(db, cancellationToken);
        var version = await db.AiPromptVersions.SingleOrDefaultAsync(x => x.Id == promptVersionId, cancellationToken)
            ?? throw new InvalidOperationException($"Prompt version {promptVersionId} was not found.");
        if (version.Status == "Active" || version.Status == "Archived")
        {
            throw new InvalidOperationException("Only draft or inactive prompt versions can be archived.");
        }

        version.Status = "Archived";
        AddAudit(db, version, "Archived", actorId, notes, new { version.Status });
        AddAudit(db, version, "CacheInvalidated", actorId, notes, new { version.ContentHash });
        await BumpRevisionAsync(db, actorId, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        await CommitAsync(transaction, cancellationToken);
        InvalidateRevisionCache();
    }

    public Task<IReadOnlyList<EmergencyPromptDto>> GetEmergencyPrompts() => Task.FromResult(emergencyPrompts.GetAll());

    public async Task<long> GetSettingsRevisionAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await GetRevisionAsync(db, cancellationToken);
    }

    private async Task<PromptVersionDto> SetActiveVersionAsync(
        int promptVersionId,
        string? actorId,
        string? notes,
        string action,
        CancellationToken cancellationToken)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await BeginTransactionAsync(db, cancellationToken);
        var target = await db.AiPromptVersions.SingleOrDefaultAsync(x => x.Id == promptVersionId, cancellationToken)
            ?? throw new InvalidOperationException($"Prompt version {promptVersionId} was not found.");
        if (target.Status is "Archived")
        {
            throw new InvalidOperationException("An archived prompt version cannot be activated.");
        }
        if (action == "Rollback" && target.Status != "Inactive")
        {
            throw new InvalidOperationException("Rollback requires a previously inactive prompt version.");
        }

        var current = await db.AiPromptVersions.SingleOrDefaultAsync(
            x => x.PromptModeCode == target.PromptModeCode && x.Status == "Active", cancellationToken);
        if (current?.Id == target.Id)
        {
            return ToDto(target);
        }

        if (current is not null)
        {
            current.Status = "Inactive";
            AddAudit(db, current, "Deactivated", actorId, notes, new { current.Status, ReplacedByPromptVersionId = target.Id });
            await db.SaveChangesAsync(cancellationToken);
        }

        target.Status = "Active";
        AddAudit(db, target, action, actorId, notes, new { target.Status, ReplacedPromptVersionId = current?.Id });
        AddAudit(db, target, "CacheInvalidated", actorId, notes, new { target.ContentHash });
        await BumpRevisionAsync(db, actorId, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        await CommitAsync(transaction, cancellationToken);
        InvalidateRevisionCache();
        logger.LogInformation("Prompt mode {PromptModeCode} activated version {PromptVersionId}; revision advanced.", target.PromptModeCode, target.Id);
        return ToDto(target);
    }

    private async Task<long> GetRevisionAsync(ISubscriptionDbContext db, CancellationToken cancellationToken)
    {
        if (cache.TryGetValue<long>(RevisionCacheKey, out var revision))
        {
            return revision;
        }

        var raw = await db.SystemSettings.AsNoTracking()
            .Where(x => x.Key == SettingsRevisionKey)
            .Select(x => x.Value)
            .SingleOrDefaultAsync(cancellationToken);
        revision = long.TryParse(raw, out var parsed) && parsed >= 0 ? parsed : 0;
        cache.Set(RevisionCacheKey, revision, RevisionPollInterval);
        return revision;
    }

    private async Task BumpRevisionAsync(ISubscriptionDbContext db, string? actorId, CancellationToken cancellationToken)
    {
        if (db.Database.IsRelational())
        {
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO public."SystemSetting" ("Key", "Value", "Description", "ModifiedAt", "ModifiedByUserId")
                VALUES ({SettingsRevisionKey}, '1', 'Monotonic Phase 5 prompt and settings revision.', CURRENT_TIMESTAMP, {actorId})
                ON CONFLICT ("Key") DO UPDATE
                SET "Value" = CASE
                        WHEN public."SystemSetting"."Value" ~ '^[0-9]+$'
                            THEN (public."SystemSetting"."Value"::bigint + 1)::text
                        ELSE '1'
                    END,
                    "ModifiedAt" = CURRENT_TIMESTAMP,
                    "ModifiedByUserId" = EXCLUDED."ModifiedByUserId";
                """, cancellationToken);
            return;
        }

        var setting = await db.SystemSettings.SingleOrDefaultAsync(x => x.Key == SettingsRevisionKey, cancellationToken);
        if (setting is null)
        {
            db.SystemSettings.Add(new SystemSetting
            {
                Key = SettingsRevisionKey,
                Value = "1",
                Description = "Monotonic Phase 5 prompt and settings revision.",
                ModifiedAt = timeProvider.GetUtcNow(),
                ModifiedByUserId = actorId
            });
        }
        else
        {
            var current = long.TryParse(setting.Value, out var value) ? value : 0;
            setting.Value = (current + 1).ToString(System.Globalization.CultureInfo.InvariantCulture);
            setting.ModifiedAt = timeProvider.GetUtcNow();
            setting.ModifiedByUserId = actorId;
        }
    }

    private PromptVersionDto CacheVersion(AiPromptVersion version, long revision)
    {
        var application = configuration["ApplicationName"] ?? hostEnvironment.ApplicationName;
        var key = $"prompt:{application}:{hostEnvironment.EnvironmentName}:{revision}:{version.PromptModeCode}:{version.Id}:{version.ContentHash}:{version.SchemaVersion}";
        return cache.GetOrCreate(key, entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = PromptCacheDuration;
            return ToDto(version);
        })!;
    }

    private void InvalidateRevisionCache() => cache.Remove(RevisionCacheKey);

    private async Task<Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction?> BeginTransactionAsync(
        ISubscriptionDbContext db,
        CancellationToken cancellationToken)
    {
        return db.Database.IsRelational()
            ? await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken)
            : null;
    }

    private static async Task CommitAsync(
        Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction? transaction,
        CancellationToken cancellationToken)
    {
        if (transaction is not null)
        {
            await transaction.CommitAsync(cancellationToken);
        }
    }

    private void AddAudit(
        ISubscriptionDbContext db,
        AiPromptVersion version,
        string action,
        string? actorId,
        string? notes,
        object? metadata)
    {
        db.AiPromptAuditEvents.Add(new AiPromptAuditEvent
        {
            PromptVersionId = version.Id,
            Action = action,
            ActorType = string.IsNullOrWhiteSpace(actorId) ? "System" : "Admin",
            ActorId = actorId,
            OccurredAt = timeProvider.GetUtcNow(),
            Notes = notes,
            ChangeMetadataJson = metadata is null ? null : JsonSerializer.Serialize(metadata)
        });
    }

    private static PromptVersionDto ToDto(AiPromptVersion version) => new(
        version.Id,
        version.PromptModeCode,
        version.VersionLabel,
        version.SystemInstruction,
        version.UserMessageTemplate,
        version.SchemaVersion,
        version.ContentHash,
        version.Status,
        version.CreatedAt,
        version.CreatedByUserId);

    private static void ValidatePromptText(string versionLabel, string systemInstruction, string userMessageTemplate)
    {
        if (string.IsNullOrWhiteSpace(versionLabel) || versionLabel.Trim().Length > 100)
        {
            throw new ArgumentException("Version label is required and must be 100 characters or fewer.", nameof(versionLabel));
        }
        if (string.IsNullOrWhiteSpace(systemInstruction) || string.IsNullOrWhiteSpace(userMessageTemplate))
        {
            throw new ArgumentException("Both system instruction and user message template are required.");
        }

        var placeholders = PlaceholderPattern.Matches(userMessageTemplate)
            .Select(match => match.Groups[1].Value)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        if (placeholders.Any(placeholder => placeholder != "memberText"))
        {
            throw new ArgumentException("The user message template contains an unsupported placeholder.", nameof(userMessageTemplate));
        }
        if (!placeholders.Contains("memberText", StringComparer.Ordinal))
        {
            throw new ArgumentException("The user message template must contain the approved {memberText} placeholder.", nameof(userMessageTemplate));
        }
    }

    private PromptVersionDto? GetEmergencyFallback(string promptModeCode)
    {
        var emergency = emergencyPrompts.Get(promptModeCode);
        if (emergency is null || !emergency.IsValid)
        {
            return null;
        }

        return new PromptVersionDto(
            0,
            emergency.PromptModeCode,
            emergency.VersionLabel,
            emergency.SystemInstruction,
            emergency.UserMessageTemplate,
            emergency.SchemaVersion,
            emergency.ContentHash,
            "Active",
            timeProvider.GetUtcNow(),
            "EmergencyResource",
            IsEmergency: true);
    }
}
