using OMM.Shared.Models.MasterData;

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

public sealed class SignalTemplate(
    string id,
    SignalCategory category,
    string title,
    string description,
    string sampleQuery,
    string defaultSecurity,
    string defaultSector,
    string defaultConcern,
    string defaultScope,
    string defaultObjective,
    bool isActive = true,
    int sortOrder = 0) : AuditableEntity
{
    public string Id { get; set; } = id;
    public SignalCategory Category { get; set; } = category;
    public string Title { get; set; } = title;
    public string Description { get; set; } = description;
    public string SampleQuery { get; set; } = sampleQuery;
    public string DefaultSecurity { get; set; } = defaultSecurity;
    public string DefaultSector { get; set; } = defaultSector;
    public string DefaultConcern { get; set; } = defaultConcern;
    public string DefaultScope { get; set; } = defaultScope;
    public string DefaultObjective { get; set; } = defaultObjective;
    public bool IsActive { get; set; } = isActive;
    public int SortOrder { get; set; } = sortOrder;
}

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
