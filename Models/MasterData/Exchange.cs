namespace omm.Models.MasterData;

public class Exchange : AuditableEntity
{
    public int Id { get; set; }
    public int CountryId { get; set; }
    public required string ExchangeCode { get; set; }
    public required string ExchangeName_EN { get; set; }
    public required string ExchangeName_ZH_TW { get; set; }
    public required string ExchangeName_ZH_CN { get; set; }
    public bool IsActive { get; set; }
    public Country Country { get; set; } = null!;
    public ICollection<Market> Markets { get; set; } = [];
}
