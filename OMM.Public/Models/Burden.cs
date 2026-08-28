namespace OMM.Public.Models;

public enum BurdenType
{
    Mortgage,
    CreditCard,
    VehicleLoan,
    EducationLoan,
    PersonalLoan,
    TaxPayable,
    Other
}

public class Burden
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public BurdenType Type { get; set; }
    public decimal Balance { get; set; }
    public decimal OriginalAmount { get; set; }
    public decimal InterestRate { get; set; }
    public decimal MonthlyPayment { get; set; }
    public string Currency { get; set; } = "MYR";
    public string? LinkedMineId { get; set; }
    public string? MaturityDate { get; set; }
}
