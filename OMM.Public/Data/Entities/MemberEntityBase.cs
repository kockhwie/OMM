namespace OMM.Public.Data.Entities;

public abstract class UserOwnedEntity
{
    public Guid Id { get; set; }

    public required string UserId { get; set; }

    public ApplicationUser? User { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? ModifiedAt { get; set; }

    public DateTimeOffset? DeletedAt { get; set; }

    public bool IsDeleted { get; set; }
}
