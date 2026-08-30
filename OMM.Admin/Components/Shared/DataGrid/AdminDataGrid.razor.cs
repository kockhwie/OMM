using System.Timers;
using Microsoft.AspNetCore.Components;

namespace OMM.Admin.Components.Shared.DataGrid;

public partial class AdminDataGrid<TItem> : ComponentBase, IDisposable
{
    private readonly List<GridColumn<TItem>> _columns = [];
    private string _searchTerm = string.Empty;
    private string? _sortColumn;
    private SortDirection _sortDirection = SortDirection.None;
    private System.Timers.Timer? _debounceTimer;

    [Parameter]
    public IEnumerable<TItem>? Items { get; set; }

    [Parameter]
    public int TotalCount { get; set; }

    [Parameter]
    public int PageSize { get; set; } = 20;

    /// <summary>
    /// Required for Blazor @bind-PageSize two-way binding.
    /// Raised after PageSize changes via the page-size selector.
    /// </summary>
    [Parameter]
    public EventCallback<int> PageSizeChanged { get; set; }

    [Parameter]
    public int[] PageSizeOptions { get; set; } = [10, 20, 50, 100];

    [Parameter]
    public int CurrentPage { get; set; } = 1;

    [Parameter]
    public EventCallback<int> CurrentPageChanged { get; set; }

    [Parameter]
    public bool IsLoading { get; set; }

    [Parameter]
    public bool ServerSide { get; set; }

    [Parameter]
    public bool ShowSearch { get; set; } = true;

    [Parameter]
    public bool ShowPageSizeSelector { get; set; } = true;

    [Parameter]
    public string SearchPlaceholder { get; set; } = "Search...";

    [Parameter]
    public int SearchDebounceMs { get; set; } = 300;

    [Parameter]
    public string ActionColumnTitle { get; set; } = "Actions";

    [Parameter]
    public RenderFragment? Columns { get; set; }

    [Parameter]
    public RenderFragment<TItem>? ActionColumn { get; set; }

    [Parameter]
    public RenderFragment? EmptyTemplate { get; set; }

    [Parameter]
    public RenderFragment? LoadingTemplate { get; set; }

    [Parameter]
    public RenderFragment? ToolbarTemplate { get; set; }

    [Parameter]
    public EventCallback<int> OnPageChanged { get; set; }

    [Parameter]
    public EventCallback<int> OnPageSizeChanged { get; set; }

    [Parameter]
    public EventCallback<SortChangedArgs> OnSortChanged { get; set; }

    [Parameter]
    public EventCallback<string> OnSearchChanged { get; set; }

    [Parameter]
    public EventCallback<TItem> OnRowClick { get; set; }

    internal void RegisterColumn(GridColumn<TItem> column)
    {
        if (!_columns.Contains(column))
        {
            _columns.Add(column);
            StateHasChanged();
        }
    }

    public int TotalColumnCount => _columns.Count + (ActionColumn != null ? 1 : 0);

    public int TotalItemCount => ServerSide ? TotalCount : (Items?.Count() ?? 0);

    public int TotalPages => Math.Max(1, (int)Math.Ceiling(TotalItemCount / (double)PageSize));

    public int ItemRangeStart => TotalItemCount == 0 ? 0 : ((CurrentPage - 1) * PageSize) + 1;

    public int ItemRangeEnd => Math.Min(CurrentPage * PageSize, TotalItemCount);

    public IEnumerable<TItem> ProcessedItems
    {
        get
        {
            if (Items == null) return [];

            if (ServerSide)
            {
                return Items;
            }

            var query = Items.AsEnumerable();

            // Client-side search
            if (!string.IsNullOrWhiteSpace(_searchTerm))
            {
                query = query.Where(item =>
                {
                    foreach (var col in _columns)
                    {
                        var val = col.GetRawValue(item)?.ToString();
                        if (val != null && val.Contains(_searchTerm, StringComparison.OrdinalIgnoreCase))
                        {
                            return true;
                        }
                    }
                    return false;
                });
            }

            // Client-side sort
            if (!string.IsNullOrEmpty(_sortColumn) && _sortDirection != SortDirection.None)
            {
                var targetCol = _columns.FirstOrDefault(c => c.ColumnKey == _sortColumn);
                if (targetCol != null)
                {
                    query = _sortDirection == SortDirection.Ascending
                        ? query.OrderBy(item => targetCol.GetRawValue(item))
                        : query.OrderByDescending(item => targetCol.GetRawValue(item));
                }
            }

            // Client-side pagination
            return query.Skip((CurrentPage - 1) * PageSize).Take(PageSize);
        }
    }

