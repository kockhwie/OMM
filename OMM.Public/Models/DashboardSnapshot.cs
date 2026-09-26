namespace OMM.Public.Models;

public sealed class DashboardSnapshot
{
    public MinerProfile Profile { get; init; } = new();
    public DashboardSummary Summary { get; init; } = new();
    public IReadOnlyList<Mine> Mines { get; init; } = [];
    public IReadOnlyList<Burden> Burdens { get; init; } = [];
    public IReadOnlyList<IncomeRecord> IncomeRecords { get; init; } = [];
    public IReadOnlyList<Expense> Expenses { get; init; } = [];
    public IReadOnlyList<Goal> Goals { get; init; } = [];
    public IReadOnlyList<Notification> Notifications { get; init; } = [];
}
