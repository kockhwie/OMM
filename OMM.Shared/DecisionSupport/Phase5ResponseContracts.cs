namespace OMM.Shared.DecisionSupport;

public sealed record Phase5RiskContext(
    string TimeHorizon, string LiquidityNeed, string LossTolerance,
    string KnowledgeLevel, bool HasPortfolioData, DateTimeOffset CapturedAt);

public sealed record Phase5ScenarioBranch(
    string Key, string Label, string Assumption,
    string ExpectedTransmission, string Uncertainty);

public sealed record StandardEducationalResponse(
    string Title, string Summary, IReadOnlyList<string> Assumptions,
    IReadOnlyList<string> Risks, IReadOnlyList<string> Opportunities,
    IReadOnlyList<Phase5ScenarioBranch> Scenarios, string Disclosure);

public sealed record ProAdviceResponse(
    string Summary, IReadOnlyList<string> PersonalizedConsiderations,
    IReadOnlyList<string> Risks, IReadOnlyList<Phase5ScenarioBranch> Scenarios,
    string Disclosure, bool IsValidated = false);

public static class Phase5ResponseContracts
{
    public static bool ValidateStandard(StandardEducationalResponse response)
    {
        ArgumentNullException.ThrowIfNull(response);
        return response.Disclosure == "Educational information only" &&
               response.Assumptions.Count <= 8 && response.Risks.Count <= 8 &&
               response.Opportunities.Count <= 8 && response.Scenarios.Count <= 6 &&
               response.Scenarios.All(IsValidScenario);
    }

    public static ProAdviceResponse ValidatePro(ProAdviceResponse response, bool advisoryPermitted, Phase5RiskContext? riskContext)
    {
        ArgumentNullException.ThrowIfNull(response);
        var valid = advisoryPermitted && riskContext is not null &&
            response.Disclosure == "Personalized Pro Advice" &&
            !string.IsNullOrWhiteSpace(response.Summary) && response.Scenarios.Count <= 6 &&
            response.Scenarios.All(IsValidScenario);
        return response with { IsValidated = valid };
    }

    public static StandardEducationalResponse RenderStandard(InquiryIntentResult classification, ImpactEvaluationResult? impact, Phase5RiskContext? riskContext)
    {
        ArgumentNullException.ThrowIfNull(classification);
        var eventLabel = string.IsNullOrWhiteSpace(classification.EventName) ? "the identified topic" : classification.EventName;
        var rules = impact?.MatchedRules ?? [];
        var scenarios = rules.Take(3).Select((rule, index) => new Phase5ScenarioBranch(
            $"rule-{rule.Id}", index == 0 ? "Configured pathway" : $"Alternative pathway {index + 1}",
            $"If {rule.SourceEntityKey} remains relevant to this event", rule.TransmissionMechanism,
            $"Direction: {rule.Direction}; strength: {rule.Strength}; horizon: {rule.TimeHorizon}")).ToArray();
        var response = new StandardEducationalResponse(
            $"Review of {eventLabel}",
            "This is a structured explanation of the configured market relationship. It does not determine an investment action.",
            ["The event classification is an unverified interpretation of the inquiry.", riskContext is null ? "No personal risk context was provided." : $"Time horizon: {riskContext.TimeHorizon}."],
            rules.Take(4).Select(rule => $"{rule.Direction} effect through {rule.TransmissionMechanism}; strength {rule.Strength}.").ToArray(),
            rules.Take(4).Select(rule => $"Monitor the {rule.TargetEntityType} exposure described by the configured {rule.TimeHorizon} pathway.").ToArray(),
            scenarios, "Educational information only");
        return ValidateStandard(response) ? response : response with { Scenarios = [] };
    }

    private static bool IsValidScenario(Phase5ScenarioBranch scenario) =>
        !string.IsNullOrWhiteSpace(scenario.Key) && !string.IsNullOrWhiteSpace(scenario.Label) &&
        !string.IsNullOrWhiteSpace(scenario.Assumption) && !string.IsNullOrWhiteSpace(scenario.ExpectedTransmission) &&
        !string.IsNullOrWhiteSpace(scenario.Uncertainty);
}
