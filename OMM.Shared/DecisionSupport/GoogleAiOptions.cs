namespace OMM.Shared.DecisionSupport;

public sealed class GoogleAiOptions
{
    public bool Enabled { get; set; }
    public string ApiKey { get; set; } = string.Empty;
    /// <summary>Models are tried in order. The first successful structured response wins.</summary>
    public List<string> Models { get; set; } = [];
    /// <summary>Legacy single-model setting; used only when Models is empty.</summary>
    public string Model { get; set; } = string.Empty;
    public string BaseUrl { get; set; } = "https://generativelanguage.googleapis.com/v1beta";
    public int TimeoutSeconds { get; set; } = 20;
}
