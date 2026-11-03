namespace OMM.Shared.DecisionSupport;

/// <summary>
/// Applies deterministic safety decisions after the provider response has been
/// schema-validated. It does not try to moderate arbitrary prose with regexes;
/// it only operates on the server-owned classification fields and approved terms.
/// </summary>
public static class Phase5InquirySafetyPolicy
{
    private static readonly string[] SensitiveTerms =
    [
        "corruption", "election", "leadership", "resignation", "governance",
        "regulatory", "regulator", "investigation", "misconduct", " bribery"
    ];

    public static SafetyDecision Evaluate(
        string? signalType,
        string? eventName,
        string? mentionedSecurity,
        string? mentionedSector,
        string? mentionedCountry,
        string? intent,
        IReadOnlyList<string> scopes,
        decimal confidence)
    {
        var sensitive = ContainsSensitiveTerm(eventName) ||
                        (string.Equals(signalType, "market_event", StringComparison.Ordinal) &&
                         ContainsSensitiveTerm(eventName));

        var hasEntity = !string.IsNullOrWhiteSpace(mentionedSecurity) ||
                        !string.IsNullOrWhiteSpace(mentionedSector) ||
                        !string.IsNullOrWhiteSpace(mentionedCountry);
        var hasScope = scopes.Any(scope => !string.Equals(scope, "global", StringComparison.OrdinalIgnoreCase));
        var isUnknown = string.Equals(signalType, "unknown", StringComparison.Ordinal) &&
                        string.Equals(intent, "unknown", StringComparison.Ordinal);

        if (isUnknown && !hasEntity && !hasScope && string.IsNullOrWhiteSpace(eventName) && confidence < 0.5m)
        {
            return new SafetyDecision(
                IsAllowed: false,
                IsSensitive: false,
                SanitizedEventName: null,
                Message: "This inquiry needs a stock, sector, country, or market event before analysis can continue.");
        }

        return new SafetyDecision(
            IsAllowed: true,
            IsSensitive: sensitive,
            SanitizedEventName: sensitive ? "Governance or regulatory event" : eventName,
            Message: sensitive
                ? "Sensitive public-interest event. Use official disclosures and verified facts; treat allegations as unverified claims."
                : null);
    }

    private static bool ContainsSensitiveTerm(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return false;
        var normalized = value.Trim();
        return SensitiveTerms.Any(term => normalized.Contains(term.Trim(), StringComparison.OrdinalIgnoreCase));
    }
}

public sealed record SafetyDecision(
    bool IsAllowed,
    bool IsSensitive,
    string? SanitizedEventName,
    string? Message);
