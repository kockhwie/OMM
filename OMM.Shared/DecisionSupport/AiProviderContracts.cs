namespace OMM.Shared.DecisionSupport;

public sealed record AiProviderRequest(
    string SystemInstruction,
    string UserContent,
    object ResponseSchema,
    string SchemaVersion,
    string CorrelationId,
    int MaximumAttempts = 2);

public sealed record AiProviderResult(
    bool IsAvailable,
    string? Json,
    int AttemptCount,
    string? ErrorMessage = null);

/// <summary>Provider boundary for structured Phase 5 AI calls.</summary>
public interface IAiProvider
{
    Task<AiProviderResult> GenerateJsonAsync(AiProviderRequest request, CancellationToken cancellationToken = default);
}

public interface IPhase5InquiryClassifier
{
    Task<InquiryIntentResult> ClassifyAsync(
        string memberText,
        OMM.Shared.Subscription.AiExecutionContext executionContext,
        CancellationToken cancellationToken = default);
}
