using System.Reflection;
using System.Text.Json;

namespace OMM.Shared.Prompts;

public sealed class EmergencyPromptCatalog
{
    private readonly IReadOnlyList<EmergencyPromptDto> _prompts;

    public EmergencyPromptCatalog()
    {
        _prompts = new[]
        {
            Load("standard-emergency-v1.json"),
            Load("pro-emergency-v1.json")
        };

        if (_prompts.Any(prompt => !prompt.IsValid))
        {
            throw new InvalidOperationException("An embedded Phase 5 emergency prompt is missing or has an invalid content hash.");
        }
    }

    public IReadOnlyList<EmergencyPromptDto> GetAll() => _prompts;

    public EmergencyPromptDto? Get(string promptModeCode) =>
        _prompts.FirstOrDefault(prompt => string.Equals(prompt.PromptModeCode, promptModeCode, StringComparison.OrdinalIgnoreCase) && prompt.IsValid);

    private static EmergencyPromptDto Load(string fileName)
    {
        var assembly = typeof(EmergencyPromptCatalog).Assembly;
        var resourceName = assembly.GetManifestResourceNames()
            .SingleOrDefault(name => name.EndsWith($"EmergencyPrompts.{fileName}", StringComparison.Ordinal));

        if (resourceName is null)
        {
            throw new InvalidOperationException($"Embedded emergency prompt resource '{fileName}' was not found.");
        }

        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Embedded emergency prompt resource '{fileName}' could not be opened.");
        var resource = JsonSerializer.Deserialize<EmergencyPromptResource>(stream)
            ?? throw new InvalidOperationException($"Embedded emergency prompt resource '{fileName}' is invalid JSON.");
        var calculatedHash = PromptContentHash.Compute(
            resource.PromptModeCode,
            resource.SchemaVersion,
            resource.SystemInstruction,
            resource.UserMessageTemplate);

        return new EmergencyPromptDto(
            resource.PromptModeCode,
            resource.VersionLabel,
            resource.SystemInstruction,
            resource.UserMessageTemplate,
            resource.SchemaVersion,
            resource.ContentHash,
            string.Equals(calculatedHash, resource.ContentHash, StringComparison.OrdinalIgnoreCase));
    }

    private sealed class EmergencyPromptResource
    {
        public string PromptModeCode { get; init; } = string.Empty;
        public string VersionLabel { get; init; } = string.Empty;
        public string SystemInstruction { get; init; } = string.Empty;
        public string UserMessageTemplate { get; init; } = string.Empty;
        public string SchemaVersion { get; init; } = string.Empty;
        public string ContentHash { get; init; } = string.Empty;
    }
}
