using System.Linq.Expressions;
using Microsoft.AspNetCore.Components;

namespace OMM.Admin.Components.Shared.DataGrid;

public partial class GridColumn<TItem> : ComponentBase
{
    [CascadingParameter]
    public AdminDataGrid<TItem>? ParentGrid { get; set; }

    [Parameter]
    public Expression<Func<TItem, object?>>? Field { get; set; }

    [Parameter]
    public Func<TItem, object?>? Value { get; set; }

    [Parameter]
    public string? Title { get; set; }

    [Parameter]
    public string? SortKey { get; set; }

    [Parameter]
    public bool Sortable { get; set; }

    [Parameter]
    public string? CssClass { get; set; }

    [Parameter]
    public string? HeaderCssClass { get; set; }

    [Parameter]
    public string? Format { get; set; }

    [Parameter]
    public RenderFragment<TItem>? Template { get; set; }

    private Func<TItem, object?>? _compiledField;
    private string? _derivedSortKey;

    protected override void OnInitialized()
    {
        if (Field != null)
        {
            _compiledField = Field.Compile();
            _derivedSortKey = ExtractMemberName(Field.Body);
        }

        ParentGrid?.RegisterColumn(this);
    }

    public string ColumnKey => SortKey ?? _derivedSortKey ?? Title ?? string.Empty;

    public string HeaderTitle => Title ?? _derivedSortKey ?? string.Empty;

    public object? GetRawValue(TItem item)
    {
        if (Value != null)
        {
            return Value(item);
        }

        if (_compiledField != null)
        {
            try
            {
                return _compiledField(item);
            }
            catch
            {
                return null;
            }
        }

        return null;
    }

    public string FormatValue(TItem item)
    {
        var raw = GetRawValue(item);
        if (raw == null) return string.Empty;

        if (!string.IsNullOrWhiteSpace(Format) && raw is IFormattable formattable)
        {
            return formattable.ToString(Format, null);
        }

        return raw.ToString() ?? string.Empty;
    }

    private static string? ExtractMemberName(Expression expression)
    {
        if (expression is UnaryExpression unary && unary.Operand is MemberExpression memberUnary)
        {
            return memberUnary.Member.Name;
        }

        if (expression is MemberExpression member)
        {
            return member.Member.Name;
        }

        return null;
    }
}
