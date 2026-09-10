using OMM.Shared.Validation;

namespace OMM.Public.Validation;

public static class PublicInputValidator
{
    public static string? GetDisplayTextError(string fieldName, string? value, bool optional = false)
    {
        if (optional && string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return SearchableTextPolicy.IsValidStandardText(value)
            ? null
            : $"{fieldName}: {SearchableTextPolicy.InvalidMessage}";
    }

    public static void ValidateDisplayText(string fieldName, string? value, bool optional = false)
    {
        var error = GetDisplayTextError(fieldName, value, optional);
        if (error is not null)
        {
            throw new ArgumentException(error, fieldName);
        }
    }
}
