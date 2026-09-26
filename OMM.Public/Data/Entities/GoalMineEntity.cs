namespace OMM.Public.Data.Entities;

public class GoalMineEntity
{
    public Guid GoalId { get; set; }

    public GoalEntity? Goal { get; set; }

    public Guid MineId { get; set; }

    public MineEntity? Mine { get; set; }

    public required string UserId { get; set; }

    public ApplicationUser? User { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? DeletedAt { get; set; }

    public bool IsDeleted { get; set; }
}
