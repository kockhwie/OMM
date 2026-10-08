namespace OMM.Shared.DecisionSupport;

/// <summary>
/// Supported signal categories spanning stock, sector, domestic, global, portfolio, and opportunity levels.
/// </summary>
public enum SignalCategory
{
    IndividualStock,
    Sector,
    MalaysianMarket,
    GlobalEconomy,
    PortfolioDecision,
    Opportunity
}

public sealed record SignalTemplate(
    string Id,
    SignalCategory Category,
    string Title,
    string Description,
    string SampleQuery,
    string DefaultSecurity,
    string DefaultSector,
    string DefaultConcern,
    string DefaultScope,
    string DefaultObjective);

public sealed record MemberConcernOption(
    string Key,
    string Title,
    string Description,
    string Icon,
    string Tone);

public sealed record ScopeOption(
    string Key,
    string Title,
    string Description,
    string Icon,
    string Category);

public sealed record ObjectiveOption(
    string Key,
    string Title,
    string Description,
    string Icon,
    string PrimaryBenefit);

public sealed record ImpactTransmissionNode(
    string Stage,
    string Title,
    string Description,
    string Severity,
    string Tone);

public sealed record ImpactMapResult(
    string Summary,
    string DirectEffect,
    string IndirectEffect,
    IReadOnlyList<ImpactTransmissionNode> Nodes,
    IReadOnlyList<string> KeyVulnerabilities,
    IReadOnlyList<string> KeyResilienceFactors);

public sealed record ActionOption(
    string Key,
    string Title,
    string Description,
    string Tag,
    string Tone,
    IReadOnlyList<string> Advantages,
    IReadOnlyList<string> TradeOffs,
    string SuitabilityRationale);

public sealed record ScenarioBranch(
    string Key,
    string Title,
    string Label,
    string Tone,
    string Icon,
    decimal PriceChangePercent,
    decimal TargetSharePrice,
    decimal DividendForecastPerShare,
    decimal EstimatedTotalReturnPercent,
    string Description,
    string KeyAssumption,
    string WhatToMonitor);

public sealed record ScenarioCalculationInput(
    decimal CurrentSharePrice,
    decimal AnnualDividendPerShare,
    int? EstimatedSharesOwned = null,
    string Horizon = "Several months",
    decimal StressAdjustmentPercent = 0);

public sealed record MonitoringCheckpoint(
    string Title,
    string TriggerCondition,
    string ReviewHorizon,
    string MetricToTrack,
    string ActionIfTriggered);

public sealed record FullDecisionPathwayResult(
    SignalTemplate SelectedSignal,
    MemberConcernOption SelectedConcern,
    ScopeOption SelectedScope,
    ObjectiveOption SelectedObjective,
    ImpactMapResult ImpactMap,
    IReadOnlyList<ActionOption> Options,
    string SelectedActionKey,
    IReadOnlyList<ScenarioBranch> Scenarios,
    MonitoringCheckpoint Checkpoint);
