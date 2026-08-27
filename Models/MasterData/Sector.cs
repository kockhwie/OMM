namespace omm.Models.MasterData;

public class Sector : AuditableEntity
{
    public int Id { get; set; }
    public int CountryId { get; set; }
    public required string SectorCode { get; set; }
    public required string SectorName_EN { get; set; }
    public required string SectorName_ZH_TW { get; set; }
    public required string SectorName_ZH_CN { get; set; }
    public bool IsActive { get; set; }
    public Country Country { get; set; } = null!;
    public ICollection<SubSector> SubSectors { get; set; } = [];
    public ICollection<Stock> Stocks { get; set; } = [];
}
