using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;

namespace OMM.Shared.Validation;

public static partial class SearchableTextPolicy
{
    public const string DatabaseConstraintName = "CK_Sector_SearchableNames";
    public const string InvalidMessage = "Use standard letters, numbers, spaces, and basic punctuation. Stylized Unicode characters are not supported because they may not be searchable.";

    [GeneratedRegex(@"^(?!.*[\u0250-\u02FF\u1D00-\u1D7F\u1D400-\u1D7FF])[\p{L}\p{Nd}\p{Zs}.,&'()/\-]+$", RegexOptions.CultureInvariant)]
    private static partial Regex StandardTextRegex();

    [GeneratedRegex(@"^(?!.*[\u0250-\u02FF\u1D00-\u1D7F\u1D400-\u1D7FF])[\p{L}\p{Nd}\p{IsCJKUnifiedIdeographs}\p{Zs}.,&'()/\-]+$", RegexOptions.CultureInvariant)]
    private static partial Regex TraditionalChineseTextRegex();

    public static bool IsValidStandardText(string? value) =>
        !string.IsNullOrWhiteSpace(value) && StandardTextRegex().IsMatch(value);

    public static bool IsValidTraditionalChineseText(string? value) =>
        !string.IsNullOrWhiteSpace(value) && TraditionalChineseTextRegex().IsMatch(value);
}

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter)]
public sealed class SearchableTextAttribute(bool allowTraditionalChinese = false) : ValidationAttribute
{
    public bool AllowTraditionalChinese { get; } = allowTraditionalChinese;

    public override bool IsValid(object? value)
    {
        if (value is null)
        {
            return true;
        }

        return value is string text && (AllowTraditionalChinese
            ? SearchableTextPolicy.IsValidTraditionalChineseText(text)
            : SearchableTextPolicy.IsValidStandardText(text));
    }

    public override string FormatErrorMessage(string name) =>
        ErrorMessage ?? $"{name}: {SearchableTextPolicy.InvalidMessage}";
}
