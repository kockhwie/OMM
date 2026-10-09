using System;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace OMM.Shared.Infrastructure.Settings;

public sealed class SystemSettingsService(
    NpgsqlDataSource dataSource,
    IMemoryCache cache,
    ILogger<SystemSettingsService> logger) : ISystemSettingsService
{
    private static readonly TimeSpan CacheDuration = TimeSpan.FromSeconds(60);
    private static string GetCacheKey(string key) => $"sys_setting_{key}";

    public async Task<string?> GetSettingAsync(string key, CancellationToken ct = default)
    {
        var cacheKey = GetCacheKey(key);
        if (cache.TryGetValue<string>(cacheKey, out var cachedValue))
        {
            return cachedValue;
        }

        try
        {
            await using var connection = await dataSource.OpenConnectionAsync(ct);
            await using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT "Value"
                FROM public."SystemSetting"
                WHERE "Key" = @key
                LIMIT 1;
                """;
            command.Parameters.AddWithValue("key", key);

            var result = await command.ExecuteScalarAsync(ct);
            var value = result is null or DBNull ? null : Convert.ToString(result);

            if (value is not null)
            {
                cache.Set(cacheKey, value, CacheDuration);
            }

            return value;
        }
        catch (Exception ex) when (ex is DbException or NpgsqlException)
        {
            logger.LogWarning(ex, "Failed to read system setting '{Key}' from database. Falling back to null.", key);
            return null;
        }
    }

    public async Task<bool> GetBoolSettingAsync(string key, bool defaultValue = false, CancellationToken ct = default)
    {
        var raw = await GetSettingAsync(key, ct);
        if (string.IsNullOrWhiteSpace(raw))
        {
            return defaultValue;
        }

        return bool.TryParse(raw, out var parsed) ? parsed : defaultValue;
    }

    public async Task SetSettingAsync(string key, string value, string? userId = null, string? description = null, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(value);

        try
        {
            await using var connection = await dataSource.OpenConnectionAsync(ct);
            await using var command = connection.CreateCommand();
            command.CommandText = """
                INSERT INTO public."SystemSetting" ("Key", "Value", "Description", "ModifiedAt", "ModifiedByUserId")
                VALUES (@key, @value, @description, CURRENT_TIMESTAMP, @userId)
                ON CONFLICT ("Key") DO UPDATE
                SET "Value" = EXCLUDED."Value",
                    "Description" = COALESCE(EXCLUDED."Description", public."SystemSetting"."Description"),
                    "ModifiedAt" = CURRENT_TIMESTAMP,
                    "ModifiedByUserId" = EXCLUDED."ModifiedByUserId";
                """;
            command.Parameters.AddWithValue("key", key);
            command.Parameters.AddWithValue("value", value);
            command.Parameters.AddWithValue("description", (object?)description ?? DBNull.Value);
            command.Parameters.AddWithValue("userId", (object?)userId ?? DBNull.Value);

            await command.ExecuteNonQueryAsync(ct);

            // Update cache immediately
            cache.Set(GetCacheKey(key), value, CacheDuration);
            logger.LogInformation("System setting '{Key}' updated to '{Value}' by user '{UserId}'.", key, value, userId ?? "System");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to update system setting '{Key}' to '{Value}'.", key, value);
            throw;
        }
    }

    public Task<bool> IsGoogleAuthEnabledAsync(CancellationToken ct = default)
    {
        return GetBoolSettingAsync(SystemSettingKeys.GoogleAuthEnabled, defaultValue: false, ct);
    }

    public Task SetGoogleAuthEnabledAsync(bool enabled, string? userId = null, CancellationToken ct = default)
    {
        return SetSettingAsync(
            SystemSettingKeys.GoogleAuthEnabled,
            enabled ? "true" : "false",
            userId,
            "Controls whether members can log in or register via Google external authentication.",
            ct);
    }
}
