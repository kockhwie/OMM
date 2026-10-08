using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace OMM.Shared.DecisionSupport;

public sealed class GoogleAiIntentInterpreter(
    HttpClient httpClient,
    IOptions<GoogleAiOptions> options,
    ILogger<GoogleAiIntentInterpreter> logger) : IInquiryIntentInterpreter
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    private static readonly HashSet<string> GenericCommands = new(StringComparer.OrdinalIgnoreCase)
    {
        "proceed", "next", "continue", "help", "hi", "hello", "hey", "start", "ok", "okay", "yes", "no", "what next", "test", "go", "action", "run"
    };

    private readonly GoogleAiOptions _options = options.Value;

    public async Task<InquiryIntentResult> InterpretAsync(string memberText, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(memberText))
            return Unavailable("Enter a question or signal first.");

        var cleanText = memberText.Trim();
        if (GenericCommands.Contains(cleanText))
        {
            return new InquiryIntentResult(
                IsAvailable: true,
                Summary: "Your input didn't specify a stock or market event. Choose an example topic below or pick from our curated market signals.",
                SignalType: "unknown",
                EventName: null,
                MentionedSecurity: null,
                MentionedSector: null,
                MentionedCountry: null,
                PossibleScopes: ["single_holding", "sector", "portfolio"],
                Intent: "unknown",
                MissingQuestions: ["Which stock or market event would you like to evaluate (e.g. Maybank price drop, US Fed rate hike, or Tenaga dividend)?"],
                Confidence: 0.20m);
        }

        var models = _options.Models.Where(model => !string.IsNullOrWhiteSpace(model)).ToList();
        if (models.Count == 0 && !string.IsNullOrWhiteSpace(_options.Model))
            models.Add(_options.Model);

        if (!_options.Enabled || string.IsNullOrWhiteSpace(_options.ApiKey) || models.Count == 0)
            return Unavailable("AI interpretation is not configured. You can continue with guided questions.");

        var failures = new List<string>();
        foreach (var model in models)
        {
            try
            {
                using var request = new HttpRequestMessage(
                    HttpMethod.Post,
                    $"{_options.BaseUrl.TrimEnd('/')}/models/{Uri.EscapeDataString(model)}:generateContent");
                request.Headers.Add("x-goog-api-key", _options.ApiKey);
                request.Content = JsonContent.Create(BuildRequest(memberText), options: JsonOptions);

                using var response = await httpClient.SendAsync(request, cancellationToken);
                if (!response.IsSuccessStatusCode)
                {
                    var statusCode = (int)response.StatusCode;
                    var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
                    failures.Add($"{model}: HTTP {statusCode}");
                    logger.LogWarning("Google AI model {Model} returned HTTP {StatusCode}: {ErrorBody}; trying the next model.", model, statusCode, errorBody);

                    if (response.StatusCode is System.Net.HttpStatusCode.Unauthorized or System.Net.HttpStatusCode.Forbidden)
                    {
                        break;
                    }

                    continue;
                }

                var envelope = await response.Content.ReadFromJsonAsync<GenerateContentResponse>(JsonOptions, cancellationToken);
                var parts = envelope?.Candidates?.FirstOrDefault()?.Content?.Parts;
                var jsonText = parts is { Count: > 0 }
                    ? string.Concat(parts.Select(p => p.Text))
                    : null;

                if (string.IsNullOrWhiteSpace(jsonText))
                {
                    failures.Add($"{model}: empty response");
                    logger.LogWarning("Google AI model {Model} returned no text; trying the next model.", model);
                    continue;
                }

                jsonText = CleanJson(jsonText);
                var result = JsonSerializer.Deserialize<InquiryIntentPayload>(jsonText, JsonOptions);
                if (result is not null)
                    return ToResult(result);

                logger.LogWarning("Google AI model {Model} returned invalid JSON; trying the next model.", model);
                failures.Add($"{model}: invalid JSON");
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                failures.Add($"{model}: timeout");
                logger.LogWarning("Google AI model {Model} timed out; trying the next model.", model);
            }
            catch (Exception exception)
            {
                failures.Add($"{model}: request error");
                logger.LogWarning(exception, "Google AI model {Model} failed; trying the next model.", model);
            }
        }

        var failureSummary = failures.Count == 0 ? string.Empty : $" ({string.Join("; ", failures)})";
        return Unavailable($"All configured AI models are temporarily unavailable{failureSummary}. You can continue with guided questions.");
    }

    private static string CleanJson(string text)
    {
        var trimmed = text.Trim();
        if (trimmed.StartsWith("```json", StringComparison.OrdinalIgnoreCase))
        {
            trimmed = trimmed[7..].Trim();
        }
        else if (trimmed.StartsWith("```", StringComparison.OrdinalIgnoreCase))
        {
            trimmed = trimmed[3..].Trim();
        }

        if (trimmed.EndsWith("```", StringComparison.OrdinalIgnoreCase))
        {
            trimmed = trimmed[..^3].Trim();
        }

        return trimmed;
    }

    private static object BuildRequest(string memberText) => new
    {
        systemInstruction = new
        {
            parts = new[]
            {
                new { text = "You classify a Malaysian personal-finance member's question about stocks, dividends, sectors, macro events, or portfolio decisions. Extract intent and key entities. If the user message is brief, conversational, or does not clearly name a company, sector, or market catalyst (such as 'what should I do', 'help me decide', 'proceed', 'what next'), DO NOT produce meta-summaries like 'User requested to proceed'. Instead, set signalType to 'unknown', intent to 'unknown', confidence to 0.25, and set summary to 'Your question needs more detail to identify a specific stock or market event.' In missingQuestions, suggest concrete clarifying questions like 'Which stock or market event would you like to evaluate (e.g. Maybank price drop, US Fed rate hike, or Tenaga dividend)?'. Do not give buy, sell, hold, or investment advice. Use null when an entity is absent. Return only JSON matching the schema." }
            }
        },
        contents = new[]
        {
            new
            {
                role = "user",
                parts = new[]
                {
                    new { text = $"Classify this member message:\n\n{memberText.Trim()}" }
                }
            }
        },
        generationConfig = new
        {
            responseMimeType = "application/json",
            responseSchema = new
            {
                type = "object",
                properties = new
                {
                    summary = new { type = "string" },
                    signalType = new { type = "string", @enum = new[] { "stock_event", "economic_event", "sector_event", "market_event", "portfolio_decision", "opportunity", "unknown" } },
                    eventName = new { type = "string", nullable = true },
                    mentionedSecurity = new { type = "string", nullable = true },
                    mentionedSector = new { type = "string", nullable = true },
                    mentionedCountry = new { type = "string", nullable = true },
                    possibleScopes = new { type = "array", items = new { type = "string" } },
                    intent = new { type = "string", @enum = new[] { "understand", "compare_actions", "reduce_risk", "find_opportunity", "monitor", "unknown" } },
                    missingQuestions = new { type = "array", items = new { type = "string" } },
                    confidence = new { type = "number" }
                },
                required = new[] { "summary", "signalType", "eventName", "mentionedSecurity", "mentionedSector", "mentionedCountry", "possibleScopes", "intent", "missingQuestions", "confidence" }
            }
        }
    };

    private static InquiryIntentResult ToResult(InquiryIntentPayload payload) => new(
        true,
        payload.Summary ?? "The signal was interpreted.",
        payload.SignalType ?? "unknown",
        payload.EventName,
        payload.MentionedSecurity,
        payload.MentionedSector,
        payload.MentionedCountry,
        payload.PossibleScopes ?? [],
        payload.Intent ?? "unknown",
        payload.MissingQuestions ?? [],
        Math.Clamp(payload.Confidence, 0, 1));

    private static InquiryIntentResult Unavailable(string message) => new(
        false, string.Empty, "unknown", null, null, null, null, [], "unknown", [], 0, message);

    private sealed class GenerateContentResponse
    {
        public List<GenerateCandidate>? Candidates { get; set; }
    }

    private sealed class GenerateCandidate
    {
        public GenerateContentContent? Content { get; set; }
    }

    private sealed class GenerateContentContent
    {
        public List<GeneratePart>? Parts { get; set; }
    }

    private sealed class GeneratePart
    {
        public string? Text { get; set; }
    }

    private sealed class InquiryIntentPayload
    {
        public string? Summary { get; set; }
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
