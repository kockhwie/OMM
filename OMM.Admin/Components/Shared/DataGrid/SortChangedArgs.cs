namespace OMM.Admin.Components.Shared.DataGrid;

public enum SortDirection
{
    None,
    Ascending,
    Descending
}

public record SortChangedArgs(string ColumnKey, SortDirection Direction);
