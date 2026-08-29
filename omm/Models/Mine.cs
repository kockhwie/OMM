namespace omm.Models;

public enum MineCategory
{
    Retirement,
    CashAndDeposits,
    Investments,
    Property,
    PreciousMetals,
    Other
}

public enum MineType
{
    EpfKwsp,
    FixedDeposit,
    Stocks,
    Reit,
    Funds,
    Property,
    Gold,
    Silver,
    OtherMine
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
    public List<SubMine>? SubMines { get; set; }
    public string? LinkedBurdenId { get; set; }
    public string Status { get; set; } = "active"; // active, maturing, inactive
    public string UpdatedAt { get; set; } = string.Empty;
}
