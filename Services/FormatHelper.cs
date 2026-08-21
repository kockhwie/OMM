using System.Globalization;
using omm.Models;

namespace omm.Services;

public static class FormatHelper
{
    private static readonly CultureInfo MyCulture = new("en-MY");

    public static string FormatCurrency(decimal amount, string currency = "MYR")
    {
        var prefix = currency == "MYR" ? "RM" : currency + " ";
        return $"{prefix}{amount:N0}";
    }

    public static string FormatCurrencyPrecise(decimal amount, string currency = "MYR")
    {
        var prefix = currency == "MYR" ? "RM" : currency + " ";
        return $"{prefix}{amount:N2}";
    }

    public static string FormatPercent(decimal value)
    {
        var prefix = value > 0 ? "+" : "";
        return $"{prefix}{value:F1}%";
    }

    public static string FormatNumber(decimal value)
    {
        return value.ToString("N0", CultureInfo.InvariantCulture);
    }

    public static string FormatCurrencyCompact(decimal amount, string currency = "MYR")
    {
        var prefix = currency == "MYR" ? "RM " : currency + " ";
        var abs = Math.Abs(amount);
        var sign = amount < 0 ? "-" : "";

        if (abs >= 1_000_000)
            return $"{sign}{prefix}{(abs / 1_000_000m):F2}M";
        if (abs >= 1_000)
            return $"{sign}{prefix}{(abs / 1_000m):F1}k";

        return $"{sign}{prefix}{abs:N0}";
    }

    public static string FreedomLabel(decimal ratio)
    {
        if (ratio <= 0) return "No recurring passive coverage yet";
        if (ratio < 25) return "Early stage";
        if (ratio < 50) return "Building momentum";
        if (ratio < 75) return "Getting closer";
        if (ratio < 100) return "Almost there";
        return "Passive income covers recurring expenses";
    }

    public static string GoalStatusLabel(string status) => status switch
    {
        "on-track" => "On Track",
        "needs-attention" => "Needs Attention",
        "not-started" => "Not Started",
        "achieved" => "Achieved",
        _ => status
    };

    public static int GoalProgress(decimal current, decimal target)
    {
        if (target <= 0) return 0;
        return Math.Min((int)Math.Round((current / target) * 100), 100);
    }

    public static int DaysUntil(string dateStr)
    {
        if (DateTime.TryParse(dateStr, out var target))
        {
            var now = DateTime.Today;
            return (int)Math.Ceiling((target - now).TotalDays);
        }
        return 0;
    }

    public static string CategoryLabel(MineCategory category) => category switch
    {
        MineCategory.Retirement => "Retirement",
        MineCategory.CashAndDeposits => "Cash & Deposits",
        MineCategory.Investments => "Investments",
        MineCategory.Property => "Property",
        MineCategory.PreciousMetals => "Precious Metals",
        _ => category.ToString()
    };

    public static string MineTypeLabel(MineType type) => type switch
    {
        MineType.EpfKwsp => "EPF / KWSP",
        MineType.FixedDeposit => "Fixed Deposit",
        MineType.Stocks => "Stocks",
        MineType.Reit => "REIT",
        MineType.Funds => "Unit Trust / Funds",
        MineType.Property => "Real Estate",
        MineType.Gold => "Physical Gold / Savings",
        MineType.Silver => "Silver",
        _ => type.ToString()
    };

    public static string BurdenTypeLabel(BurdenType type) => type switch
    {
        BurdenType.Mortgage => "Mortgage",
        BurdenType.CreditCard => "Credit Card",
        BurdenType.VehicleLoan => "Vehicle Loan",
        BurdenType.EducationLoan => "Education Loan",
        BurdenType.PersonalLoan => "Personal Loan",
        BurdenType.TaxPayable => "Tax Payable",
        _ => "Other"
    };

    public static string IncomeClassLabel(IncomeClass cls) => cls switch
    {
        IncomeClass.Active => "Active",
        IncomeClass.PassiveMineGenerated => "Passive / Mine-generated",
        IncomeClass.NonRecurring => "Non-recurring",
        _ => cls.ToString()
    };

    public static string CategoryToIcon(MineCategory category)
    {
        return category switch
        {
            MineCategory.Retirement => "ti-shield-lock",
            MineCategory.CashAndDeposits => "ti-building-bank",
            MineCategory.Investments => "ti-chart-pie",
            MineCategory.Property => "ti-home-dollar",
            MineCategory.PreciousMetals => "ti-coins",
            _ => "ti-folder"
        };
    }

    public static string BurdenTypeToIcon(BurdenType type)
    {
        return type switch
        {
            BurdenType.Mortgage => "ti-home-cancel",
            BurdenType.VehicleLoan => "ti-car",
            BurdenType.CreditCard => "ti-credit-card",
            BurdenType.EducationLoan => "ti-school",
            BurdenType.PersonalLoan => "ti-user-dollar",
            BurdenType.TaxPayable => "ti-receipt-tax",
            _ => "ti-alert-circle"
        };
    }

    public static string GoalTypeToIcon(GoalType type)
    {
        return type switch
        {
            GoalType.Safety => "ti-shield-check",
            GoalType.DebtReduction => "ti-target",
            GoalType.Freedom => "ti-sparkles",
            GoalType.Retirement => "ti-flag",
            GoalType.WealthBuilding => "ti-trending-up",
            GoalType.Income => "ti-trending-up",
            GoalType.MajorPurchase => "ti-home",
            GoalType.Habits => "ti-circle-check",
            _ => "ti-target"
        };
    }
}
