using OMM.Public.Models;
using OMM.Shared.Models.MasterData;

namespace OMM.Public.Data.Entities;

public class MineEntity : UserOwnedEntity
{
    public required string Name { get; set; }

    public MineCategory Category { get; set; }

    public MineType Type { get; set; }

    public int? InstitutionId { get; set; }

    public Institution? Institution { get; set; }

    public required int CurrencyId { get; set; }

    public Currency? Currency { get; set; }

    public decimal CurrentValue { get; set; }

    public decimal PurchaseCost { get; set; }

    public decimal Growth { get; set; }

    public decimal GrowthPct { get; set; }

    public decimal MonthlyIncome { get; set; }

    public string? Holdings { get; set; }

    public Guid? LinkedBurdenId { get; set; }

    public BurdenEntity? LinkedBurden { get; set; }

    public required string Status { get; set; }

    public DateOnly UpdatedOn { get; set; }

    public ICollection<MinePositionEntity> Positions { get; set; } = [];

    public ICollection<GoalMineEntity> GoalLinks { get; set; } = [];
}
