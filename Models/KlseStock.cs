namespace omm.Models;

/// <summary>A single KLSE-listed counter: its Bursa stock code and company name.</summary>
public class KlseStock
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
}
