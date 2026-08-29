using Dapper;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using OMM.Public.Models;
using System.Text.Json;

namespace OMM.Public.Services;

public interface IKlseStockLookupService
{
    /// <summary>Returns the full KLSE code/name list. Implementations should cache it —
    /// this gets called once per StockAutosuggest instance, and the list rarely changes
    /// within a session.</summary>
    Task<IReadOnlyList<KlseStock>> GetAllAsync();

    /// <summary>Removes the cached list so the next lookup reloads the configured source.</summary>
    Task RefreshAsync();
}

/// <summary>
/// Default implementation: reads the active stock lookup fields from PostgreSQL once
/// and caches them in memory. The UI contract remains independent of the data provider.
/// </summary>
public sealed class KlseStockLookupService(
    Npgsql.NpgsqlDataSource dataSource,
    IWebHostEnvironment environment,
    IMemoryCache cache,
    IOptions<StockLookupOptions> options) : IKlseStockLookupService
{
    private const string CacheKey = "stock-lookup:active-stocks";
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly SemaphoreSlim _lock = new(1, 1);

    public async Task<IReadOnlyList<KlseStock>> GetAllAsync()
    {
        if (cache.TryGetValue(CacheKey, out IReadOnlyList<KlseStock>? cachedStocks))
        {
            return cachedStocks!;
        }

        await _lock.WaitAsync();
        try
        {
            if (cache.TryGetValue(CacheKey, out cachedStocks))
            {
                return cachedStocks!;
            }

            var provider = options.Value.Provider?.Trim();
            var stocks = provider?.ToUpperInvariant() switch
            {
                null or "" or "DATABASE" => await LoadFromDatabaseAsync(),
                "JSON" => await LoadFromJsonAsync(),
                _ => throw new InvalidOperationException(
                    "Invalid StockLookup:Provider. Use 'Database' or 'Json'.")
            };

            cache.Set<IReadOnlyList<KlseStock>>(
                CacheKey,
                stocks,
                new MemoryCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromDays(options.Value.CacheDays)
                });

            return stocks;
        }
        finally
        {
            _lock.Release();
        }
    }

    public Task RefreshAsync()
    {
        cache.Remove(CacheKey);
        return Task.CompletedTask;
    }

    private async Task<List<KlseStock>> LoadFromDatabaseAsync()
    {
        const string sql = """
            SELECT "Id",
                   "StockCode" AS "Code",
                   "ShortName_EN" AS "Name"
            FROM "Stock"
            WHERE "IsDeleted" = FALSE
              AND "IsActive" = TRUE
            ORDER BY "StockCode";
            """;

        await using var connection = await dataSource.OpenConnectionAsync();
        var stocks = await connection.QueryAsync<KlseStock>(sql);
        return stocks.ToList();
    }

    private async Task<List<KlseStock>> LoadFromJsonAsync()
    {
        var path = Path.Combine(environment.WebRootPath, "data", "klse-stocks.json");
        if (!File.Exists(path))
        {
            return [];
        }

        await using var stream = File.OpenRead(path);
        return await JsonSerializer.DeserializeAsync<List<KlseStock>>(stream, JsonOptions) ?? [];
    }
}