    private void HandleSearchInput(ChangeEventArgs e)
    {
        _searchTerm = e.Value?.ToString() ?? string.Empty;

        _debounceTimer?.Stop();
        _debounceTimer?.Dispose();

        _debounceTimer = new System.Timers.Timer(SearchDebounceMs);
        _debounceTimer.AutoReset = false;
        _debounceTimer.Elapsed += async (_, _) =>
        {
            await InvokeAsync(async () =>
            {
                CurrentPage = 1;
                if (CurrentPageChanged.HasDelegate)
                {
                    await CurrentPageChanged.InvokeAsync(CurrentPage);
                }

                if (OnSearchChanged.HasDelegate)
                {
                    await OnSearchChanged.InvokeAsync(_searchTerm);
                }

                StateHasChanged();
            });
        };
        _debounceTimer.Start();
    }

    private async Task ClearSearch()
    {
        _searchTerm = string.Empty;
        CurrentPage = 1;

        if (CurrentPageChanged.HasDelegate)
        {
            await CurrentPageChanged.InvokeAsync(CurrentPage);
        }

        if (OnSearchChanged.HasDelegate)
        {
            await OnSearchChanged.InvokeAsync(string.Empty);
        }
    }

    private async Task HandleHeaderClick(GridColumn<TItem> column)
    {
        if (!column.Sortable) return;

        if (_sortColumn == column.ColumnKey)
        {
            _sortDirection = _sortDirection switch
            {
                SortDirection.None => SortDirection.Ascending,
                SortDirection.Ascending => SortDirection.Descending,
                SortDirection.Descending => SortDirection.None,
                _ => SortDirection.Ascending
            };

            if (_sortDirection == SortDirection.None)
            {
                _sortColumn = null;
            }
        }
        else
        {
            _sortColumn = column.ColumnKey;
            _sortDirection = SortDirection.Ascending;
        }

        CurrentPage = 1;
        if (CurrentPageChanged.HasDelegate)
        {
            await CurrentPageChanged.InvokeAsync(CurrentPage);
        }

        if (OnSortChanged.HasDelegate)
        {
            await OnSortChanged.InvokeAsync(new SortChangedArgs(_sortColumn ?? string.Empty, _sortDirection));
        }
    }

    private async Task HandlePageSizeChange(ChangeEventArgs e)
    {
        if (int.TryParse(e.Value?.ToString(), out var newSize) && newSize > 0)
        {
            PageSize = newSize;
            CurrentPage = 1;

            if (PageSizeChanged.HasDelegate)
            {
                await PageSizeChanged.InvokeAsync(PageSize);
            }

            if (CurrentPageChanged.HasDelegate)
            {
                await CurrentPageChanged.InvokeAsync(CurrentPage);
            }

            if (OnPageSizeChanged.HasDelegate)
            {
                await OnPageSizeChanged.InvokeAsync(PageSize);
            }

            if (OnPageChanged.HasDelegate)
            {
                await OnPageChanged.InvokeAsync(CurrentPage);
            }
        }
    }

    private async Task GoToPage(int page)
    {
        if (page < 1 || page > TotalPages || page == CurrentPage) return;

        CurrentPage = page;

        if (CurrentPageChanged.HasDelegate)
        {
            await CurrentPageChanged.InvokeAsync(CurrentPage);
        }

        if (OnPageChanged.HasDelegate)
        {
            await OnPageChanged.InvokeAsync(CurrentPage);
        }
    }

    private async Task HandleRowClick(TItem item)
    {
        if (OnRowClick.HasDelegate)
        {
            await OnRowClick.InvokeAsync(item);
        }
    }

    private IEnumerable<int> GetVisiblePageNumbers()
    {
        var total = TotalPages;
        var current = CurrentPage;

        if (total <= 7)
        {
            for (int i = 1; i <= total; i++) yield return i;
            yield break;
        }

        yield return 1;

        if (current > 3)
        {
            yield return -1; // Ellipsis
        }

        var start = Math.Max(2, current - 1);
        var end = Math.Min(total - 1, current + 1);

        for (int i = start; i <= end; i++)
        {
            yield return i;
        }

        if (current < total - 2)
        {
            yield return -1; // Ellipsis
        }

        yield return total;
    }

    public void Dispose()
    {
        _debounceTimer?.Stop();
        _debounceTimer?.Dispose();
    }
}
