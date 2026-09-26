namespace OMM.Public.Data.Entities;

public class NotificationEntity : UserOwnedEntity
{
    public required string Title { get; set; }

    public required string Body { get; set; }

    public required string Type { get; set; }

    public DateOnly OccurredOn { get; set; }

    public DateTimeOffset? ReadAt { get; set; }

    public DateTimeOffset? DismissedAt { get; set; }

    public required string DedupeKey { get; set; }
}
