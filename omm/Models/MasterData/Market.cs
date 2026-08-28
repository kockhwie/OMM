namespace omm.Models.MasterData;

public class Market : AuditableEntity
{
    public int Id { get; set; }
    public int ExchangeId { get; set; }
    public required string MarketCode { get; set; }
    public required string MarketName_EN { get; set; }
    public required string MarketName_ZH_TW { get; set; }
    public required string MarketName_ZH_CN { get; set; }
    public bool IsActive { get; set; }
    public Exchange Exchange { get; set; } = null!;
    public ICollection<Stock> Stocks { get; set; } = [];
}
