namespace OMM.Public.Models;

public class Expense
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Currency { get; set; } = string.Empty;
    public string Frequency { get; set; } = "monthly";
    public string CreatedAt { get; set; } = string.Empty;
    public string? ModifiedAt { get; set; }
}
