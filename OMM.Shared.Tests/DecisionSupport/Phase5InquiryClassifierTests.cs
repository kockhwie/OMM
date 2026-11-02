using Microsoft.Extensions.Logging.Abstractions;
using OMM.Shared.DecisionSupport;
using OMM.Shared.Prompts;
using OMM.Shared.Subscription;
using Xunit;

namespace OMM.Shared.Tests.DecisionSupport;

public class Phase5InquiryClassifierTests
{
    private static readonly EmergencyPromptCatalog EmergencyPrompts = new();

    private sealed class FakeAiProvider(AiProviderResult result) : IAiProvider
    {
        public AiProviderRequest? LastRequest { get; private set; }

        public Task<AiProviderResult> GenerateJsonAsync(AiProviderRequest request, CancellationToken cancellationToken = default)
        {
            LastRequest = request;
            return Task.FromResult(result);
        }
    }

    private sealed class FakePromptProvider(PromptVersionDto? activePrompt) : IPromptProvider
    {
        public Task<PromptVersionDto?> GetActivePromptAsync(string promptModeCode, CancellationToken cancellationToken = default)
            => Task.FromResult(activePrompt);

        public Task<PromptVersionDto?> GetVersionAsync(int promptVersionId, CancellationToken cancellationToken = default)
            => Task.FromResult(activePrompt);

        public Task<PromptResolutionResult> ResolvePromptAsync(string requestedMode, bool proEnabled = true, int? overrideVersionId = null, CancellationToken cancellationToken = default)
            => Task.FromResult(new PromptResolutionResult(activePrompt, activePrompt?.PromptModeCode ?? requestedMode, false, null, false));
    }

    private static AiExecutionContext CreateContext(
        string mode = SubscriptionConstants.PromptModes.Standard,
        AiExecutionOrigin origin = AiExecutionOrigin.Public,
        int? overrideVersionId = null) => new()
    {
        RequestedMode = mode,
        EffectiveMode = mode,
        DisplayLabel = mode == SubscriptionConstants.PromptModes.Pro ? SubscriptionConstants.DisplayLabels.Pro : SubscriptionConstants.DisplayLabels.Standard,
        DisclosureMode = mode == SubscriptionConstants.PromptModes.Pro ? SubscriptionConstants.DisplayLabels.Pro : SubscriptionConstants.DisplayLabels.Standard,
        IsAdvisoryPermitted = mode == SubscriptionConstants.PromptModes.Pro,
        Origin = origin,
        PersistencePermitted = true,
        PromptVersionOverrideId = overrideVersionId,
        AnonymizedSessionId = "anon-sess-001",
        AiCallAttemptsRemaining = 2
    };

    [Fact]
    public async Task ClassifyAsync_ValidPayload_ReturnsSuccessfulClassification()
    {
        var validJson = """
        {
            "signalType": "economic_event",
            "eventName": "US Rate Hike",
            "mentionedSecurity": null,
            "mentionedSector": "Banking",
            "mentionedCountry": "Malaysia",
            "possibleScopes": ["country", "sector"],
            "intent": "assess_impact",
            "missingQuestions": ["Are you reviewing short-term or long-term impact?"],
            "confidence": 0.88
        }
        """;

        var fakeAi = new FakeAiProvider(new AiProviderResult(true, validJson, 1));
        var fakePrompt = new FakePromptProvider(new PromptVersionDto(
            1, SubscriptionConstants.PromptModes.Standard, "Standard v1", "System instructions",
            "Analyze: {memberText}", "v1", "hash1", "Active", DateTimeOffset.UtcNow, null));

        var classifier = new Phase5InquiryClassifier(fakeAi, fakePrompt, EmergencyPrompts, NullLogger<Phase5InquiryClassifier>.Instance);

        var result = await classifier.ClassifyAsync("How do US rates affect Malaysian banks?", CreateContext());

        Assert.True(result.IsAvailable);
        Assert.Equal("economic_event", result.SignalType);
        Assert.Equal("US Rate Hike", result.EventName);
        Assert.Equal("Banking", result.MentionedSector);
        Assert.Equal("Malaysia", result.MentionedCountry);
        Assert.Equal("assess_impact", result.Intent);
        Assert.Equal(0.88m, result.Confidence);
        Assert.Single(result.MissingQuestions);
        Assert.Equal(1, result.PromptVersionId);
        Assert.False(result.UsedEmergencyPrompt);
    }

