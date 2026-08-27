namespace omm.Models.MasterData;

public class SubSector : AuditableEntity
{
    public int Id { get; set; }
    public int SectorId { get; set; }
    public required string SubSectorCode { get; set; }
    public required string SubSectorName_EN { get; set; }
    public required string SubSectorName_ZH_TW { get; set; }
    public required string SubSectorName_ZH_CN { get; set; }
    public bool IsActive { get; set; }
    public Sector Sector { get; set; } = null!;
    public ICollection<Stock> Stocks { get; set; } = [];
}
