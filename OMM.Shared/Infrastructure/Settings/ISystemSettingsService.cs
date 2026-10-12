namespace OMM.Shared.Infrastructure.Settings;

public static class SystemSettingKeys
{
    public const string GoogleAuthEnabled = "Authentication:GoogleEnabled";
    public const string AiClassificationEnabled = "DecisionSupport:AiClassificationEnabled";
}

public interface ISystemSettingsService
{
    Task<string?> GetSettingAsync(string key, CancellationToken ct = default);
    Task<bool> GetBoolSettingAsync(string key, bool defaultValue = false, CancellationToken ct = default);
    Task SetSettingAsync(string key, string value, string? userId = null, string? description = null, CancellationToken ct = default);
    Task<bool> IsGoogleAuthEnabledAsync(CancellationToken ct = default);
    Task SetGoogleAuthEnabledAsync(bool enabled, string? userId = null, CancellationToken ct = default);
    Task<bool> IsAiClassificationEnabledAsync(CancellationToken ct = default);
    Task SetAiClassificationEnabledAsync(bool enabled, string? userId = null, CancellationToken ct = default);
}
