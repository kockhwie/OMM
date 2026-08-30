namespace OMM.Shared.Models.MasterData;

public class Stock : AuditableEntity
{
    public int Id { get; set; }
    public required string StockCode { get; set; }
    public required string ShortName_EN { get; set; }
    public string? ShortName_ZH_TW { get; set; }
    public string? ShortName_ZH_CN { get; set; }
    public string? LegalName_EN { get; set; }
    public string? LegalName_ZH_TW { get; set; }
    public string? LegalName_ZH_CN { get; set; }
    public string? RicCode { get; set; }
    public string? YahooSymbol { get; set; }
    public string? IsinCode { get; set; }
    public int MarketId { get; set; }
    public int? SectorId { get; set; }
    public int? SubSectorId { get; set; }
    public bool ShariahCompliant { get; set; }
    public required string Currency { get; set; }
    public bool IsActive { get; set; }
    public decimal? CurrentPrice { get; set; }
    public decimal? MarketCap { get; set; }
    public decimal? EPS { get; set; }
    public decimal? DPS { get; set; }
    public decimal? NTA { get; set; }
    public decimal? ROE { get; set; }
    public decimal? ROA { get; set; }
    public decimal? DebtToEquity { get; set; }
    public decimal? CurrentRatio { get; set; }
    public DateTimeOffset? LastScrapedAt { get; set; }
    public decimal? PE { get; set; }
    public decimal? PB { get; set; }
    public decimal? DividendYield { get; set; }
    public DateTimeOffset? LastCalculatedAt { get; set; }
    public Market Market { get; set; } = null!;
    public Sector? Sector { get; set; }
    public SubSector? SubSector { get; set; }
}
