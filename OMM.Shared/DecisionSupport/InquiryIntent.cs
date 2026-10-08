namespace OMM.Shared.DecisionSupport;

public interface IInquiryIntentInterpreter
{
    Task<InquiryIntentResult> InterpretAsync(string memberText, CancellationToken cancellationToken = default);
}

public sealed record InquiryIntentResult(
    bool IsAvailable,
    string Summary,
    string SignalType,
    string? EventName,
    string? MentionedSecurity,
    string? MentionedSector,
    string? MentionedCountry,
    IReadOnlyList<string> PossibleScopes,
    string Intent,
    IReadOnlyList<string> MissingQuestions,
    decimal Confidence,
    string? ErrorMessage = null);
