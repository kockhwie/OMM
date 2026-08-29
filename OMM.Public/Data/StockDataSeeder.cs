using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using OMM.Public.Models.MasterData;

namespace OMM.Public.Data;

public static class StockDataSeeder
{
    public static async Task SeedAsync(ApplicationDbContext dbContext, string jsonPath, CancellationToken cancellationToken = default)
    {
        if (await dbContext.Stocks.AnyAsync(cancellationToken))
        {
            return;
        }

        await using var stream = File.OpenRead(jsonPath);
        var sourceStocks = await JsonSerializer.DeserializeAsync<List<SourceStock>>(stream, cancellationToken: cancellationToken)
            ?? throw new InvalidOperationException($"Could not read stock seed data from '{jsonPath}'.");

        var stocks = sourceStocks.Select(sourceStock => new Stock
        {
            StockCode = sourceStock.Code,
            ShortName_EN = sourceStock.Name,
            MarketId = 1,
            ShariahCompliant = false,
            Currency = "MYR",
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow
        });

        await dbContext.Stocks.AddRangeAsync(stocks, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private sealed class SourceStock
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("code")]
        public string Code { get; set; } = string.Empty;
    }
}
