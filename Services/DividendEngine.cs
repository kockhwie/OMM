namespace omm.Services;

// ─────────────────────────────────────────────────────────────────────────────
// Data Models
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>Dividend payment frequency.</summary>
public enum DividendFrequency
{
    Monthly = 12,
    Quarterly = 4,
    SemiAnnually = 2,
    Annually = 1
}

/// <summary>Recurring contribution frequency for the simulator.</summary>
public enum ContributionFrequency
{
    None = 0,
    Monthly = 12,
    Quarterly = 4,
    Annually = 1
}

/// <summary>Instant results from Quick Calculate mode.</summary>
public record QuickCalcResult
{
    public decimal AnnualDividend { get; init; }
    public decimal RegularAnnualDividend { get; init; }
    public decimal SpecialDividendTotal { get; init; }
    public decimal PerPaymentDividend { get; init; }
    public decimal MonthlyEquivalent { get; init; }
    public decimal? DividendYield { get; init; }         // null when share price not provided
    public decimal? YieldOnCost { get; init; }           // null when purchase price not provided
    public int PaymentsPerYear { get; init; }
    public string FrequencyLabel { get; init; } = "";
    public List<decimal> PayoutBreakdown { get; init; } = [];
}

/// <summary>All inputs for the Investment Simulator.</summary>
public record DividendSimulatorInput
{
    // Stock / Share metadata
    public string StockSymbol { get; init; } = string.Empty;
    public string StockName { get; init; } = string.Empty;

    // Investment
    public decimal InitialShares { get; init; }
    public decimal PurchasePrice { get; init; }          // price paid per share (for yield-on-cost)
    public decimal CurrentSharePrice { get; init; }      // current market price per share

    // Contribution
    public decimal RecurringContribution { get; init; }  // amount per contribution period
    public ContributionFrequency ContributionFrequency { get; init; } = ContributionFrequency.None;

    // Dividend
    public decimal DividendPerShare { get; init; }       // base per-payment dividend (if uniform)
    public DividendFrequency DividendFrequency { get; init; } = DividendFrequency.Quarterly;
    public decimal DividendGrowthRate { get; init; }     // annual %, e.g. 5 = 5%
    public decimal SpecialDividendPerShare { get; init; } // one-off/special dividend per share
    public List<decimal>? VariableDividends { get; init; } // optional per-period breakdown

    // Growth
    public decimal SharePriceGrowthRate { get; init; }   // annual %, e.g. 6 = 6%; default 0

    // Duration
    public int InvestmentYears { get; init; } = 10;

    // Reinvestment
    public bool DripEnabled { get; init; } = false;
    public bool AllowFractionalShares { get; init; } = true;

    // Tax
    public bool TaxEnabled { get; init; } = false;
    public decimal DividendTaxRate { get; init; }        // %, e.g. 15 = 15%
    public decimal CapitalGainsTaxRate { get; init; }    // %, e.g. 10 = 10%
}

/// <summary>Per-year snapshot used for the table and charts.</summary>
public record YearProjection
{
    public int Year { get; init; }
    public decimal Shares { get; init; }
    public decimal DividendPerShare { get; init; }       // annual (sum of all payments that year)
    public decimal AnnualDividendGross { get; init; }
    public decimal AnnualDividendNet { get; init; }      // after tax
    public decimal SharePrice { get; init; }
    public decimal PortfolioValue { get; init; }
    public decimal CumulativeContributions { get; init; }
    public decimal CumulativeDividendsGross { get; init; }
    public decimal CumulativeDividendsNet { get; init; }
    public decimal CumulativeSharesBoughtViaDrip { get; init; }
    public decimal TotalReturn { get; init; }            // % total return vs initial investment
}

/// <summary>Full result from the Investment Simulator.</summary>
public record SimulatorResult
{
    public List<YearProjection> Projections { get; init; } = [];
    public decimal FinalPortfolioValue { get; init; }
    public decimal FinalAnnualDividend { get; init; }
    public decimal TotalDividendsGross { get; init; }
    public decimal TotalDividendsNet { get; init; }
    public decimal TotalShares { get; init; }
    public decimal TotalReturn { get; init; }            // %
    public decimal YieldOnCost { get; init; }
    public decimal InitialInvestment { get; init; }
    public decimal TotalContributions { get; init; }
    // Cash-vs-DRIP comparison (value if dividends taken as cash instead)
    public decimal ValueWithoutDrip { get; init; }
}

// ─────────────────────────────────────────────────────────────────────────────
// Calculation Engine
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Pure financial calculation engine for stock/share dividend calculations.
/// Contains no UI dependencies. All inputs and outputs use decimal for precision.
/// </summary>
public static class DividendEngine
{
    // ── Frequency Helpers ────────────────────────────────────────────────────

