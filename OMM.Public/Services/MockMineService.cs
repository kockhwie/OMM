using OMM.Public.Models;

namespace OMM.Public.Services;

public class MockMineService : IMineService
{
    private readonly MinerProfile _profile = new()
    {
        Name = "Jason Goh",
        Email = "jason.goh@codingdinos.com",
        Country = "Malaysia",
        Currency = "MYR",
        Language = "English",
        JoinedDate = "2026-01-15"
    };

    private readonly List<Mine> _mines =
    [
        new()
        {
            Id = "mine-epf",
            Name = "EPF / KWSP",
            Category = MineCategory.Retirement,
            Type = MineType.EpfKwsp,
            Institution = "KWSP",
            Currency = "MYR",
            CurrentValue = 186000,
            PurchaseCost = 168000,
            Growth = 18000,
            GrowthPct = 10.7m,
            MonthlyIncome = 480,
            Status = "active",
            UpdatedAt = "2026-08-01"
        },
        new()
        {
            Id = "mine-fd-cimb",
            Name = "CIMB Fixed Deposit",
            Category = MineCategory.CashAndDeposits,
            Type = MineType.FixedDeposit,
            Institution = "CIMB",
            Currency = "MYR",
            CurrentValue = 80000,
            PurchaseCost = 76000,
            Growth = 4000,
            GrowthPct = 5.3m,
            MonthlyIncome = 233,
            SubMines =
            [
                new() { Id = "fd-1", Label = "Placement 1", Principal = 50000, Rate = 3.0m, MaturityDate = "2027-06-15" },
                new() { Id = "fd-2", Label = "Placement 2", Principal = 30000, Rate = 4.0m, MaturityDate = "2026-09-02" }
            ],
            Status = "maturing",
            UpdatedAt = "2026-08-10"
        },
        new()
        {
            Id = "mine-maybank",
            Name = "Maybank Shares",
            Category = MineCategory.Investments,
            Type = MineType.Stocks,
            Institution = "Bursa Malaysia",
            Currency = "MYR",
            CurrentValue = 18000,
            PurchaseCost = 16000,
            Growth = 2000,
            GrowthPct = 12.5m,
            MonthlyIncome = 40,
            Holdings = "1,500 shares",
            SubMines =
            [
                new() { Id = "p1", Label = "Purchase 1", Quantity = "1,000 shares", PurchasePrice = 10.0m },
                new() { Id = "p2", Label = "Purchase 2", Quantity = "500 shares", PurchasePrice = 12.0m }
            ],
            Status = "active",
            UpdatedAt = "2026-08-15"
        },
        new()
        {
            Id = "mine-pubank",
            Name = "Public Bank Shares",
            Category = MineCategory.Investments,
            Type = MineType.Stocks,
            Institution = "Bursa Malaysia",
            Currency = "MYR",
            CurrentValue = 9600,
            PurchaseCost = 11200,
            Growth = -1600,
            GrowthPct = -14.3m,
            MonthlyIncome = 25,
            Holdings = "400 shares",
            Status = "active",
            UpdatedAt = "2026-08-15"
        },
        new()
        {
            Id = "mine-condo1",
            Name = "Condo 1 - Mont Kiara",
            Category = MineCategory.Property,
            Type = MineType.Property,
            Currency = "MYR",
            CurrentValue = 620000,
            PurchaseCost = 540000,
            Growth = 80000,
            GrowthPct = 14.8m,
            MonthlyIncome = 1800,
            LinkedBurdenId = "burden-mortgage",
            Status = "active",
            UpdatedAt = "2026-07-20"
        },
        new()
        {
            Id = "mine-gold",
            Name = "Gold Savings",
            Category = MineCategory.PreciousMetals,
            Type = MineType.Gold,
            Institution = "Maybank",
            Currency = "MYR",
            CurrentValue = 32000,
            PurchaseCost = 28000,
            Growth = 4000,
            GrowthPct = 14.3m,
            MonthlyIncome = 0,
            Holdings = "50 grams",
            Status = "active",
            UpdatedAt = "2026-08-12"
        }
    ];

