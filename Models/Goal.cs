namespace omm.Models;

public enum GoalType
{
    Safety,
    WealthBuilding,
    DebtReduction,
    Income,
    Retirement,
    Freedom,
    MajorPurchase,
    Habits
}

public class Goal
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public GoalType Type { get; set; }
    public decimal Target { get; set; }
    public decimal Current { get; set; }
    public string TargetDate { get; set; } = string.Empty;
    public string Status { get; set; } = "on-track"; // on-track, needs-attention, not-started, achieved
    public List<string>? LinkedMineIds { get; set; }
}