    [Fact]
    public async Task ClassifyAsync_ExceedsMaxLength_ReturnsUnavailable()
    {
        var fakeAi = new FakeAiProvider(new AiProviderResult(true, "{}", 0));
        var fakePrompt = new FakePromptProvider(null);
        var classifier = new Phase5InquiryClassifier(fakeAi, fakePrompt, EmergencyPrompts, NullLogger<Phase5InquiryClassifier>.Instance);

        var longText = new string('x', 20_001);
        var result = await classifier.ClassifyAsync(longText, CreateContext());

        Assert.False(result.IsAvailable);
        Assert.Contains("too long", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ClassifyAsync_EmptyText_ReturnsUnavailable()
    {
        var fakeAi = new FakeAiProvider(new AiProviderResult(true, "{}", 0));
        var fakePrompt = new FakePromptProvider(null);
        var classifier = new Phase5InquiryClassifier(fakeAi, fakePrompt, EmergencyPrompts, NullLogger<Phase5InquiryClassifier>.Instance);

        var result = await classifier.ClassifyAsync("   ", CreateContext());

        Assert.False(result.IsAvailable);
    }

    [Fact]
    public async Task ClassifyAsync_PublicOriginWithPromptOverride_RejectsAsInvalidContext()
    {
        var fakeAi = new FakeAiProvider(new AiProviderResult(true, "{}", 0));
        var fakePrompt = new FakePromptProvider(null);
        var classifier = new Phase5InquiryClassifier(fakeAi, fakePrompt, EmergencyPrompts, NullLogger<Phase5InquiryClassifier>.Instance);

        var context = CreateContext(origin: AiExecutionOrigin.Public, overrideVersionId: 99);
        var result = await classifier.ClassifyAsync("Test query", context);

        Assert.False(result.IsAvailable);
        Assert.Contains("invalid AI execution context", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ClassifyAsync_WhenPromptProviderThrows_FallsBackToEmergencyPrompt()
    {
        var validJson = """
        {
            "signalType": "stock_event",
            "eventName": "Quarterly Earnings",
            "mentionedSecurity": "MAYBANK",
            "mentionedSector": null,
            "mentionedCountry": "Malaysia",
            "possibleScopes": ["stock"],
            "intent": "explain",
            "missingQuestions": [],
            "confidence": 0.95
        }
        """;

        var fakeAi = new FakeAiProvider(new AiProviderResult(true, validJson, 1));
        var throwingPromptProvider = new ThrowingPromptProvider();
        var classifier = new Phase5InquiryClassifier(fakeAi, throwingPromptProvider, EmergencyPrompts, NullLogger<Phase5InquiryClassifier>.Instance);

        var result = await classifier.ClassifyAsync("Maybank earnings report", CreateContext());

        Assert.True(result.IsAvailable);
        Assert.True(result.UsedEmergencyPrompt);
        Assert.Null(result.PromptVersionId);
    }

    [Fact]
    public async Task ClassifyAsync_EscapesHtmlAndXmlTagsInUserContent()
    {
        var validJson = """
        {
            "signalType": "unknown",
            "eventName": null,
            "mentionedSecurity": null,
            "mentionedSector": null,
            "mentionedCountry": null,
            "possibleScopes": [],
            "intent": "unknown",
            "missingQuestions": [],
            "confidence": 0.5
        }
        """;

        var fakeAi = new FakeAiProvider(new AiProviderResult(true, validJson, 1));
        var fakePrompt = new FakePromptProvider(new PromptVersionDto(
            1, SubscriptionConstants.PromptModes.Standard, "Standard v1", "System",
            "Inquiry: {memberText}", "v1", "hash1", "Active", DateTimeOffset.UtcNow, null));

        var classifier = new Phase5InquiryClassifier(fakeAi, fakePrompt, EmergencyPrompts, NullLogger<Phase5InquiryClassifier>.Instance);

        await classifier.ClassifyAsync("<script>alert('xss')</script> & \"quotes\"", CreateContext());

        Assert.NotNull(fakeAi.LastRequest);
        Assert.Contains("&lt;script&gt;alert(&apos;xss&apos;)&lt;/script&gt; &amp; &quot;quotes&quot;", fakeAi.LastRequest.UserContent);
        Assert.DoesNotContain("<script>", fakeAi.LastRequest.UserContent);
    }

    private sealed class ThrowingPromptProvider : IPromptProvider
    {
        public Task<PromptVersionDto?> GetActivePromptAsync(string promptModeCode, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("Simulated database failure");

        public Task<PromptVersionDto?> GetVersionAsync(int promptVersionId, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("Simulated database failure");

        public Task<PromptResolutionResult> ResolvePromptAsync(string requestedMode, bool proEnabled = true, int? overrideVersionId = null, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("Simulated database failure");
    }
}

