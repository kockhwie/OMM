using System.Text.Json;
using omm.Models;

namespace omm.Services;

public interface IKlseStockLookupService
{
    /// <summary>Returns the full KLSE code/name list. Implementations should cache it —
    /// this gets called once per StockAutosuggest instance, and the list rarely changes
    /// within a session.</summary>
    Task<IReadOnlyList<KlseStock>> GetAllAsync();
}

/// <summary>
/// Default implementation: reads wwwroot/data/klse-stocks.json once and caches it in memory.
///
/// You already have a KLSE code/name list — the easiest path is to export it to that same
/// JSON shape (an array of {"code": "...", "name": "..."}) and drop it in wwwroot/data/.
/// If your list instead lives in a database or comes from an API, just write a different
/// IKlseStockLookupService implementation and swap the registration in Program.cs —
/// nothing else in the autosuggest component needs to change.
/// </summary>
public sealed class KlseStockLookupService(IWebHostEnvironment env) : IKlseStockLookupService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private List<KlseStock>? _cache;
    private readonly SemaphoreSlim _lock = new(1, 1);

    public async Task<IReadOnlyList<KlseStock>> GetAllAsync()
    {
        if (_cache is not null)
        {
            return _cache;
        }

        await _lock.WaitAsync();
        try
        {
            if (_cache is not null)
            {
                return _cache;
            }

            var path = Path.Combine(env.WebRootPath, "data", "klse-stocks.json");
            if (!File.Exists(path))
            {
                _cache = [];
                return _cache;
            }

            await using var stream = File.OpenRead(path);
            _cache = await JsonSerializer.DeserializeAsync<List<KlseStock>>(stream, JsonOptions) ?? [];
            return _cache;
        }
        finally
        {
            _lock.Release();
        }
    }
}
