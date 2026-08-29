namespace OMM.Public.Models;

public enum IncomeClass
{
    Active,
    PassiveMineGenerated,
    NonRecurring
}

public class IncomeRecord
{
    public string Id { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public IncomeClass Classification { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "MYR";
    public string Frequency { get; set; } = "monthly"; // monthly, annual, one-off
    public string? MineId { get; set; }
    public string? MineName { get; set; }
    public string Date { get; set; } = string.Empty;
}
