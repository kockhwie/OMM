using System.Numerics;

namespace OMM.Shared.Models.MasterData;

public class Country : AuditableEntity
{
    public int Id { get; set; }
    public required string CountryCode { get; set; }
    public required string CountryName_EN { get; set; }
    public required string CountryName_ZH_TW { get; set; }
    public required string CountryName_ZH_CN { get; set; }
    public required string DefaultCurrencyCode { get; set; }
    public bool IsActive { get; set; }
    public ICollection<Exchange> Exchanges { get; set; } = [];
    public ICollection<Sector> Sectors { get; set; } = [];
    public ICollection<Institution> Institutions { get; set; } = [];
}
