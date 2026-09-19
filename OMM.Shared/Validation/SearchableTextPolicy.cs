using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;
using System.Text;

namespace OMM.Shared.Validation;

public static partial class SearchableTextPolicy
{
    public const string DatabaseConstraintName = "CK_Sector_SearchableNames";
    public const string InvalidMessage = "Use standard letters, numbers, spaces, and basic punctuation. Stylized Unicode characters are not supported because they may not be searchable.";

    [GeneratedRegex(@"^[\p{L}\p{Nd}\p{Zs}.,&'()/\-]+$", RegexOptions.CultureInvariant)]
    private static partial Regex StandardTextRegex();

    [GeneratedRegex(@"^[\p{L}\p{Nd}\p{IsCJKUnifiedIdeographs}\p{Zs}.,&'()/\-]+$", RegexOptions.CultureInvariant)]
    private static partial Regex TraditionalChineseTextRegex();

    public static bool IsValidStandardText(string? value) =>
        !string.IsNullOrWhiteSpace(value) && !ContainsStylizedUnicode(value) && StandardTextRegex().IsMatch(value);

    public static bool IsValidTraditionalChineseText(string? value) =>
        !string.IsNullOrWhiteSpace(value) && !ContainsStylizedUnicode(value) && TraditionalChineseTextRegex().IsMatch(value);

    private static bool ContainsStylizedUnicode(string value) => value.EnumerateRunes().Any(rune =>
        rune.Value is >= 0x0250 and <= 0x02FF or
        >= 0x1D00 and <= 0x1D7F or
        >= 0x1D400 and <= 0x1D7FF);
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
