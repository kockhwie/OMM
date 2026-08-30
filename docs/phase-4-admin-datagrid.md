# Phase 4 — AdminDataGrid Component

> **Document Status:** Draft  
> **Phase:** 4 of N  
> **Predecessor:** Phase 3 — Admin Layout & Navigation (`handoff-phase-3.md`)  
> **Target Audience:** Developer / Agent implementing the generic data-grid for the Admin console

---

## 1. Objective

Build a **reusable, generic `AdminDataGrid<TItem>` Blazor component** that will power all tabular admin list pages (`/admin/klse-stocks`, `/admin/institutions`, `/admin/reference-data`, `/admin/users`).

The component must handle:
- Column definition via declarative child components
- Client-side and server-side pagination
- Multi-column sorting (ascending / descending) with visual indicators
- Debounced free-text search / filter
- Loading skeleton and empty-state templates
- An action-column slot for Phase 5/6 edit + delete buttons

---

## 2. Location & Project Ownership

| Item | Decision |
|---|---|
| **Component lives in** | `OMM.Admin/Components/Shared/DataGrid/` |
| **Namespace** | `OMM.Admin.Components.Shared` |
| **Rationale** | The grid uses admin-specific CSS variables and Tabler Icons. The public app has no tabular admin interface, so there is no current need to place this in `OMM.Shared`. |
| **CSS** | New scoped stylesheet `AdminDataGrid.razor.css` (CSS isolation) co-located with the component. |

---

## 3. Component API Surface

### 3.1 Primary Component — `AdminDataGrid<TItem>`

```razor
<AdminDataGrid TItem="Stock"
               Items="@stocks"
               TotalCount="@totalCount"
               PageSize="20"
               IsLoading="@isLoading"
               OnPageChanged="HandlePageChanged"
               OnSortChanged="HandleSortChanged"
               OnSearchChanged="HandleSearchChanged">

    <Columns>
        <GridColumn TItem="Stock" Field="@(s => s.Ticker)"   Title="Ticker"      Sortable="true" />
        <GridColumn TItem="Stock" Field="@(s => s.Name)"     Title="Name"        Sortable="true" />
        <GridColumn TItem="Stock" Field="@(s => s.Market)"   Title="Market"      Sortable="true" />
        <GridColumn TItem="Stock" Field="@(s => s.IsActive)" Title="Active"      Sortable="false" />
    </Columns>

    <ActionColumn Context="stock">
        <button class="btn btn-sm btn-outline-primary" @onclick="() => EditStock(stock)">
            <i class="ti ti-edit"></i>
        </button>
        <button class="btn btn-sm btn-outline-danger" @onclick="() => DeleteStock(stock)">
            <i class="ti ti-trash"></i>
        </button>
    </ActionColumn>

    <EmptyTemplate>
        <p class="text-muted">No stocks found matching your search.</p>
    </EmptyTemplate>

</AdminDataGrid>
```

### 3.2 Parameters Reference

| Parameter | Type | Required | Default | Description |
|---|---|---|---|---|
| `TItem` | generic type | Yes | — | The entity type for each row |
| `Items` | `IEnumerable<TItem>` | Yes | — | The current page's data (server or client sliced) |
| `TotalCount` | `int` | No | `Items.Count()` | Total record count (used for server-side page math) |
| `PageSize` | `int` | No | `20` | Rows per page |
| `PageSizeOptions` | `int[]` | No | `[10, 20, 50, 100]` | Dropdown choices for rows-per-page |
| `CurrentPage` | `int` | No | `1` | Externally controlled page index |
| `IsLoading` | `bool` | No | `false` | Activates the skeleton loading state |
| `ServerSide` | `bool` | No | `false` | If `true`, delegates paging/sort to parent via events |
| `SearchPlaceholder` | `string` | No | `"Search…"` | Placeholder text for the search input |
| `SearchDebounceMs` | `int` | No | `300` | Debounce delay in milliseconds |
| `Columns` | `RenderFragment` | Yes | — | Column definitions (`<GridColumn>` children) |
| `ActionColumn` | `RenderFragment<TItem>` | No | — | Per-row action buttons slot |
| `EmptyTemplate` | `RenderFragment` | No | built-in | Custom empty state |
| `LoadingTemplate` | `RenderFragment` | No | built-in | Custom skeleton/loading overlay |

### 3.3 Events (Callbacks)

| Event | Signature | Fires When |
|---|---|---|
| `OnPageChanged` | `EventCallback<int>` | User clicks a page number or prev/next |
| `OnSortChanged` | `EventCallback<SortChangedArgs>` | User clicks a sortable column header |
| `OnSearchChanged` | `EventCallback<string>` | Debounced search input changes |

