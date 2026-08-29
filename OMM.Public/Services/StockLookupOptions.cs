namespace OMM.Public.Services;

public sealed class StockLookupOptions
{
    public string Provider { get; set; } = "Database";

    public int CacheDays { get; set; } = 30;
}
