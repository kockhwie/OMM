using System.Security.Cryptography;
using System.Text;

namespace OMM.Shared.Prompts;

public static class PromptContentHash
{
    public static string Compute(string promptModeCode, string schemaVersion, string systemInstruction, string userMessageTemplate)
    {
        var canonical = string.Join('\n', promptModeCode, schemaVersion, systemInstruction, userMessageTemplate);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical))).ToLowerInvariant();
    }
}