```csharp
public record SortChangedArgs(string ColumnKey, SortDirection Direction);

public enum SortDirection { None, Ascending, Descending }
```

### 3.4 Child Component — `GridColumn<TItem>`

| Parameter | Type | Description |
|---|---|---|
| `Field` | `Expression<Func<TItem, object>>` | Used to extract the value and derive the column key |
| `Title` | `string` | Column header label |
| `Sortable` | `bool` | Whether the column header is a sort trigger |
| `CssClass` | `string` | Optional extra CSS class on `<td>` cells |
| `Format` | `string` | Optional .NET format string (e.g. `"dd MMM yyyy"`) |
| `Template` | `RenderFragment<TItem>` | Optional full custom cell renderer (overrides `Field`) |

---

## 4. Internal Behaviour

### 4.1 Pagination Logic

- When `ServerSide = false` (default): the component slices `Items` internally using `Skip((CurrentPage-1) * PageSize).Take(PageSize)`.
- When `ServerSide = true`: the component renders the `Items` collection as-is (already sliced by the parent) and fires `OnPageChanged` to request a new slice.
- Total page count = `Math.Ceiling(TotalCount / (double)PageSize)`.
- The pagination bar shows: `[First] [Prev] [1] [2] ... [N] [Next] [Last]` with a max of 7 visible page buttons. Ellipses are inserted when the range exceeds the window.

### 4.2 Sorting

- Sort state is held inside the component: `string _sortColumn` + `SortDirection _sortDirection`.
- Clicking a sorted column toggles `Ascending -> Descending -> None`.
- When `ServerSide = false`: sort is applied in-memory via LINQ reflection on the `Field` expression key.
- When `ServerSide = true`: fires `OnSortChanged` with the `SortChangedArgs` and lets the parent rebuild the `Items` collection.
- Column headers display Tabler Icons: `ti-arrows-sort` (unsorted), `ti-sort-ascending` / `ti-sort-descending`.

### 4.3 Search / Filter

- A debounced `<input>` at the top of the grid is always visible.
- After `SearchDebounceMs` ms of inactivity the component fires `OnSearchChanged` with the current term.
- When `ServerSide = false`: the component applies a case-insensitive `string.Contains` across all `Field` values on columns without a custom `Template`.
- Changing the search term resets `CurrentPage` to `1`.

### 4.4 Loading State

- When `IsLoading = true`:
  - A custom `LoadingTemplate` is shown if provided.
  - Otherwise the component renders 5 skeleton rows with an animated CSS pulse.
  - The search input and column headers remain visible; only the table body is replaced.

### 4.5 Empty State

- If `Items` is empty and `IsLoading = false`, the `EmptyTemplate` (or the built-in "No records found." message) is rendered inside a full-width `<tr>`.

---

## 5. File Structure

```
OMM.Admin/
└── Components/
    └── Shared/
        └── DataGrid/
            ├── AdminDataGrid.razor          # Primary component markup
            ├── AdminDataGrid.razor.cs       # Code-behind (partial class)
            ├── AdminDataGrid.razor.css      # Scoped CSS (dark admin palette)
            ├── GridColumn.razor             # Column definition child component
            ├── GridColumn.razor.cs          # Column code-behind
            └── SortChangedArgs.cs           # Record + enum shared by caller pages
```

---

## 6. Styling Guide

The component uses the admin CSS variable palette established in Phase 3.

| Token | Usage |
|---|---|
| `--omm-ink-900` | Table background |
| `--omm-ink-800` | Header row background |
| `--omm-burden-red` | Sorted-column header accent |
| `--omm-text-primary` | Cell text |
| `--omm-text-muted` | Pagination label, empty-state text |
| `--omm-border` | Row dividers, table border |

Key visual rules:
- **Striped rows:** even rows get `background: rgba(255,255,255,0.03)`.
- **Hover:** `rgba(255,255,255,0.06)`.
- **Sort indicator:** active column header coloured `--omm-burden-red` with a 2 px bottom border.
- **Skeleton pulse:** `background: linear-gradient(90deg, var(--omm-ink-800) 25%, var(--omm-ink-700) 50%, var(--omm-ink-800) 75%)` animated via `background-size`.
- **Pagination:** outlined pill-style buttons; active page uses `--omm-burden-red` fill.

---

## 7. Integration Plan

### 7.1 Pages to Wire Up in Phase 4

