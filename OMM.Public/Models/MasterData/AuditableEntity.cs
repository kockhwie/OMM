namespace OMM.Public.Models.MasterData;

public abstract class AuditableEntity
{
    public string? CreatedByUserId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public string? ModifiedByUserId { get; set; }
    public DateTimeOffset? ModifiedAt { get; set; }
    public string? DeletedByUserId { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
    public bool IsDeleted { get; set; }
}
