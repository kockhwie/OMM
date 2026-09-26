using OMM.Public.Models;

namespace OMM.Public.Data.Entities;

public class GoalEntity : UserOwnedEntity
{
    public required string Title { get; set; }

    public GoalType Type { get; set; }

    public decimal Target { get; set; }

    public decimal Current { get; set; }

    public DateOnly TargetDate { get; set; }

    public required string Status { get; set; }

    public ICollection<GoalMineEntity> MineLinks { get; set; } = [];
}