    public static int PaymentsPerYear(DividendFrequency frequency) => (int)frequency;

    public static string FrequencyLabel(DividendFrequency frequency) => frequency switch
    {
        DividendFrequency.Monthly => "monthly",
        DividendFrequency.Quarterly => "quarterly",
        DividendFrequency.SemiAnnually => "semi-annually",
        DividendFrequency.Annually => "annually",
        _ => ""
    };

    public static string FrequencyShortLabel(DividendFrequency frequency) => frequency switch
    {
        DividendFrequency.Monthly => "Monthly",
        DividendFrequency.Quarterly => "Quarterly",
        DividendFrequency.SemiAnnually => "Semi-annual",
        DividendFrequency.Annually => "Annual",
        _ => ""
    };

    public static string GetPeriodLabel(DividendFrequency frequency, int periodIndex) => frequency switch
    {
        DividendFrequency.Quarterly => $"Q{periodIndex + 1}",
        DividendFrequency.SemiAnnually => $"H{periodIndex + 1}",
        DividendFrequency.Monthly => $"M{periodIndex + 1}",
        DividendFrequency.Annually => "Annual",
        _ => $"Period {periodIndex + 1}"
    };

    // ── Quick Calculate ──────────────────────────────────────────────────────

    /// <summary>Compute the full Quick Calculate result set.</summary>
    public static QuickCalcResult QuickCalculate(
        decimal shares,
        decimal dividendPerShare,
        DividendFrequency frequency,
        decimal? sharePrice = null,
        decimal? purchasePrice = null,
        decimal specialDividendPerShare = 0m,
        IReadOnlyList<decimal>? variableDividends = null)
    {
        int paymentsPerYear = PaymentsPerYear(frequency);
        var payoutBreakdown = new List<decimal>();

        decimal regularAnnualDps = 0m;

        if (variableDividends != null && variableDividends.Count > 0)
        {
            for (int i = 0; i < paymentsPerYear; i++)
            {
                decimal dps = i < variableDividends.Count ? Math.Max(0m, variableDividends[i]) : 0m;
                payoutBreakdown.Add(dps);
                regularAnnualDps += dps;
            }
        }
        else
        {
            for (int i = 0; i < paymentsPerYear; i++)
            {
                payoutBreakdown.Add(dividendPerShare);
            }
            regularAnnualDps = Math.Max(0m, dividendPerShare) * paymentsPerYear;
        }

        decimal specialDps = Math.Max(0m, specialDividendPerShare);
        decimal totalAnnualDps = regularAnnualDps + specialDps;

        decimal annualDividend = shares > 0 ? shares * totalAnnualDps : 0m;
        decimal regularAnnual = shares > 0 ? shares * regularAnnualDps : 0m;
        decimal specialTotal = shares > 0 ? shares * specialDps : 0m;
        decimal perPayment = paymentsPerYear > 0 ? regularAnnual / paymentsPerYear : 0m;
        decimal monthly = CalculateMonthlyEquivalent(annualDividend);

        decimal? yield = (sharePrice.HasValue && sharePrice.Value > 0)
            ? CalculateDividendYield(annualDividend, sharePrice.Value, shares)
            : null;

        decimal? yoc = (purchasePrice.HasValue && purchasePrice.Value > 0)
            ? CalculateYieldOnCost(annualDividend, purchasePrice.Value, shares)
            : null;

        return new QuickCalcResult
        {
            AnnualDividend = annualDividend,
            RegularAnnualDividend = regularAnnual,
            SpecialDividendTotal = specialTotal,
            PerPaymentDividend = perPayment,
            MonthlyEquivalent = monthly,
            DividendYield = yield,
            YieldOnCost = yoc,
            PaymentsPerYear = paymentsPerYear,
            FrequencyLabel = FrequencyLabel(frequency),
            PayoutBreakdown = payoutBreakdown
        };
    }

    /// <summary>Annual dividend divided by 12 for monthly equivalent display.</summary>
    public static decimal CalculateMonthlyEquivalent(decimal annualDividend)
        => annualDividend / 12m;

    /// <summary>
    /// Dividend yield = annual dividend income / current market value.
    /// Returns null if share price is zero or not provided.
    /// </summary>
    public static decimal? CalculateDividendYield(decimal annualDividend, decimal sharePrice, decimal shares)
    {
        if (sharePrice <= 0 || shares <= 0) return null;
        var portfolioValue = sharePrice * shares;
        return portfolioValue > 0 ? (annualDividend / portfolioValue) * 100m : null;
    }

