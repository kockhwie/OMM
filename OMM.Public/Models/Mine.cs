namespace OMM.Public.Models;

public enum MineCategory
{
    Retirement,         // EPF, PRS (Phase 3)
    CashAndDeposits,    // Savings Account, FD, Cash in Hand
    Investments,        // Stocks (MY + US), REIT, Unit Trust, ETF, ASB
    Property,           // Residential, Commercial
    PreciousMetals,     // Gold, Silver
    Digital,            // Cryptocurrency
    Other
}

public enum ForeignCurrencyTransactionType
{
    Purchase,
    Sale,
    Adjustment
}

public class ForeignCurrencyTransaction
{
    public string Id { get; set; } = string.Empty;
    public ForeignCurrencyTransactionType TransactionType { get; set; } = ForeignCurrencyTransactionType.Purchase;
    public DateOnly TransactionDate { get; set; }
    public decimal ForeignAmount { get; set; }
    public decimal MyrAmount { get; set; }
    public decimal ExchangeRate { get; set; }
    public decimal FeesMyr { get; set; }
    public string? Notes { get; set; }
}

public enum MineType
{
    // Retirement
    EpfKwsp = 0,
    // TabungHaji → Phase 2 (stub, hidden from UI picker for now)
    // Prs → Phase 3

    // Cash & Deposits
    SavingsAccount = 10,
    FixedDeposit = 1,
    CashInHand = 11,
    ForeignCurrency = 17,

    // Investments
    UnitTrustAsb = 12,
    UnitTrustGeneral = 4,
    [Obsolete("Use UnitTrustGeneral")]
    Funds = 4,
    Stocks = 2,
    StocksUs = 13,
    Reit = 3,
    Etf = 14,

    // Property
    PropertyResidential = 5,
    [Obsolete("Use PropertyResidential")]
    Property = 5,
    PropertyCommercial = 15,

    // Precious Metals
    Gold = 6,
    Silver = 7,

    // Digital
    Cryptocurrency = 16,

    // Catch-all
    Others = 8
}

/// <summary>
/// Type-specific fields captured during Add/Edit Mine.
/// Serialised as JSON into MineEntity.MetadataJson (jsonb column).
/// All fields nullable — only populate what applies to the MineType.
/// </summary>
public class MineMetadata
{
    // --- Shared optional ---
    public string? AccountNumber           { get; set; } // last 4 digits, reference only
    public string? Notes                   { get; set; }

    // --- Foreign Currency ---
    public string? ForeignCurrencyCode     { get; set; } // JPY, USD, SGD, etc.
    public string? CurrencyCustodyType     { get; set; } // Physical Cash, Online Wallet, Bank Account
    public string? CurrencyProvider        { get; set; } // Wise, bank, exchange, etc.
    public decimal? CurrentSellRate        { get; set; } // foreign units per one base-currency unit

    // --- EPF / Savings ---
    public string?  AccountType            { get; set; } // e.g. "Conventional", "Shariah"
    public decimal? Akaun1Balance          { get; set; } // Akaun Persaraan (75%)
    public decimal? Akaun2Balance          { get; set; } // Akaun Sejahtera (15%)
    public decimal? Akaun3Balance          { get; set; } // Akaun Fleksibel (10%)
    public decimal? MonthlyContribution    { get; set; } // Total employee + employer monthly contribution
    public decimal? ExpectedDividendRate   { get; set; } // % p.a. for EPF/ASB dividend projections

    // --- ASB / Unit Trust ---
    public decimal? UnitsHeld              { get; set; }
    public decimal? NavPerUnit             { get; set; } // RM 1.00 for ASB, market NAV for others
    public string?  FundCode               { get; set; } // "ASB", "ASM 2", fund name

    // --- Fixed Deposit ---
    public decimal? Principal              { get; set; }
    public decimal? InterestRatePct        { get; set; } // % per annum
    public int?     TenureMonths           { get; set; }
    public DateOnly? PlacementDate         { get; set; } // original FD placement date
    public DateOnly? MaturityDate          { get; set; }
    public string?  RolloverBehavior       { get; set; } // "auto-renew" | "cash-out"

    // --- Stocks / ETF ---
    public string?  StockCode              { get; set; } // e.g. "MAYBANK", "AAPL"
    public string?  Exchange               { get; set; } // "Bursa" | "NASDAQ" | "NYSE"
    public int?     SharesOwned            { get; set; } // in units (not lots)
    public decimal? CurrentSharePrice      { get; set; } // latest price in asset currency
    public decimal? PurchasePricePerShare  { get; set; } // average cost per share
    public decimal? DividendYieldPct       { get; set; } // % per annum estimate

    // --- Property ---
    public string?  Address                { get; set; }
    public string?  PropertyType           { get; set; } // "Condo", "Terraced", "Semi-D", "Apartment", "Bungalow"
    public decimal? OwnershipPct           { get; set; } // default 100
    public decimal? RentalIncomeMonthly    { get; set; }
    public decimal? MaintenanceCostMonthly { get; set; }
    public Guid?    LinkedMortgageBurdenId { get; set; }

    // --- Gold / Silver ---
    public decimal? WeightGrams            { get; set; }
    public string?  GoldForm               { get; set; } // "Physical Bar" | "Jewellery" | "Digital (GIA/GAP)"
    public decimal? CurrentPricePerGram    { get; set; }
    public decimal? BuybackPricePerGram    { get; set; }

    // --- Cryptocurrency ---
    public string?  CoinSymbol             { get; set; } // "BTC", "ETH", "BNB"
    public decimal? CoinsHeld              { get; set; } // high precision
    public decimal? CurrentPricePerCoin    { get; set; } // in MYR
    public decimal? PurchasePricePerCoin   { get; set; } // in MYR
    public decimal? StakingYieldPct        { get; set; } // % per annum
    public string?  WalletOrExchange       { get; set; }
}

public class SubMine
{
    public string Id { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public decimal? Principal { get; set; }
    public decimal? Rate { get; set; }
    public string? MaturityDate { get; set; }
    public string? Quantity { get; set; }
    public decimal? PurchasePrice { get; set; }
}

public class Mine
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public MineCategory Category { get; set; }
    public MineType Type { get; set; }
    public string? Institution { get; set; }
    public string Currency { get; set; } = "MYR";
    public decimal CurrentValue { get; set; }
    public decimal PurchaseCost { get; set; }
    public decimal Growth { get; set; }
    public decimal GrowthPct { get; set; }
    public decimal MonthlyIncome { get; set; }
    public string? Holdings { get; set; }
    public MineMetadata? Metadata { get; set; }
    public List<SubMine>? SubMines { get; set; }
    public List<ForeignCurrencyTransaction>? ForeignCurrencyTransactions { get; set; }
    public string? LinkedBurdenId { get; set; }
    public string Status { get; set; } = "active"; // active, maturing, inactive
    public string CreatedAt { get; set; } = string.Empty;
    public string? ModifiedAt { get; set; }
    public string UpdatedAt { get; set; } = string.Empty;
}
