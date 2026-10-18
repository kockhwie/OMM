namespace OMM.Shared.DecisionSupport;

/// <summary>
/// Provides portfolio and position-level context for decision pathway workflows.
/// </summary>
public interface IPortfolioContextProvider
{
    /// <summary>
    /// Looks up a user's position for a specific stock or asset by ticker code, name, or combined query (e.g., "Maybank (1155)").
    /// Returns null if the user has no matching position or is unauthenticated.
    /// </summary>
    Task<PositionContext?> GetPositionAsync(string userId, string stockCodeOrName, CancellationToken ct = default);

    /// <summary>
    /// Retrieves a snapshot of the user's total portfolio and individual stock positions.
    /// </summary>
    Task<PortfolioSnapshot> GetSnapshotAsync(string userId, CancellationToken ct = default);
}

/// <summary>
/// Contextual information about a specific position held by a user.
/// </summary>
public sealed record PositionContext(
    string StockCode,
    decimal Shares,
    decimal CostBasis,
    decimal MarketValue,
    decimal WeightPct,
    decimal AnnualDividend,
    string StockName = "")
{
    public decimal TotalCost => Shares * CostBasis;
}

/// <summary>
/// Portfolio-level snapshot summarizing total mines value and stock holdings.
/// </summary>
public sealed record PortfolioSnapshot(
    decimal TotalMinesValue,
    int StockPositionsCount,
    IReadOnlyList<PositionContext> Positions);
