namespace OMM.Public.Data.Entities;

public class ExpenseEntity : UserOwnedEntity
{
    public required string Name { get; set; }

    public decimal Amount { get; set; }

    public required int CurrencyId { get; set; }

    public Currency? Currency { get; set; }

    public RecordFrequency Frequency { get; set; }
}
