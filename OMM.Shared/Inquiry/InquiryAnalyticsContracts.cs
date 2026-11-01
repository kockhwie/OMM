namespace OMM.Shared.Inquiry;

public sealed record InquiryEntityLinkCapture(
    long SessionId,
    long? TurnId,
    string EntityType,
    string EntityKey,
    string? DisplayName,
    string ResolutionMethod,
    decimal? Confidence,
    bool IsResolved,
    bool UserConfirmed);

public sealed record InquirySelectionCapture(
    long SessionId,
    long? TurnId,
    string EventType,
    string? PurposeCode,
    string? SelectionKey,
    string? RedactedMetadataJson);

public sealed record InquiryOutcomeCapture(
    long SessionId,
    string? FinalRoute,
    int ClarificationCount,
    string? AbandonmentPoint,
    string CompletionStatus,
    bool AlertCreated,
    bool SavedAnalysis,
    bool PathwayCompleted);

public sealed record InquiryTelemetryCapture(
    long? SessionId,
    string? UserId,
    string EventType,
    string? Stage,
    string RedactedPayloadJson);

public interface IInquiryAnalyticsService
{
    Task RecordEntityLinksAsync(IReadOnlyCollection<InquiryEntityLinkCapture> links, CancellationToken cancellationToken = default);
    Task RecordSelectionAsync(InquirySelectionCapture selection, CancellationToken cancellationToken = default);
    Task RecordOutcomeAsync(InquiryOutcomeCapture outcome, CancellationToken cancellationToken = default);
    Task RecordTelemetryAsync(InquiryTelemetryCapture telemetry, CancellationToken cancellationToken = default);
}
