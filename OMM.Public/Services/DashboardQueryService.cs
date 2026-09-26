using OMM.Public.Data.Entities;
using OMM.Public.Models;

namespace OMM.Public.Services;

public sealed class DashboardQueryService(
    DatabaseMineService mineService,
    MemberRecordService recordService,
    IMinerProfileService profileService,
    INotificationService notificationService)
{
    public async Task<DashboardSnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default)
    {
        var profile = await profileService.GetCurrentAsync(cancellationToken);
        var mines = await mineService.GetMinesAsync(cancellationToken);
        var burdens = await recordService.GetBurdensAsync(cancellationToken);
        var income = await recordService.GetIncomeRecordsAsync(cancellationToken);
        var expenses = await recordService.GetExpensesAsync(cancellationToken);
        var goals = await recordService.GetGoalsAsync(cancellationToken);
        await notificationService.GenerateAsync(cancellationToken);
        var notifications = await notificationService.GetAsync(cancellationToken);

        var currencies = mines.Select(item => item.Currency)
            .Concat(burdens.Select(item => item.Currency))
            .Concat(income.Select(item => item.Currency))
            .Concat(expenses.Select(item => item.Currency))
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var comparable = currencies.Count <= 1;
        var currency = currencies.SingleOrDefault() ?? profile?.CurrencyCode ?? string.Empty;

        return new DashboardSnapshot
        {
            Profile = MapProfile(profile),
            Mines = mines,
            Burdens = burdens,
            IncomeRecords = income,
            Expenses = expenses,
            Goals = goals,
            Notifications = notifications,
            Summary = CalculateSummary(mines, burdens, income, expenses, comparable, currency)
        };
    }

    public static DashboardSummary CalculateSummary(
        IReadOnlyList<Mine> mines,
        IReadOnlyList<Burden> burdens,
        IReadOnlyList<IncomeRecord> income,
        IReadOnlyList<Expense> expenses,
        bool comparable,
        string currency)
    {
        if (!comparable)
            return new DashboardSummary { HasMixedCurrencies = true, Currency = string.Empty };

        var totalMines = mines.Sum(item => item.CurrentValue);
        var totalBurdens = burdens.Sum(item => item.Balance);
        var passiveIncome = mines.Sum(item => item.MonthlyIncome) + income
            .Where(item => item.Classification == IncomeClass.PassiveMineGenerated)
            .Sum(item => MemberRecordService.MonthlyEquivalent(item.Amount, ParseFrequency(item.Frequency)));
        var activeIncome = income
            .Where(item => item.Classification == IncomeClass.Active)
            .Sum(item => MemberRecordService.MonthlyEquivalent(item.Amount, ParseFrequency(item.Frequency)));
        var recurringExpenses = expenses.Sum(item => MemberRecordService.MonthlyEquivalent(item.Amount, ParseFrequency(item.Frequency)));
        var growth = mines.Sum(item => item.Growth);
        var purchaseCost = mines.Sum(item => item.PurchaseCost);
        var freedomRatio = recurringExpenses <= 0
            ? passiveIncome > 0 ? 100m : 0m
            : Math.Clamp(passiveIncome / recurringExpenses * 100m, 0m, 100m);

        return new DashboardSummary
        {
            Currency = currency,
            TotalMines = totalMines,
            TotalBurdens = totalBurdens,
            NetWealth = totalMines - totalBurdens,
            PassiveIncomeMonthly = passiveIncome,
            ActiveIncomeMonthly = activeIncome,
            RecurringExpensesMonthly = recurringExpenses,
            FreedomRatio = Math.Round(freedomRatio, 0),
            TotalGrowth = growth,
            TotalGrowthPct = purchaseCost > 0 ? Math.Round(growth / purchaseCost * 100m, 1) : 0m
        };
    }

    private static RecordFrequency ParseFrequency(string frequency) => frequency.ToLowerInvariant() switch
    {
        "annual" => RecordFrequency.Annual,
        "one-off" => RecordFrequency.OneOff,
        _ => RecordFrequency.Monthly
    };

    private static MinerProfile MapProfile(MinerProfileView? profile) => profile is null
        ? new()
        : new MinerProfile
        {
            Name = profile.DisplayName ?? string.Empty,
            Email = profile.Email,
            Country = profile.CountryName ?? string.Empty,
            Currency = profile.CurrencyCode ?? string.Empty,
            Language = profile.Language ?? string.Empty,
            JoinedDate = profile.CreatedAt.ToString("yyyy-MM-dd")
        };
}