    private readonly List<Burden> _burdens =
    [
        new()
        {
            Id = "burden-mortgage",
            Name = "Mortgage - Condo 1",
            Type = BurdenType.Mortgage,
            Balance = 380000,
            OriginalAmount = 480000,
            InterestRate = 3.85m,
            MonthlyPayment = 2300,
            Currency = "MYR",
            LinkedMineId = "mine-condo1",
            MaturityDate = "2041-06-15"
        },
        new()
        {
            Id = "burden-car",
            Name = "Honda Civic Hire Purchase",
            Type = BurdenType.VehicleLoan,
            Balance = 42000,
            OriginalAmount = 120000,
            InterestRate = 2.9m,
            MonthlyPayment = 1850,
            Currency = "MYR",
            MaturityDate = "2028-03-10"
        },
        new()
        {
            Id = "burden-cc",
            Name = "Credit Card - Maybank 2",
            Type = BurdenType.CreditCard,
            Balance = 8500,
            OriginalAmount = 8500,
            InterestRate = 15.0m,
            MonthlyPayment = 500,
            Currency = "MYR"
        },
        new()
        {
            Id = "burden-study",
            Name = "PTPTN Education Loan",
            Type = BurdenType.EducationLoan,
            Balance = 18000,
            OriginalAmount = 40000,
            InterestRate = 1.0m,
            MonthlyPayment = 300,
            Currency = "MYR"
        }
    ];

    private readonly List<IncomeRecord> _incomeRecords =
    [
        new() { Id = "inc-1", Source = "Salary", Classification = IncomeClass.Active, Amount = 8500, Currency = "MYR", Frequency = "monthly", Date = "2026-08-25" },
        new() { Id = "inc-2", Source = "Rental - Condo 1", Classification = IncomeClass.PassiveMineGenerated, Amount = 2500, Currency = "MYR", Frequency = "monthly", MineId = "mine-condo1", MineName = "Condo 1 - Mont Kiara", Date = "2026-08-05" },
        new() { Id = "inc-3", Source = "EPF Distribution", Classification = IncomeClass.PassiveMineGenerated, Amount = 5760, Currency = "MYR", Frequency = "annual", MineId = "mine-epf", MineName = "EPF / KWSP", Date = "2026-03-01" },
        new() { Id = "inc-4", Source = "CIMB FD Interest", Classification = IncomeClass.PassiveMineGenerated, Amount = 2800, Currency = "MYR", Frequency = "annual", MineId = "mine-fd-cimb", MineName = "CIMB Fixed Deposit", Date = "2026-06-15" },
        new() { Id = "inc-5", Source = "Maybank Dividend", Classification = IncomeClass.PassiveMineGenerated, Amount = 480, Currency = "MYR", Frequency = "annual", MineId = "mine-maybank", MineName = "Maybank Shares", Date = "2026-05-20" },
        new() { Id = "inc-6", Source = "Annual Bonus", Classification = IncomeClass.NonRecurring, Amount = 12000, Currency = "MYR", Frequency = "one-off", Date = "2026-02-10" }
    ];

    private readonly List<Goal> _goals =
    [
        new() { Id = "goal-1", Title = "Emergency Fund (6 months)", Type = GoalType.Safety, Target = 60000, Current = 42000, TargetDate = "2027-06-30", Status = "on-track" },
        new() { Id = "goal-2", Title = "Pay off Credit Card", Type = GoalType.DebtReduction, Target = 8500, Current = 0, TargetDate = "2026-12-31", Status = "not-started" },
        new() { Id = "goal-3", Title = "Reach 30% Freedom", Type = GoalType.Freedom, Target = 30, Current = 22, TargetDate = "2027-12-31", Status = "needs-attention" },
        new() { Id = "goal-4", Title = "Retirement at 55", Type = GoalType.Retirement, Target = 1500000, Current = 186000, TargetDate = "2043-01-01", Status = "needs-attention" }
    ];

    private readonly List<Notification> _notifications =
    [
        new()
        {
            Id = "n-1",
            Title = "CIMB FD matures in 14 days",
            Body = "Your Placement 2 (RM30,000 @ 4.00%) matures on 2 Sep 2026. You may want to review renewal options and compare available rates.",
            Type = "maturity",
            Date = "2026-08-19",
            Read = false
        },
        new()
        {
            Id = "n-2",
            Title = "Emergency Fund milestone reached",
            Body = "You've crossed 70% of your Emergency Fund goal. RM18,000 remaining to reach the target.",
            Type = "goal",
            Date = "2026-08-15",
            Read = false
        },
        new()
        {
            Id = "n-3",
            Title = "Stock value needs updating",
            Body = "Your Public Bank Shares were last updated 5 days ago. Consider updating the current price for an accurate Net Wealth view.",
            Type = "data",
            Date = "2026-08-10",
            Read = true
        }
    ];

    public Task<MinerProfile> GetMinerProfileAsync() => Task.FromResult(_profile);

