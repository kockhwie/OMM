namespace OMM.Shared.Subscription;

/// <summary>
/// Execution context for AI prompts.
/// Enforces the strict Phase 5 privacy invariant:
/// Member names, email addresses, and account IDs must NEVER be passed to AI providers.
/// </summary>
public sealed record AiExecutionContext
{
    public string RequestedMode { get; init; } = SubscriptionConstants.PromptModes.Standard;
    public string EffectiveMode { get; init; } = SubscriptionConstants.PromptModes.Standard;
    public string DisplayLabel { get; init; } = SubscriptionConstants.DisplayLabels.Standard;
    public bool IsAdvisoryPermitted { get; init; }
    public AiExecutionOrigin Origin { get; init; } = AiExecutionOrigin.Public;
    public bool PersistencePermitted { get; init; } = true;
    public int AiCallAttemptsRemaining { get; init; } = 2;
    public string DisclosureMode { get; init; } = SubscriptionConstants.DisplayLabels.Standard;
    public int? PromptVersionOverrideId { get; init; }
    public string CorrelationId { get; init; } = Guid.NewGuid().ToString("N");
    public string? AnonymizedSessionId { get; init; }

    /// <summary>
    /// Validates that a candidate text payload does not contain member identifiers.
    /// </summary>
    public static void AssertPrivacySafe(string? candidateText, string? userId = null, string? email = null)
    {
        if (string.IsNullOrWhiteSpace(candidateText))
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(userId) && candidateText.Contains(userId, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Privacy violation: candidate AI payload contains member account ID.");
        }

        if (!string.IsNullOrWhiteSpace(email) && candidateText.Contains(email, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Privacy violation: candidate AI payload contains member email address.");
        }
    }
}

public enum AiExecutionOrigin
{
    Public,
    AdminSimulator
}

