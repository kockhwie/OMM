using OMM.Public.Models;

namespace OMM.Public.Data.Entities;

public class BurdenEntity : UserOwnedEntity
{
    public required string Name { get; set; }

    public BurdenType Type { get; set; }

    public decimal Balance { get; set; }

    public decimal OriginalAmount { get; set; }

    public decimal InterestRate { get; set; }

    public decimal MonthlyPayment { get; set; }

    public required int CurrencyId { get; set; }

    public Currency? Currency { get; set; }

    public Guid? LinkedMineId { get; set; }

    public MineEntity? LinkedMine { get; set; }

    public DateOnly? MaturityDate { get; set; }
}
