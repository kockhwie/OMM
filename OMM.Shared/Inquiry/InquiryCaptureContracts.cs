namespace OMM.Shared.Inquiry;

public sealed record InquirySessionStartRequest(
    string? UserId,
    string EntryPoint,
    string RequestedMode,
    bool IsAnonymous,
    string FlowVersion);

public sealed record InquiryTurnCapture(
    string? DetectedIntent,
    string? EventType,
    string? VerificationStatus,
    decimal? Confidence,
    int? PromptVersionId,
    bool UsedEmergencyPrompt,
    string RequestedMode,
    string EffectiveMode,
    string? FallbackReason,
    int AiCallAttemptCount);

public interface IInquiryCaptureService
{
    Task<long> StartSessionAsync(InquirySessionStartRequest request, CancellationToken cancellationToken = default);
    Task<long> PersistTurnAsync(long sessionId, string? userId, int turnNumber, InquiryTurnCapture turn, string rawMemberMessage, CancellationToken cancellationToken = default);
    Task CompleteSessionAsync(long sessionId, string? userId, CancellationToken cancellationToken = default);
}

public interface IInquiryMessageProtector
{
    bool IsConfigured { get; }
    string Protect(string plaintext);
    string Unprotect(string protectedMessage);
}
