using OMM.Public.Models;

namespace OMM.Public.Data.Entities;

public class IncomeRecordEntity : UserOwnedEntity
{
    public required string Source { get; set; }

    public IncomeClass Classification { get; set; }

    public decimal Amount { get; set; }

    public required int CurrencyId { get; set; }

    public Currency? Currency { get; set; }

    public RecordFrequency Frequency { get; set; }

    public Guid? MineId { get; set; }

    public MineEntity? Mine { get; set; }

    public DateOnly RecordDate { get; set; }
}
