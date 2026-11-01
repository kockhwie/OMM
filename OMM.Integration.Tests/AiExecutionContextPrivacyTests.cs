using OMM.Shared.Subscription;

namespace OMM.Integration.Tests;

public class AiExecutionContextPrivacyTests
{
    [Fact]
    public void AiExecutionContext_defaults_to_privacy_safe_fields()
    {
        var context = new AiExecutionContext();

        Assert.Equal("STANDARD", context.EffectiveMode);
        Assert.Equal("Educational information only", context.DisplayLabel);
        Assert.False(context.IsAdvisoryPermitted);
        Assert.False(string.IsNullOrWhiteSpace(context.CorrelationId));
        Assert.Null(context.AnonymizedSessionId);
    }

    [Fact]
    public void AssertPrivacySafe_throws_when_account_id_is_in_candidate_text()
    {
        var userId = "user-uuid-12345";
        var payload = $"Analysis for member {userId} with risk profile high";

        var ex = Assert.Throws<InvalidOperationException>(() =>
            AiExecutionContext.AssertPrivacySafe(payload, userId: userId));

        Assert.Contains("contains member account ID", ex.Message);
    }

    [Fact]
    public void AssertPrivacySafe_throws_when_email_is_in_candidate_text()
    {
        var email = "investor@example.com";
        var payload = $"Report generated for investor@example.com regarding stocks";

        var ex = Assert.Throws<InvalidOperationException>(() =>
            AiExecutionContext.AssertPrivacySafe(payload, email: email));

        Assert.Contains("contains member email address", ex.Message);
    }

    [Fact]
    public void AssertPrivacySafe_succeeds_when_no_pii_is_present()
    {
        var payload = "General analysis for banking sector with 4.5% dividend yield";

        // Should not throw
        AiExecutionContext.AssertPrivacySafe(payload, userId: "user-123", email: "user@example.com");
    }
}

