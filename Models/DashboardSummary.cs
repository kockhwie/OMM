namespace omm.Models;

public class DashboardSummary
{
    public decimal TotalMines { get; set; }
    public decimal TotalBurdens { get; set; }
    public decimal NetWealth { get; set; }
    public decimal PassiveIncomeMonthly { get; set; }
    public decimal ActiveIncomeMonthly { get; set; }
    public decimal RecurringExpensesMonthly { get; set; }
    public decimal FreedomRatio { get; set; }
    public decimal TotalGrowth { get; set; }
    public decimal TotalGrowthPct { get; set; }
}
