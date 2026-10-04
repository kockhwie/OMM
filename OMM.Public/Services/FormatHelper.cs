using System.Globalization;
using OMM.Public.Models;

namespace OMM.Public.Services;

public static class FormatHelper
{
    public enum GrowthState
    {
        Unavailable,
        Flat,
        Positive,
        Negative
    }

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

    public static string GetCurrencySymbol(string currency = "MYR") => currency?.ToUpperInvariant() switch
    {
        "MYR" => "RM",
        "USD" => "$",
        "SGD" => "S$",
        "EUR" => "€",
        "GBP" => "£",
        "JPY" => "¥",
        "CNY" => "¥",
        "AUD" => "A$",
        "CAD" => "C$",
        "HKD" => "HK$",
        "NZD" => "NZ$",
        "THB" => "฿",
        "IDR" => "Rp",
        "VND" => "₫",
        _ => string.IsNullOrWhiteSpace(currency) ? "RM" : currency.Trim() + " "
    };

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
        MineCategory.Digital => "Digital Assets",
        MineCategory.Other => "Others",
        _ => category.ToString()
    };

    public static string MineTypeLabel(MineType type) => type switch
    {
        MineType.EpfKwsp => "EPF / KWSP",
        MineType.SavingsAccount => "Savings Account",
        MineType.FixedDeposit => "Fixed Deposit (FD)",
        MineType.CashInHand => "Cash in Hand",
        MineType.ForeignCurrency => "Foreign Currency",
        MineType.UnitTrustAsb => "ASB / ASM",
        MineType.UnitTrustGeneral => "Unit Trust / Mutual Fund",
        MineType.Stocks => "Stocks (Malaysia Stock Market)",
        MineType.StocksUs => "Stocks (US Stock Market)",
        MineType.Reit => "REIT",
        MineType.Etf => "ETF",
        MineType.PropertyResidential => "Residential Property",
        MineType.PropertyCommercial => "Commercial Property",
        MineType.Gold => "Gold",
        MineType.Silver => "Silver",
        MineType.Cryptocurrency => "Cryptocurrency",
        MineType.Others => "Others",
        _ => type.ToString()
    };

    public static string CurrentValueLabel(MineType type) => type switch
    {
        MineType.EpfKwsp or MineType.SavingsAccount or MineType.CashInHand => "Balance",
        MineType.ForeignCurrency => "Value (MYR)",
        MineType.FixedDeposit => "Projected Value",
        MineType.UnitTrustAsb or MineType.UnitTrustGeneral
            or MineType.Stocks or MineType.StocksUs
            or MineType.Reit or MineType.Etf => "Market Value",
        MineType.PropertyResidential or MineType.PropertyCommercial => "Valuation",
        MineType.Gold or MineType.Silver => "Est. Value",
        MineType.Cryptocurrency => "Value (MYR)",
        _ => "Current Value"
    };

    public static bool HasCurrentValuation(Mine mine) =>
        mine.Type != MineType.ForeignCurrency || mine.Metadata?.CurrentSellRate is > 0;

    public static GrowthState GetGrowthState(Mine mine)
    {
        if (!HasCurrentValuation(mine))
        {
            return GrowthState.Unavailable;
        }

        return mine.Growth switch
        {
            > 0 => GrowthState.Positive,
            < 0 => GrowthState.Negative,
            _ => GrowthState.Flat
        };
    }

    public static string GrowthStateLabel(Mine mine) => GetGrowthState(mine) switch
    {
        GrowthState.Positive or GrowthState.Negative => FormatPercent(mine.GrowthPct),
        GrowthState.Flat => "Flat",
        _ => "Pending"
    };

    public static string GrowthStateIcon(GrowthState state) => state switch
    {
        GrowthState.Positive => "ti-trending-up",
        GrowthState.Negative => "ti-trending-down",
        _ => "ti-minus"
    };

    public static string GrowthStateClass(GrowthState state) => state switch
    {
        GrowthState.Positive => "text-success",
        GrowthState.Negative => "text-danger",
        _ => "text-muted"
    };

    public static string MineTypeToIcon(MineType type) => type switch
    {
        MineType.EpfKwsp => "ti-shield-lock",
        MineType.SavingsAccount => "ti-building-bank",
        MineType.FixedDeposit => "ti-lock-dollar",
        MineType.CashInHand => "ti-cash",
        MineType.ForeignCurrency => "ti-world-dollar",
        MineType.UnitTrustAsb => "ti-chart-area",
        MineType.UnitTrustGeneral => "ti-chart-pie",
        MineType.Stocks => "ti-trending-up",
        MineType.StocksUs => "ti-globe",
        MineType.Reit => "ti-building-skyscraper",
        MineType.Etf => "ti-chart-bar",
        MineType.PropertyResidential => "ti-home-dollar",
        MineType.PropertyCommercial => "ti-building-store",
        MineType.Gold => "ti-coins",
        MineType.Silver => "ti-coin",
        MineType.Cryptocurrency => "ti-currency-bitcoin",
        _ => "ti-folder"
    };

    /// <summary>
    /// Returns the valid <see cref="MineType"/> values for a given <see cref="MineCategory"/>.
    /// Used to cascade the Asset Type dropdown in the Add Mine form.
    /// </summary>
    public static IReadOnlyList<MineType> GetTypesForCategory(MineCategory category) => category switch
    {
        MineCategory.Retirement      => [MineType.EpfKwsp, MineType.Others],
        MineCategory.CashAndDeposits => [MineType.SavingsAccount, MineType.FixedDeposit, MineType.CashInHand, MineType.ForeignCurrency, MineType.Others],
        MineCategory.Investments     => [MineType.UnitTrustAsb, MineType.UnitTrustGeneral, MineType.Stocks, MineType.StocksUs, MineType.Reit, MineType.Etf, MineType.Others],
        MineCategory.Property        => [MineType.PropertyResidential, MineType.PropertyCommercial, MineType.Others],
        MineCategory.PreciousMetals  => [MineType.Gold, MineType.Silver, MineType.Others],
        MineCategory.Digital         => [MineType.Cryptocurrency, MineType.Others],
        _                            => [MineType.Others]
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
        IncomeClass.PassiveMineGenerated => "Passive",
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
            MineCategory.Digital => "ti-currency-bitcoin",
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
