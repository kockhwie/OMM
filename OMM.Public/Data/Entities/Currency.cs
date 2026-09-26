namespace OMM.Public.Data.Entities;

public class Currency
{
    public int Id { get; set; }

    public required string Code { get; set; }

    public required string Name_EN { get; set; }

    public bool IsActive { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public bool IsDeleted { get; set; }
}
