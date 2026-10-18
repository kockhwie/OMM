namespace OMM.Shared.DecisionSupport;

public static class DecisionPathwayStatus
{
    public const string Open = "Open";
    public const string Reviewed = "Reviewed";
    public const string Dismissed = "Dismissed";
}

public sealed class DecisionPathwayRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string? UserId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public string SignalId { get; set; } = string.Empty;
    public string SignalTitle { get; set; } = string.Empty;
    public string ConcernKey { get; set; } = string.Empty;
    public string ScopeKey { get; set; } = string.Empty;
    public string ObjectiveKey { get; set; } = string.Empty;
    public string SelectedActionKey { get; set; } = string.Empty;
    public decimal? CurrentSharePrice { get; set; }
    public decimal? AnnualDividendPerShare { get; set; }
    public string HorizonLabel { get; set; } = string.Empty;
    public string CheckpointTitle { get; set; } = string.Empty;
    public string CheckpointTriggerCondition { get; set; } = string.Empty;
    public string? Notes { get; set; }

    // Phase 3 Follow-Through Properties
    public DateOnly ReviewDate { get; set; }
    public decimal? TriggerPriceBelow { get; set; }
    public decimal? TriggerPriceAbove { get; set; }
    public string Status { get; set; } = DecisionPathwayStatus.Open;
    public DateTimeOffset? ReviewedAt { get; set; }
    public string? StockCode { get; set; }
}
