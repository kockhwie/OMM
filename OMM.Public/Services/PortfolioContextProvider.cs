using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using OMM.Public.Data.Entities;
using OMM.Public.Models;
using OMM.Shared.DecisionSupport;

namespace OMM.Public.Services;

/// <summary>
/// Extracts user portfolio and position context using IMineRepository,
/// matching user-held stocks against decision pathway queries.
/// </summary>
public sealed class PortfolioContextProvider(
    IMineRepository repository,
    ILogger<PortfolioContextProvider> logger) : IPortfolioContextProvider
{
    private static readonly Regex ParensRegex = new(@"\(([^)]+)\)", RegexOptions.Compiled);

    public async Task<PositionContext?> GetPositionAsync(string userId, string stockCodeOrName, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(stockCodeOrName))
        {
            return null;
        }

        var mines = await GetMinesSafelyAsync(userId, ct);
        if (mines.Count == 0)
        {
            return null;
        }

        var totalMinesValue = CalculateTotalValuedMines(mines);
        var stockMines = GetStockMinesWithMetadata(mines);

        if (stockMines.Count == 0)
        {
            return null;
        }

        var parsed = ParseQuery(stockCodeOrName);
        var scoredMines = stockMines
            .Select(item => (Item: item, Score: ComputeMatchScore(item.Mine, item.Meta, parsed)))
            .Where(x => x.Score > 0)
            .OrderByDescending(x => x.Score)
            .ToList();

        if (scoredMines.Count == 0)
        {
            return null;
        }

        // Take the top matched stock identifier (code or name) to aggregate multiple lots/mines if present
        var best = scoredMines[0].Item;
        var bestCode = best.Meta?.StockCode?.Trim();
        var bestName = best.Mine.Name?.Trim();

        var matches = scoredMines
            .Where(x => (!string.IsNullOrEmpty(bestCode) && string.Equals(x.Item.Meta?.StockCode?.Trim(), bestCode, StringComparison.OrdinalIgnoreCase))
                     || (!string.IsNullOrEmpty(bestName) && string.Equals(x.Item.Mine.Name?.Trim(), bestName, StringComparison.OrdinalIgnoreCase)))
            .Select(x => x.Item)
            .ToList();

        var totalShares = 0m;
        var totalMarketValue = 0m;
        var totalPurchaseCost = 0m;
        var totalAnnualDividend = 0m;

        foreach (var (mine, meta) in matches)
        {
            var shares = CalculateShares(mine, meta);
            var marketValue = CalculateMarketValue(mine, meta, shares);
            var purchaseCost = CalculatePurchaseCost(mine, meta, shares);
            var dividend = CalculateAnnualDividend(mine, meta, marketValue);

            totalShares += shares;
            totalMarketValue += marketValue;
            totalPurchaseCost += purchaseCost;
            totalAnnualDividend += dividend;
        }

        var costBasis = totalShares > 0 && totalPurchaseCost > 0
            ? Math.Round(totalPurchaseCost / totalShares, 4)
            : (best.Meta?.PurchasePricePerShare ?? 0m);

        var weightPct = totalMinesValue > 0
            ? Math.Round(totalMarketValue / totalMinesValue * 100m, 2)
            : 0m;

        var resolvedCode = !string.IsNullOrWhiteSpace(bestCode)
            ? bestCode
            : (!string.IsNullOrWhiteSpace(parsed.CodeInsideParens) ? parsed.CodeInsideParens : parsed.Raw);

        var resolvedName = !string.IsNullOrWhiteSpace(bestName)
            ? bestName
            : resolvedCode;

        return new PositionContext(
            StockCode: resolvedCode,
            Shares: totalShares,
            CostBasis: costBasis,
            MarketValue: totalMarketValue,
            WeightPct: weightPct,
            AnnualDividend: totalAnnualDividend,
            StockName: resolvedName);
    }

    public async Task<PortfolioSnapshot> GetSnapshotAsync(string userId, CancellationToken ct = default)
    {
        var mines = await GetMinesSafelyAsync(userId, ct);
        if (mines.Count == 0)
        {
            return new PortfolioSnapshot(0m, 0, []);
        }

        var totalMinesValue = CalculateTotalValuedMines(mines);
        var stockMines = GetStockMinesWithMetadata(mines);

        if (stockMines.Count == 0)
        {
            return new PortfolioSnapshot(totalMinesValue, 0, []);
        }

        // Group by StockCode or Mine Name to consolidate multiple entries
        var grouped = stockMines
            .GroupBy(item => !string.IsNullOrWhiteSpace(item.Meta?.StockCode)
                ? item.Meta.StockCode.Trim().ToUpperInvariant()
                : item.Mine.Name.Trim().ToUpperInvariant())
            .ToList();

        var positions = new List<PositionContext>(grouped.Count);

        foreach (var group in grouped)
        {
            var items = group.ToList();
            var first = items[0];

            var totalShares = 0m;
            var totalMarketValue = 0m;
            var totalPurchaseCost = 0m;
            var totalAnnualDividend = 0m;

            foreach (var (mine, meta) in items)
            {
                var shares = CalculateShares(mine, meta);
                var marketValue = CalculateMarketValue(mine, meta, shares);
                var purchaseCost = CalculatePurchaseCost(mine, meta, shares);
                var dividend = CalculateAnnualDividend(mine, meta, marketValue);

                totalShares += shares;
                totalMarketValue += marketValue;
                totalPurchaseCost += purchaseCost;
                totalAnnualDividend += dividend;
            }

            var costBasis = totalShares > 0 && totalPurchaseCost > 0
                ? Math.Round(totalPurchaseCost / totalShares, 4)
                : (first.Meta?.PurchasePricePerShare ?? 0m);

            var weightPct = totalMinesValue > 0
                ? Math.Round(totalMarketValue / totalMinesValue * 100m, 2)
                : 0m;

            var resolvedCode = !string.IsNullOrWhiteSpace(first.Meta?.StockCode)
                ? first.Meta.StockCode.Trim()
                : first.Mine.Name.Trim();

            var resolvedName = !string.IsNullOrWhiteSpace(first.Mine.Name)
                ? first.Mine.Name.Trim()
                : resolvedCode;

            positions.Add(new PositionContext(
                StockCode: resolvedCode,
                Shares: totalShares,
                CostBasis: costBasis,
                MarketValue: totalMarketValue,
                WeightPct: weightPct,
                AnnualDividend: totalAnnualDividend,
                StockName: resolvedName));
        }

        return new PortfolioSnapshot(
            TotalMinesValue: totalMinesValue,
            StockPositionsCount: positions.Count,
            Positions: positions.OrderByDescending(p => p.MarketValue).ToList());
    }

    private async Task<IReadOnlyList<MineEntity>> GetMinesSafelyAsync(string userId, CancellationToken ct)
    {
        try
        {
            var list = await repository.GetMinesAsync(ct);
            if (!string.IsNullOrWhiteSpace(userId))
            {
                list = list.Where(m => string.IsNullOrEmpty(m.UserId) || string.Equals(m.UserId, userId, StringComparison.Ordinal)).ToList();
            }
            return list;
        }
        catch (InvalidOperationException exception)
        {
            logger.LogDebug(exception, "User is unauthenticated or has no active context for portfolio extraction.");
            return [];
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Failed to load user mines for decision support context.");
            return [];
        }
    }

    private static decimal CalculateTotalValuedMines(IReadOnlyList<MineEntity> mines)
    {
        return mines.Sum(mine => mine.CurrentValue > 0 ? mine.CurrentValue : 0m);
    }

    private static List<(MineEntity Mine, MineMetadata? Meta)> GetStockMinesWithMetadata(IReadOnlyList<MineEntity> mines)
    {
        var result = new List<(MineEntity Mine, MineMetadata? Meta)>();

        foreach (var mine in mines)
        {
            var meta = ParseMetadata(mine);
            var isStock = mine.Category == MineCategory.Investments
                || mine.Type is MineType.Stocks or MineType.StocksUs or MineType.Reit or MineType.Etf
                || !string.IsNullOrWhiteSpace(meta?.StockCode);

            if (isStock)
            {
                result.Add((mine, meta));
            }
        }

        return result;
    }

    private static MineMetadata? ParseMetadata(MineEntity mine)
    {
        if (string.IsNullOrWhiteSpace(mine.MetadataJson)) return null;
        try
        {
            return JsonSerializer.Deserialize<MineMetadata>(mine.MetadataJson);
        }
        catch
        {
            return null;
        }
    }

    private static decimal CalculateShares(MineEntity mine, MineMetadata? meta)
    {
        if (meta?.SharesOwned is { } shares && shares > 0)
        {
            return shares;
        }

        if (mine.Positions is { Count: > 0 })
        {
            var total = 0m;
            foreach (var pos in mine.Positions)
            {
                if (!string.IsNullOrWhiteSpace(pos.Quantity) && decimal.TryParse(pos.Quantity, out var q))
                {
                    total += q;
                }
            }
            if (total > 0) return total;
        }

        return 0m;
    }

    private static decimal CalculateMarketValue(MineEntity mine, MineMetadata? meta, decimal shares)
    {
        if (mine.CurrentValue > 0)
        {
            return mine.CurrentValue;
        }

        if (shares > 0 && meta?.CurrentSharePrice is { } price && price > 0)
        {
            return shares * price;
        }

        return 0m;
    }

    private static decimal CalculatePurchaseCost(MineEntity mine, MineMetadata? meta, decimal shares)
    {
        if (mine.PurchaseCost > 0)
        {
            return mine.PurchaseCost;
        }

        if (shares > 0 && meta?.PurchasePricePerShare is { } costPerShare && costPerShare > 0)
        {
            return shares * costPerShare;
        }

        return 0m;
    }

    private static decimal CalculateAnnualDividend(MineEntity mine, MineMetadata? meta, decimal marketValue)
    {
        if (mine.MonthlyIncome > 0)
        {
            return mine.MonthlyIncome * 12m;
        }

        if (meta?.DividendYieldPct is { } yieldPct && yieldPct > 0 && marketValue > 0)
        {
            return Math.Round(marketValue * (yieldPct / 100m), 2);
        }

        return 0m;
    }

    private static QueryTokens ParseQuery(string query)
    {
        var raw = query.Trim();
        var codeInParens = string.Empty;
        var nameBeforeParens = string.Empty;

        var match = ParensRegex.Match(raw);
        if (match.Success)
        {
            codeInParens = match.Groups[1].Value.Trim();
            nameBeforeParens = raw[..match.Index].Trim();
        }

        var slashParts = raw.Split(['/', '-'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        return new QueryTokens(raw, codeInParens, nameBeforeParens, slashParts);
    }

    private static int ComputeMatchScore(MineEntity mine, MineMetadata? meta, QueryTokens query)
    {
        var stockCode = meta?.StockCode?.Trim();
        var mineName = mine.Name?.Trim();

        // 1. Exact match on StockCode
        if (!string.IsNullOrEmpty(stockCode))
        {
            if (string.Equals(stockCode, query.Raw, StringComparison.OrdinalIgnoreCase)) return 100;
            if (!string.IsNullOrEmpty(query.CodeInsideParens) && string.Equals(stockCode, query.CodeInsideParens, StringComparison.OrdinalIgnoreCase)) return 100;
            if (!string.IsNullOrEmpty(query.NameBeforeParens) && string.Equals(stockCode, query.NameBeforeParens, StringComparison.OrdinalIgnoreCase)) return 95;
            foreach (var part in query.SlashParts)
            {
                if (string.Equals(stockCode, part, StringComparison.OrdinalIgnoreCase)) return 95;
            }
        }

        // 2. Exact match on Mine Name
        if (!string.IsNullOrEmpty(mineName))
        {
            if (string.Equals(mineName, query.Raw, StringComparison.OrdinalIgnoreCase)) return 90;
            if (!string.IsNullOrEmpty(query.NameBeforeParens) && string.Equals(mineName, query.NameBeforeParens, StringComparison.OrdinalIgnoreCase)) return 90;
            if (!string.IsNullOrEmpty(query.CodeInsideParens) && string.Equals(mineName, query.CodeInsideParens, StringComparison.OrdinalIgnoreCase)) return 90;
            foreach (var part in query.SlashParts)
            {
                if (string.Equals(mineName, part, StringComparison.OrdinalIgnoreCase)) return 85;
            }
        }

        // 3. Substring match on StockCode
        if (!string.IsNullOrEmpty(stockCode))
        {
            if (query.Raw.Contains(stockCode, StringComparison.OrdinalIgnoreCase)) return 75;
            if (stockCode.Contains(query.Raw, StringComparison.OrdinalIgnoreCase)) return 70;
            if (!string.IsNullOrEmpty(query.CodeInsideParens) && query.CodeInsideParens.Contains(stockCode, StringComparison.OrdinalIgnoreCase)) return 75;
        }

        // 4. Substring match on Mine Name
        if (!string.IsNullOrEmpty(mineName))
        {
            if (!string.IsNullOrEmpty(query.NameBeforeParens) && mineName.Contains(query.NameBeforeParens, StringComparison.OrdinalIgnoreCase)) return 65;
            if (mineName.Contains(query.Raw, StringComparison.OrdinalIgnoreCase)) return 60;
            if (query.Raw.Contains(mineName, StringComparison.OrdinalIgnoreCase)) return 60;
        }

        // 5. Holdings text match fallback
        if (!string.IsNullOrEmpty(mine.Holdings) && mine.Holdings.Contains(query.Raw, StringComparison.OrdinalIgnoreCase))
        {
            return 50;
        }

        return 0;
    }

    private sealed record QueryTokens(
        string Raw,
        string CodeInsideParens,
        string NameBeforeParens,
        IReadOnlyList<string> SlashParts);
}
