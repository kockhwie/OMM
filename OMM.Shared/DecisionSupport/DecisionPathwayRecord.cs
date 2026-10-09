namespace OMM.Shared.DecisionSupport;

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
}