    /// <summary>
    /// Yield on cost = annual dividend income / original purchase cost.
    /// Returns null if purchase price is zero or not provided.
    /// </summary>
    public static decimal? CalculateYieldOnCost(decimal annualDividend, decimal purchasePrice, decimal shares)
    {
        if (purchasePrice <= 0 || shares <= 0) return null;
        var costBasis = purchasePrice * shares;
        return costBasis > 0 ? (annualDividend / costBasis) * 100m : null;
    }

    // ── Growth Projections ───────────────────────────────────────────────────

    /// <summary>Apply annual compound growth for N years.</summary>
    public static decimal CalculateFutureDividend(decimal currentDividend, decimal annualGrowthPct, int year)
    {
        if (annualGrowthPct == 0m || year <= 0) return currentDividend;
        return currentDividend * (decimal)Math.Pow(1.0 + (double)annualGrowthPct / 100.0, year);
    }

    public static decimal CalculateFutureSharePrice(decimal currentPrice, decimal annualGrowthPct, int year)
    {
        if (currentPrice <= 0) return 0m;
        if (annualGrowthPct == 0m || year <= 0) return currentPrice;
        return currentPrice * (decimal)Math.Pow(1.0 + (double)annualGrowthPct / 100.0, year);
    }

    // ── Tax ──────────────────────────────────────────────────────────────────

    public static decimal CalculateTax(decimal grossAmount, decimal taxRatePct)
    {
        if (taxRatePct <= 0 || grossAmount <= 0) return 0m;
        return grossAmount * (taxRatePct / 100m);
    }

    public static decimal CalculateNetAfterTax(decimal grossAmount, decimal taxRatePct)
        => grossAmount - CalculateTax(grossAmount, taxRatePct);

    // ── Investment Simulator ─────────────────────────────────────────────────

