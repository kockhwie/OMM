using OMM.Shared.Validation;

namespace OMM.Shared.Tests.Validation;

public sealed class SearchableTextPolicyTests
{
    [Theory]
    [InlineData("MAIN")]
    [InlineData("Main Market")]
    [InlineData("Main-Market & Co.")]
    public void StandardText_accepts_searchable_latin_text(string value)
    {
        Assert.True(SearchableTextPolicy.IsValidStandardText(value));
    }

    [Fact]
    public void TraditionalChineseText_accepts_cjk_text()
    {
        Assert.True(SearchableTextPolicy.IsValidTraditionalChineseText("主板"));
    }

    [Fact]
    public void SearchableText_rejects_stylized_unicode()
    {
        Assert.False(SearchableTextPolicy.IsValidStandardText("𝕄𝔸𝕀ℕ"));
    }
}
