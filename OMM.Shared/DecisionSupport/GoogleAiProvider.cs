using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace OMM.Shared.DecisionSupport;

public sealed class GoogleAiProvider(
    HttpClient httpClient,
    IOptions<GoogleAiOptions> options,
    ILogger<GoogleAiProvider> logger) : IAiProvider
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly GoogleAiOptions _options = options.Value;

    public async Task<AiProviderResult> GenerateJsonAsync(AiProviderRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!_options.Enabled || string.IsNullOrWhiteSpace(_options.ApiKey))
            return new(false, null, 0, "AI provider is not configured.");

        var attemptLimit = Math.Clamp(request.MaximumAttempts, 0, 2);
        if (attemptLimit == 0) return new(false, null, 0, "The inquiry AI-call budget is exhausted.");
        var models = _options.Models.Where(model => !string.IsNullOrWhiteSpace(model)).Take(attemptLimit).ToList();
        if (models.Count == 0 && !string.IsNullOrWhiteSpace(_options.Model)) models.Add(_options.Model);
        if (models.Count == 0) return new(false, null, 0, "No AI models are configured.");

        var attempts = 0;
        foreach (var model in models)
        {
            attempts++;
            try
            {
                using var message = new HttpRequestMessage(
                    HttpMethod.Post,
                    $"{_options.BaseUrl.TrimEnd('/')}/models/{Uri.EscapeDataString(model)}:generateContent");
                message.Headers.Add("x-goog-api-key", _options.ApiKey);
                message.Headers.Add("x-omm-correlation-id", request.CorrelationId);
                message.Content = JsonContent.Create(new
                {
                    systemInstruction = new { parts = new[] { new { text = request.SystemInstruction } } },
                    contents = new[] { new { role = "user", parts = new[] { new { text = request.UserContent } } } },
                    generationConfig = new
                    {
                        responseMimeType = "application/json",
                        responseSchema = request.ResponseSchema
                    }
                }, options: JsonOptions);

                using var response = await httpClient.SendAsync(message, cancellationToken);
                if (!response.IsSuccessStatusCode)
                {
                    logger.LogWarning("AI provider model {Model} returned HTTP {StatusCode} for correlation {CorrelationId}.",
                        model, (int)response.StatusCode, request.CorrelationId);
                    if (response.StatusCode is System.Net.HttpStatusCode.Unauthorized or System.Net.HttpStatusCode.Forbidden) break;
                    continue;
                }

                var envelope = await response.Content.ReadFromJsonAsync<GenerateContentResponse>(JsonOptions, cancellationToken);
                var json = envelope?.Candidates?.FirstOrDefault()?.Content?.Parts is { Count: > 0 } parts
                    ? string.Concat(parts.Select(part => part.Text))
                    : null;
                if (!string.IsNullOrWhiteSpace(json)) return new(true, CleanJson(json), attempts);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                logger.LogWarning("AI provider model {Model} timed out for correlation {CorrelationId}.", model, request.CorrelationId);
            }
            catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
            {
                logger.LogWarning(exception, "AI provider model {Model} failed for correlation {CorrelationId}.", model, request.CorrelationId);
            }
        }

        return new(false, null, attempts, "Configured AI models are temporarily unavailable.");
    }

    private static string CleanJson(string text)
    {
        var value = text.Trim();
        if (value.StartsWith("```json", StringComparison.OrdinalIgnoreCase)) value = value[7..].Trim();
        else if (value.StartsWith("```", StringComparison.OrdinalIgnoreCase)) value = value[3..].Trim();
        if (value.EndsWith("```", StringComparison.OrdinalIgnoreCase)) value = value[..^3].Trim();
        return value;
    }

    private sealed class GenerateContentResponse
    {
        public List<Candidate>? Candidates { get; set; }
    }
    private sealed class Candidate { public Content? Content { get; set; } }
    private sealed class Content { public List<Part>? Parts { get; set; } }
    private sealed class Part { public string? Text { get; set; } }
}
