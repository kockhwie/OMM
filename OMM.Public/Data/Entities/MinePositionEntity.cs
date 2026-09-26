namespace OMM.Public.Data.Entities;

public class MinePositionEntity : UserOwnedEntity
{
    public required Guid MineId { get; set; }

    public MineEntity? Mine { get; set; }

    public required string Label { get; set; }

    public PositionType PositionType { get; set; }

    public decimal? Principal { get; set; }

    public decimal? Rate { get; set; }

    public DateOnly? MaturityDate { get; set; }

    public string? Quantity { get; set; }

    public decimal? PurchasePrice { get; set; }

    public int? StockId { get; set; }
}