    public Task<DashboardSummary> GetDashboardSummaryAsync()
    {
        var totalMines = _mines.Sum(m => m.CurrentValue);
        var totalBurdens = _burdens.Sum(b => b.Balance);
        var passiveMonthly = _mines.Sum(m => m.MonthlyIncome);
        var activeMonthly = 8500m;
        var recurringExpensesMonthly = 4950m + _burdens.Sum(b => b.MonthlyPayment);
        var freedomRatio = recurringExpensesMonthly > 0 ? (passiveMonthly / recurringExpensesMonthly) * 100 : 0;
        var totalGrowth = _mines.Sum(m => m.Growth);
        var totalCost = _mines.Sum(m => m.PurchaseCost);
        var totalGrowthPct = totalCost > 0 ? (totalGrowth / totalCost) * 100 : 0;

        var summary = new DashboardSummary
        {
            TotalMines = totalMines,
            TotalBurdens = totalBurdens,
            NetWealth = totalMines - totalBurdens,
            PassiveIncomeMonthly = passiveMonthly,
            ActiveIncomeMonthly = activeMonthly,
            RecurringExpensesMonthly = recurringExpensesMonthly,
            FreedomRatio = Math.Round(freedomRatio, 0),
            TotalGrowth = totalGrowth,
            TotalGrowthPct = Math.Round(totalGrowthPct, 1)
        };

        return Task.FromResult(summary);
    }

    public Task<List<Mine>> GetMinesAsync() => Task.FromResult(_mines.ToList());

    public Task<Mine?> GetMineByIdAsync(string id) =>
        Task.FromResult(_mines.FirstOrDefault(m => m.Id.Equals(id, StringComparison.OrdinalIgnoreCase)));

    public Task AddMineAsync(Mine mine)
    {
        if (string.IsNullOrEmpty(mine.Id))
            mine.Id = $"mine-{Guid.NewGuid():N}"[..12];
        if (string.IsNullOrEmpty(mine.UpdatedAt))
            mine.UpdatedAt = DateTime.Today.ToString("yyyy-MM-dd");
        _mines.Add(mine);
        return Task.CompletedTask;
    }

    public Task DeleteMineAsync(string id)
    {
        var item = _mines.FirstOrDefault(m => m.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
        if (item != null) _mines.Remove(item);
        return Task.CompletedTask;
    }

    public Task<List<Burden>> GetBurdensAsync() => Task.FromResult(_burdens.ToList());

    public Task<Burden?> GetBurdenByIdAsync(string id) =>
        Task.FromResult(_burdens.FirstOrDefault(b => b.Id.Equals(id, StringComparison.OrdinalIgnoreCase)));

    public Task AddBurdenAsync(Burden burden)
    {
        if (string.IsNullOrEmpty(burden.Id))
            burden.Id = $"burden-{Guid.NewGuid():N}"[..12];
        _burdens.Add(burden);
        return Task.CompletedTask;
    }

    public Task DeleteBurdenAsync(string id)
    {
        var item = _burdens.FirstOrDefault(b => b.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
        if (item != null) _burdens.Remove(item);
        return Task.CompletedTask;
    }

    public Task<List<IncomeRecord>> GetIncomeRecordsAsync() => Task.FromResult(_incomeRecords.ToList());

    public Task AddIncomeRecordAsync(IncomeRecord record)
    {
        if (string.IsNullOrEmpty(record.Id))
            record.Id = $"inc-{Guid.NewGuid():N}"[..10];
        if (string.IsNullOrEmpty(record.Date))
            record.Date = DateTime.Today.ToString("yyyy-MM-dd");
        _incomeRecords.Add(record);
        return Task.CompletedTask;
    }

    public Task DeleteIncomeRecordAsync(string id)
    {
        var item = _incomeRecords.FirstOrDefault(r => r.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
        if (item != null) _incomeRecords.Remove(item);
        return Task.CompletedTask;
    }

    public Task<List<Goal>> GetGoalsAsync() => Task.FromResult(_goals.ToList());

    public Task AddGoalAsync(Goal goal)
    {
        if (string.IsNullOrEmpty(goal.Id))
            goal.Id = $"goal-{Guid.NewGuid():N}"[..10];
        _goals.Add(goal);
        return Task.CompletedTask;
    }

    public Task DeleteGoalAsync(string id)
    {
        var item = _goals.FirstOrDefault(g => g.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
        if (item != null) _goals.Remove(item);
        return Task.CompletedTask;
    }

    public Task<List<Notification>> GetNotificationsAsync() => Task.FromResult(_notifications.ToList());
}
