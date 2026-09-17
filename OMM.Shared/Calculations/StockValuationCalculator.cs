namespace OMM.Shared.Calculations;

/// <summary>
/// Pure calculation functions for derived stock valuation metrics:
/// P/E ratio, P/B ratio, and Dividend Yield.
/// </summary>
public static class StockValuationCalculator
{
    /// <summary>
    /// Price-to-Earnings ratio: CurrentPrice / EPS.
    /// Returns null if either input is null, or if CurrentPrice &lt;= 0 or EPS &lt;= 0 (guards against divide-by-zero and negative PE).
    /// </summary>
    public static decimal? CalculatePE(decimal? currentPrice, decimal? eps)
    {
        if (!currentPrice.HasValue || !eps.HasValue || currentPrice.Value <= 0 || eps.Value <= 0)
        {
            return null;
        }

        return Math.Round(currentPrice.Value / eps.Value, 4, MidpointRounding.AwayFromZero);
    }

    /// <summary>
    /// Price-to-Book ratio: CurrentPrice / NTA (using book value per share as NTA proxy).
    /// Returns null if either input is null, or if CurrentPrice &lt;= 0 or NTA &lt;= 0 (guards against divide-by-zero and negative PB).
    /// </summary>
    public static decimal? CalculatePB(decimal? currentPrice, decimal? nta)
    {
        if (!currentPrice.HasValue || !nta.HasValue || currentPrice.Value <= 0 || nta.Value <= 0)
        {
            return null;
        }

        return Math.Round(currentPrice.Value / nta.Value, 4, MidpointRounding.AwayFromZero);
    }

    /// <summary>
    /// Dividend Yield percentage: (DPS / CurrentPrice) * 100.
    /// Stored as a percentage (e.g. 4.5 means 4.5%).
    /// Returns null if either input is null, or if CurrentPrice &lt;= 0 or DPS &lt; 0.
    /// </summary>
    public static decimal? CalculateDividendYield(decimal? currentPrice, decimal? dps)
    {
        if (!currentPrice.HasValue || !dps.HasValue || currentPrice.Value <= 0 || dps.Value < 0)
        {
            return null;
        }

        return Math.Round((dps.Value / currentPrice.Value) * 100m, 4, MidpointRounding.AwayFromZero);
    }
}
