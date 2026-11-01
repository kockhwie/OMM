using System.Text.Json;
using System.Security;
using Microsoft.Extensions.Logging;
using OMM.Shared.Prompts;
using OMM.Shared.Subscription;

namespace OMM.Shared.DecisionSupport;

public sealed class Phase5InquiryClassifier(
    IAiProvider provider,
    IPromptProvider promptProvider,
    EmergencyPromptCatalog emergencyPrompts,
    ILogger<Phase5InquiryClassifier> logger) : IPhase5InquiryClassifier
{
    private const int MaximumMemberTextLength = 20_000;
    private static readonly HashSet<string> AllowedFields = new(StringComparer.Ordinal)
    {
        "signalType", "eventName", "mentionedSecurity", "mentionedSector", "mentionedCountry",
        "possibleScopes", "intent", "missingQuestions", "confidence"
    };

    private static readonly object ResponseSchema = new
    {
        type = "object",
        properties = new
        {
            signalType = new { type = "string", @enum = new[] { "stock_event", "economic_event", "sector_event", "market_event", "portfolio_decision", "opportunity", "unknown" } },
            eventName = new { type = "string", nullable = true },
            mentionedSecurity = new { type = "string", nullable = true },
            mentionedSector = new { type = "string", nullable = true },
            mentionedCountry = new { type = "string", nullable = true },
            possibleScopes = new { type = "array", items = new { type = "string", @enum = new[] { "global", "country", "malaysia", "sector", "stock", "portfolio" } } },
            intent = new { type = "string", @enum = new[] { "explain", "assess_impact", "check_portfolio", "explore_opportunity", "compare_scenarios", "monitor", "request_advice", "unknown" } },
            missingQuestions = new { type = "array", items = new { type = "string" } },
            confidence = new { type = "number" }
        },
        required = new[] { "signalType", "eventName", "mentionedSecurity", "mentionedSector", "mentionedCountry", "possibleScopes", "intent", "missingQuestions", "confidence" }
    };

    public async Task<InquiryIntentResult> ClassifyAsync(
        string memberText,
        AiExecutionContext executionContext,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(executionContext);
        if (string.IsNullOrWhiteSpace(memberText)) return Unavailable("Enter a question or signal first.");
        if (memberText.Length > MaximumMemberTextLength) return Unavailable("The inquiry is too long to classify.");
        if (executionContext.EffectiveMode is not (SubscriptionConstants.PromptModes.Standard or SubscriptionConstants.PromptModes.Pro))
            return Unavailable("The requested analysis mode is not available.");
        if (executionContext.RequestedMode is not (SubscriptionConstants.PromptModes.Standard or SubscriptionConstants.PromptModes.Pro) ||
            executionContext.Origin == AiExecutionOrigin.Public && executionContext.PromptVersionOverrideId.HasValue)
            return Unavailable("The server resolved an invalid AI execution context.");
        if (executionContext.IsAdvisoryPermitted != (executionContext.EffectiveMode == SubscriptionConstants.PromptModes.Pro))
            return Unavailable("The server resolved an inconsistent analysis context.");
        if (executionContext.EffectiveMode == SubscriptionConstants.PromptModes.Pro && executionContext.RequestedMode != SubscriptionConstants.PromptModes.Pro)
            return Unavailable("Pro analysis requires an explicit server-resolved Pro request.");

        PromptVersionDto? prompt = null;
        try
        {
            prompt = executionContext.Origin == AiExecutionOrigin.AdminSimulator && executionContext.PromptVersionOverrideId.HasValue
                ? await promptProvider.GetVersionAsync(executionContext.PromptVersionOverrideId.Value, cancellationToken)
                : await promptProvider.GetActivePromptAsync(executionContext.EffectiveMode, cancellationToken);
        }
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(exception, "Active Phase 5 prompt lookup failed for mode {Mode} and correlation {CorrelationId}; using emergency baseline.",
                executionContext.EffectiveMode, executionContext.CorrelationId);
        }
        var isEmergency = prompt is null;
        if (isEmergency)
        {
            var emergency = emergencyPrompts.GetAll().SingleOrDefault(item => item.PromptModeCode == executionContext.EffectiveMode && item.IsValid);
            if (emergency is null) return Unavailable("No validated prompt is available. Continue with guided questions.");
            prompt = new PromptVersionDto(0, emergency.PromptModeCode, emergency.VersionLabel, emergency.SystemInstruction,
                emergency.UserMessageTemplate, emergency.SchemaVersion, emergency.ContentHash, "Emergency",
                DateTimeOffset.UtcNow, null, IsEmergency: true);
        }

        if (prompt is null) return Unavailable("No validated prompt is available. Continue with guided questions.");
        if (!string.Equals(prompt.PromptModeCode, executionContext.EffectiveMode, StringComparison.OrdinalIgnoreCase))
            return Unavailable("The selected prompt does not match the execution mode.", prompt.Id, isEmergency);

        if (prompt.SchemaVersion != "v1") return Unavailable("The active response schema is unsupported.", prompt.Id, isEmergency);

        logger.LogInformation("Resolved Phase 5 prompt mode {PromptMode}, version {PromptVersionId}, emergency {IsEmergency}, correlation {CorrelationId}.",
            executionContext.EffectiveMode, prompt.Id, isEmergency, executionContext.CorrelationId);
        if (!prompt.UserMessageTemplate.Contains("{memberText}", StringComparison.Ordinal) ||
            prompt.UserMessageTemplate.Replace("{memberText}", string.Empty, StringComparison.Ordinal).Contains('{') ||
            prompt.UserMessageTemplate.Replace("{memberText}", string.Empty, StringComparison.Ordinal).Contains('}'))
            return Unavailable("The active prompt contains unsupported placeholders.", prompt.Id, isEmergency);

        var safeText = SecurityElement.Escape(NormalizeMemberText(memberText)) ?? string.Empty;
        var userContent = prompt.UserMessageTemplate.Replace("{memberText}", $"<memberText>{safeText}</memberText>", StringComparison.Ordinal);

        var payload = await provider.GenerateJsonAsync(new AiProviderRequest(
            prompt.SystemInstruction,
            userContent,
            ResponseSchema,
            prompt.SchemaVersion,
            executionContext.CorrelationId,
            executionContext.AiCallAttemptsRemaining), cancellationToken);
        if (!payload.IsAvailable || string.IsNullOrWhiteSpace(payload.Json))
            return Unavailable(payload.ErrorMessage ?? "AI classification is unavailable. Continue with guided questions.", prompt.Id, isEmergency, payload.AttemptCount);

        try
        {
            using var json = JsonDocument.Parse(payload.Json);
            if (json.RootElement.ValueKind != JsonValueKind.Object ||
                json.RootElement.EnumerateObject().Count() != AllowedFields.Count ||
                json.RootElement.EnumerateObject().Any(property => !AllowedFields.Contains(property.Name)))
                return Unavailable("The AI response did not match the approved classification contract.", prompt.Id, isEmergency, payload.AttemptCount);

            var classification = JsonSerializer.Deserialize<ClassificationPayload>(payload.Json, new JsonSerializerOptions(JsonSerializerDefaults.Web)
            {
                PropertyNameCaseInsensitive = true
            });
            if (classification is null || classification.PossibleScopes is null || classification.MissingQuestions is null ||
                !IsAllowedSignal(classification.SignalType) || !IsAllowedIntent(classification.Intent) ||
                classification.Confidence is < 0m or > 1m || (classification.MissingQuestions?.Count ?? 0) > 1)
                return Unavailable("The AI response failed server validation. Continue with guided questions.", prompt.Id, isEmergency, payload.AttemptCount);

            return new InquiryIntentResult(
                true,
                "Your inquiry is ready for guided review.",
                classification.SignalType!,
                Clean(classification.EventName, 160),
                Clean(classification.MentionedSecurity, 120),
                Clean(classification.MentionedSector, 120),
                Clean(classification.MentionedCountry, 100),
                (classification.PossibleScopes ?? []).Where(IsAllowedScope).Distinct(StringComparer.Ordinal).Take(6).ToArray(),
                classification.Intent!,
                (classification.MissingQuestions ?? []).Select(value => Clean(value, 240)).Where(value => value is not null).Cast<string>().Take(1).ToArray(),
                classification.Confidence,
                PromptVersionId: prompt.Id == 0 ? null : prompt.Id,
                UsedEmergencyPrompt: isEmergency,
                AiCallAttemptCount: payload.AttemptCount);
        }
        catch (JsonException exception)
        {
            logger.LogWarning(exception, "AI classification JSON failed validation for correlation {CorrelationId}.", executionContext.CorrelationId);
            return Unavailable("The AI response was invalid. Continue with guided questions.", prompt.Id, isEmergency, payload.AttemptCount);
        }
    }

    private static string NormalizeMemberText(string text) => new(text.Trim().Where(character => !char.IsControl(character) || character is '\r' or '\n' or '\t').ToArray());
    private static string? Clean(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var clean = new string(value.Where(character => !char.IsControl(character)).ToArray()).Trim();
        return clean.Length == 0 ? null : clean[..Math.Min(clean.Length, maxLength)];
    }
    private static bool IsAllowedSignal(string? value) => value is "stock_event" or "economic_event" or "sector_event" or "market_event" or "portfolio_decision" or "opportunity" or "unknown";
    private static bool IsAllowedIntent(string? value) => value is "explain" or "assess_impact" or "check_portfolio" or "explore_opportunity" or "compare_scenarios" or "monitor" or "request_advice" or "unknown";
    private static bool IsAllowedScope(string value) => value is "global" or "country" or "malaysia" or "sector" or "stock" or "portfolio";
    private static InquiryIntentResult Unavailable(string message, int? promptId = null, bool emergency = false, int attempts = 0) =>
        new(false, string.Empty, "unknown", null, null, null, null, [], "unknown", [], 0m, message,
            promptId is > 0 ? promptId : null, emergency, attempts);

    private sealed class ClassificationPayload
    {
        public string? SignalType { get; set; }
        public string? EventName { get; set; }
        public string? MentionedSecurity { get; set; }
        public string? MentionedSector { get; set; }
        public string? MentionedCountry { get; set; }
        public List<string>? PossibleScopes { get; set; }
        public string? Intent { get; set; }
        public List<string>? MissingQuestions { get; set; }
        public decimal Confidence { get; set; }
    }
}