| Page | Route | Data Source | Notes |
|---|---|---|---|
| `KlseStocks.razor` | `/admin/klse-stocks` | `MasterDataDbContext.Stocks` | First full integration; includes Market & Sector name |
| `ReferenceData.razor` | `/admin/reference-data` | `Markets`, `Sectors`, `SubSectors` | Tab-based view, one grid per tab |

### 7.2 Pages Deferred to Phase 5/6

| Page | Route | Reason Deferred |
|---|---|---|
| `Institutions.razor` | `/admin/institutions` | Needs institution CRUD modal (Phase 6) |
| `Users.razor` | `/admin/users` | Needs admin user management flows (Phase 7) |

### 7.3 Server-Side Data Pattern

Each page will follow this pattern:

```csharp
// In KlseStocks.razor.cs
private List<Stock> _stocks = [];
private int _totalCount;
private bool _isLoading;
private int _currentPage = 1;
private string _sortColumn = "Ticker";
private SortDirection _sortDir = SortDirection.Ascending;
private string _searchTerm = "";

protected override async Task OnInitializedAsync() => await LoadData();

private async Task LoadData()
{
    _isLoading = true;
    var query = DbContext.Stocks.Include(s => s.Market).AsQueryable();

    if (!string.IsNullOrWhiteSpace(_searchTerm))
        query = query.Where(s => s.Ticker.Contains(_searchTerm) || s.Name.Contains(_searchTerm));

    _totalCount = await query.CountAsync();

    query = (_sortColumn, _sortDir) switch
    {
        ("Ticker", SortDirection.Ascending)  => query.OrderBy(s => s.Ticker),
        ("Ticker", SortDirection.Descending) => query.OrderByDescending(s => s.Ticker),
        ("Name",   SortDirection.Ascending)  => query.OrderBy(s => s.Name),
        _                                    => query.OrderBy(s => s.Ticker)
    };

    _stocks = await query.Skip((_currentPage - 1) * 20).Take(20).ToListAsync();
    _isLoading = false;
}

private async Task HandlePageChanged(int page)          { _currentPage = page; await LoadData(); }
private async Task HandleSortChanged(SortChangedArgs a) { _sortColumn = a.ColumnKey; _sortDir = a.Direction; _currentPage = 1; await LoadData(); }
private async Task HandleSearchChanged(string term)     { _searchTerm = term; _currentPage = 1; await LoadData(); }
```

---

## 8. Open Questions / Design Decisions

> [!IMPORTANT]
> Confirm the following before starting implementation.

1. **Checkbox multi-select** — Do we need bulk-select for batch delete in Phase 5? If yes, add `bool MultiSelect` parameter and `EventCallback<IEnumerable<TItem>> OnSelectionChanged` now.
2. **Column resize / reorder** — Nice-to-have or out of scope for Phase 4?
3. **Export to CSV** — Should the grid include an "Export CSV" toolbar button? If yes, a `ToolbarTemplate RenderFragment` parameter is needed.
4. **Row click navigation** — Should clicking a row navigate to a detail page, or is all interaction via the ActionColumn only?

---

## 9. Acceptance Criteria

- [ ] `AdminDataGrid<TItem>` renders with at minimum 1 column and 100+ rows without errors.
- [ ] Pagination correctly shows page N of M and navigates between pages.
- [ ] Clicking a sortable column header toggles Ascending -> Descending -> Unsorted and re-sorts rows.
- [ ] Typing in the search box fires `OnSearchChanged` only after the debounce delay.
- [ ] `IsLoading = true` shows skeleton rows, not raw data.
- [ ] `Items = []` shows the empty-state template.
- [ ] `ActionColumn` slot renders per-row with the correct `TItem` context.
- [ ] Fully integrated in `/admin/klse-stocks` with `ServerSide = true`.
- [ ] All icons use Tabler Icons (`ti ti-*`) — no emoji or other icon library.
- [ ] Styling is consistent with `AdminLayout` (dark/red palette).

---

## 10. Out of Scope for Phase 4

- Column drag-to-reorder
- Column visibility toggle
- Inline row editing
- CRUD operations (create / update / delete) — Phase 5/6
- Export to CSV / Excel
- Virtual scrolling / infinite scroll

---

## 11. What is Next After Phase 4

| Phase | Feature |
|---|---|
| **5** | Stock CRUD — add/edit modal (`StockEditModal.razor`), delete confirmation, integrated into `/admin/klse-stocks` |
| **6** | Institution CRUD — same modal pattern for `/admin/institutions` |
| **7** | Admin User Management — list admin accounts, assign roles, force password reset |
