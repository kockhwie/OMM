namespace OMM.Public.Models;

public static class BursaConstants
{
    public static readonly string[] Markets =
    [
        "Main Market",
        "ACE Market",
        "LEAP Market"
    ];

    public static readonly Dictionary<string, string[]> SectorSubSectors = new()
    {
        ["Financial Services"] = ["Banking", "Insurance", "Other Financial Services"],
        ["Consumer Products & Services"] = ["Food & Beverages", "Retailers", "Automotive", "Consumer Services", "Household Goods", "Agricultural Products", "Travel, Leisure & Hospitality"],
        ["Industrial Products & Services"] = ["Building Materials", "Chemicals", "Metals", "Packaging Materials", "Diversified Industrials", "Industrial Engineering"],
        ["Technology"] = ["Semiconductors", "Software", "Digital Services", "Hardware"],
        ["Telecommunications & Media"] = ["Telecommunications Service Providers", "Media & Advertising", "Telecommunications Equipment"],
        ["Health Care"] = ["Healthcare Providers", "Pharmaceuticals", "Healthcare Equipment & Supplies"],
        ["Property"] = ["Property Development", "Property Investment & Management"],
        ["Real Estate Investment Trusts (REITs)"] = ["Commercial", "Retail", "Industrial", "Hospitality", "Healthcare"],
        ["Plantation"] = ["Upstream Plantation", "Integrated Cultivation"],
        ["Energy"] = ["Oil & Gas Producers", "Oil & Gas Equipment & Services", "Renewable Energy"],
        ["Construction"] = ["Civil Engineering", "Heavy Construction", "Specialised Construction"],
        ["Transportation & Logistics"] = ["Logistics Services", "Ports & Shipping", "Airlines & Aviation", "Road & Rail"],
        ["Utilities"] = ["Electricity", "Gas & Water Distribution"]
    };
}

/// <summary>
/// Represents a Bursa Malaysia (KLSE) listed stock counter with core identifiers, market/sector classification, and fundamental ratios.
/// </summary>
public class KlseStock
{
    public int Id { get; set; }

    // Core Identifiers
    public string StockCode { get; set; } = string.Empty;       // e.g. "MAYBANK" or "5250"
    public string StockNumber { get; set; } = string.Empty;     // e.g. "1155" or "5250"
    public string? RicCode { get; set; }                        // e.g. "MBBM.KL"
    public string? YahooSymbol { get; set; }                    // e.g. "1155.KL"
    public string? IsinCode { get; set; }                       // e.g. "MYL1155OO000"
    public string CompanyName { get; set; } = string.Empty;

    // Backward-compatibility properties for existing components (StockAutosuggest, etc.)
    public string Code
    {
        get => !string.IsNullOrEmpty(StockNumber) ? StockNumber : StockCode;
        set
        {
            if (string.IsNullOrEmpty(StockNumber)) StockNumber = value;
            if (string.IsNullOrEmpty(StockCode)) StockCode = value;
        }
    }

    public string Name
    {
        get => CompanyName;
        set => CompanyName = value;
    }

    // Market & Sector Classification
    public string? Market { get; set; }                         // "Main Market", "ACE Market", "LEAP Market"
    public string? Sector { get; set; }                         // "Financial Services"
    public string? SubSector { get; set; }                      // "Banking"
    public bool ShariahCompliant { get; set; } = false;

    // Market Data & Key Ratios
    public decimal? CurrentPrice { get; set; }
    public decimal? MarketCap { get; set; }
    public string Currency { get; set; } = "MYR";
    public decimal? PE { get; set; }
    public decimal? PB { get; set; }
    public decimal? ROE { get; set; }
    public decimal? ROA { get; set; }
    public decimal? NTA { get; set; }
    public decimal? EPS { get; set; }
    public decimal? DPS { get; set; }
    public decimal? DividendYield { get; set; }
    public decimal? DebtToEquity { get; set; }
    public decimal? CurrentRatio { get; set; }

    // Metadata
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ScrapedAt { get; set; }
}