    /// <summary>
    /// Full period-by-period simulation.
    /// Processes dividend events at the chosen frequency, applies DRIP each period,
    /// applies recurring contributions at the chosen contribution frequency,
    /// and applies share price growth annually.
    /// </summary>
    public static SimulatorResult SimulatePortfolio(DividendSimulatorInput input)
    {
        // Validate
        if (input.InitialShares < 0 || input.CurrentSharePrice < 0 || input.InvestmentYears <= 0)
        {
            return new SimulatorResult();
        }

        int paymentsPerYear = PaymentsPerYear(input.DividendFrequency);
        int contributionsPerYear = (int)input.ContributionFrequency;

        // Working state
        decimal shares = input.InitialShares;
        decimal sharePrice = input.CurrentSharePrice > 0 ? input.CurrentSharePrice : 1m;

        decimal initialInvestment = input.InitialShares * (input.PurchasePrice > 0 ? input.PurchasePrice : sharePrice);
        decimal totalContributions = 0m;
        decimal cumulativeDividendsGross = 0m;
        decimal cumulativeDividendsNet = 0m;
        decimal cumulativeSharesDrip = 0m;

        // For cash-vs-DRIP comparison: track without DRIP
        decimal sharesNoDrip = input.InitialShares;
        decimal totalCashDividends = 0m; // dividends taken as cash in the no-DRIP scenario

        var projections = new List<YearProjection>();

        for (int year = 1; year <= input.InvestmentYears; year++)
        {
            // Step 1: Apply annual share price growth (at start of year)
            if (year > 1)
            {
                sharePrice = CalculateFutureSharePrice(
                    input.CurrentSharePrice > 0 ? input.CurrentSharePrice : 1m,
                    input.SharePriceGrowthRate,
                    year - 1);
            }
            else
            {
                sharePrice = input.CurrentSharePrice > 0 ? input.CurrentSharePrice : 1m;
            }

            // Step 2: Annual dividend growth factor
            decimal annualDividendGrowthFactor = input.DividendGrowthRate > 0
                ? (decimal)Math.Pow(1.0 + (double)input.DividendGrowthRate / 100.0, year - 1)
                : 1m;

            decimal yearDividendGross = 0m;
            decimal yearDividendNet = 0m;
            decimal yearSharesBoughtDrip = 0m;

            // Step 3: Process dividend events (per payment period)
            for (int payment = 1; payment <= paymentsPerYear; payment++)
            {
                decimal baseDps;
                if (input.VariableDividends != null && input.VariableDividends.Count >= payment)
                {
                    baseDps = input.VariableDividends[payment - 1] * annualDividendGrowthFactor;
                }
                else
                {
                    baseDps = input.DividendPerShare * annualDividendGrowthFactor;
                }

                // Add special dividend in year 1, payment 1
                if (year == 1 && payment == 1 && input.SpecialDividendPerShare > 0)
                {
                    baseDps += input.SpecialDividendPerShare;
                }

                decimal grossDiv = shares * baseDps;
                decimal taxAmount = input.TaxEnabled ? CalculateTax(grossDiv, input.DividendTaxRate) : 0m;
                decimal netDiv = grossDiv - taxAmount;

                yearDividendGross += grossDiv;
                yearDividendNet += netDiv;

                // DRIP: reinvest net dividend into additional shares
                if (input.DripEnabled && sharePrice > 0 && netDiv > 0)
                {
                    decimal newShares = input.AllowFractionalShares
                        ? netDiv / sharePrice
                        : Math.Floor(netDiv / sharePrice);
                    shares += newShares;
                    yearSharesBoughtDrip += newShares;
                }

                // No-DRIP comparison: accumulate cash dividends on no-drip shares
                decimal grossDivNoDrip = sharesNoDrip * baseDps;
                decimal netDivNoDrip = input.TaxEnabled
                    ? grossDivNoDrip - CalculateTax(grossDivNoDrip, input.DividendTaxRate)
                    : grossDivNoDrip;
                totalCashDividends += netDivNoDrip;
            }

            // Step 4: Process recurring contributions (spread through year)
            if (contributionsPerYear > 0 && input.RecurringContribution > 0 && sharePrice > 0)
            {
                decimal contributionThisYear = input.RecurringContribution * contributionsPerYear;
                totalContributions += contributionThisYear;
                decimal newSharesFromContrib = contributionThisYear / sharePrice;
                shares += input.AllowFractionalShares
                    ? newSharesFromContrib
                    : Math.Floor(newSharesFromContrib);
            }

            cumulativeDividendsGross += yearDividendGross;
            cumulativeDividendsNet += yearDividendNet;
            cumulativeSharesDrip += yearSharesBoughtDrip;

            decimal portfolioValue = shares * sharePrice;
            decimal totalInvested = initialInvestment + totalContributions;
            decimal totalReturn = totalInvested > 0
                ? ((portfolioValue + cumulativeDividendsNet - totalInvested) / totalInvested) * 100m
                : 0m;

            decimal annualizedDps = (shares > 0 && yearDividendGross > 0) ? (yearDividendGross / shares) : 0m;

            projections.Add(new YearProjection
            {
                Year = year,
                Shares = shares,
                DividendPerShare = annualizedDps,
                AnnualDividendGross = yearDividendGross,
                AnnualDividendNet = yearDividendNet,
                SharePrice = sharePrice,
                PortfolioValue = portfolioValue,
                CumulativeContributions = totalContributions,
                CumulativeDividendsGross = cumulativeDividendsGross,
                CumulativeDividendsNet = cumulativeDividendsNet,
                CumulativeSharesBoughtViaDrip = cumulativeSharesDrip,
                TotalReturn = totalReturn
            });
        }

        var last = projections.LastOrDefault();
        decimal finalPortfolio = last?.PortfolioValue ?? 0m;
        decimal finalAnnualDiv = last?.AnnualDividendNet ?? 0m;
        decimal totalInvestedFinal = initialInvestment + totalContributions;

        // Yield on cost based on final annual dividend vs original cost basis
        decimal yoc = initialInvestment > 0
            ? ((last?.AnnualDividendGross ?? 0m) / initialInvestment) * 100m
            : 0m;

        // Total return including dividends received
        decimal totalReturnPct = totalInvestedFinal > 0
            ? ((finalPortfolio + cumulativeDividendsNet - totalInvestedFinal) / totalInvestedFinal) * 100m
            : 0m;

        // No-DRIP portfolio value: same share price growth but original shares only (+ contributions)
        decimal valueWithoutDrip = sharesNoDrip * sharePrice + totalCashDividends;

        return new SimulatorResult
        {
            Projections = projections,
            FinalPortfolioValue = finalPortfolio,
            FinalAnnualDividend = finalAnnualDiv,
            TotalDividendsGross = cumulativeDividendsGross,
            TotalDividendsNet = cumulativeDividendsNet,
            TotalShares = shares,
            TotalReturn = totalReturnPct,
            YieldOnCost = yoc,
            InitialInvestment = initialInvestment,
            TotalContributions = totalContributions,
            ValueWithoutDrip = valueWithoutDrip
        };
    }

    // ── Validation Helpers ───────────────────────────────────────────────────

    public static bool IsValidPositive(decimal value) => value > 0m && value < 1_000_000_000m;
    public static bool IsValidNonNegative(decimal value) => value >= 0m && value < 1_000_000_000m;
    public static bool IsValidPercentage(decimal value) => value >= -100m && value <= 500m;
    public static bool IsValidYears(int years) => years >= 1 && years <= 100;

    /// <summary>Format a decimal for safe display (never NaN/Infinity).</summary>
    public static string SafeFormat(decimal? value, string format = "N2")
        => value.HasValue && value >= 0 ? value.Value.ToString(format) : "—";
}
