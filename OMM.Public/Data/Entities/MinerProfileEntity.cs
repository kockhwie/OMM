using OMM.Shared.Models.MasterData;

namespace OMM.Public.Data.Entities;

public class MinerProfileEntity
{
    public required string UserId { get; set; }

    public ApplicationUser? User { get; set; }

    public required string Email { get; set; }

    public string? DisplayName { get; set; }

    public int? CountryId { get; set; }

    public Country? Country { get; set; }

    public int? CurrencyId { get; set; }

    public Currency? Currency { get; set; }

    public string? Language { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? ModifiedAt { get; set; }

    public DateTimeOffset? DeletedAt { get; set; }

    public bool IsDeleted { get; set; }
}
