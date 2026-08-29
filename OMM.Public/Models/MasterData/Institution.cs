namespace OMM.Public.Models.MasterData;

public enum InstitutionCategory
{
    Bank,
    Broker,
    EpfKwsp,
    GoldProvider,
    Insurance,
    Other
}

public class Institution : AuditableEntity
{
    public int Id { get; set; }
    public int? CountryId { get; set; }
    public required string InstitutionCode { get; set; }
    public required string InstitutionName_EN { get; set; }
    public required string InstitutionName_ZH_TW { get; set; }
    public required string InstitutionName_ZH_CN { get; set; }
    public InstitutionCategory InstitutionCategory { get; set; }
    public bool IsActive { get; set; }
    public Country? Country { get; set; }
}
