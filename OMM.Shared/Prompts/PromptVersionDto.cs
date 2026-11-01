using OMM.Shared.Models.Phase5;

namespace OMM.Shared.Prompts;

public sealed record PromptVersionDto(
    int Id,
    string PromptModeCode,
    string VersionLabel,
    string SystemInstruction,
    string UserMessageTemplate,
    string SchemaVersion,
    string ContentHash,
    string Status,
    DateTimeOffset CreatedAt,
    string? CreatedByUserId,
    bool IsEmergency = false);

public sealed record PromptDraftInput(
    string PromptModeCode,
    string VersionLabel,
    string SystemInstruction,
    string UserMessageTemplate,
    string? Notes);

public sealed record EmergencyPromptDto(
    string PromptModeCode,
    string VersionLabel,
    string SystemInstruction,
    string UserMessageTemplate,
    string SchemaVersion,
    string ContentHash,
    bool IsValid);

public sealed record PromptResolutionResult(
    PromptVersionDto? Prompt,
    string EffectiveMode,
    bool IsFallback,
    string? FallbackReason,
    bool IsUnavailable);

public interface IPromptProvider
{
    Task<PromptVersionDto?> GetActivePromptAsync(string promptModeCode, CancellationToken cancellationToken = default);
    Task<PromptVersionDto?> GetVersionAsync(int promptVersionId, CancellationToken cancellationToken = default);
    Task<PromptResolutionResult> ResolvePromptAsync(
        string requestedMode,
        bool proEnabled = true,
        int? overrideVersionId = null,
        CancellationToken cancellationToken = default);
}

public interface IPromptAdministrationService
{
    Task<IReadOnlyList<PromptVersionDto>> GetVersionsAsync(string promptModeCode, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AiPromptAuditEventDto>> GetAuditEventsAsync(string promptModeCode, int take = 100, CancellationToken cancellationToken = default);
    Task<PromptVersionDto> CreateDraftAsync(PromptDraftInput input, string? actorId, CancellationToken cancellationToken = default);
    Task<PromptVersionDto> ActivateAsync(int promptVersionId, string? actorId, string? notes = null, CancellationToken cancellationToken = default);
    Task DeactivateAsync(int promptVersionId, string? actorId, string? notes = null, CancellationToken cancellationToken = default);
    Task<PromptVersionDto> RollbackAsync(int promptVersionId, string? actorId, string? notes = null, CancellationToken cancellationToken = default);
    Task ArchiveAsync(int promptVersionId, string? actorId, string? notes = null, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<EmergencyPromptDto>> GetEmergencyPrompts();
    Task<long> GetSettingsRevisionAsync(CancellationToken cancellationToken = default);
}

public sealed record AiPromptAuditEventDto(
    long Id,
    int PromptVersionId,
    string PromptModeCode,
    string Action,
    string ActorType,
    string? ActorId,
    DateTimeOffset OccurredAt,
    string? Notes,
    string? ChangeMetadataJson);
