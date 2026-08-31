# Phase 4 — Completion Handoff: AdminDataGrid & Security Hardening

> **Document Status:** Complete — ready for Phase 5
> **Phase:** 4 of N
> **Predecessor:** [`handoff-phase-3.md`](./handoff-phase-3.md)
> **Design Spec:** [`phase-4-admin-datagrid.md`](./phase-4-admin-datagrid.md)
> **Successor:** Phase 5 — Stock CRUD Management

---

## 1. Summary

Phase 4 delivered three things:

1. **`AdminDataGrid<TItem>`** — a fully generic, server-side Blazor data grid component.
2. **Security hardening** — closed the open self-registration endpoint and enforced admin-only auth paths.
3. **Bug fixes** — resolved a double-load race condition and a sort failure on navigation-property columns.

---

## 2. What Was Built

### 2.1 `AdminDataGrid<TItem>` Component

**Files:**

| File | Path |
|---|---|
| Markup | `OMM.Admin/Components/Shared/DataGrid/AdminDataGrid.razor` |
| Code-behind | `OMM.Admin/Components/Shared/DataGrid/AdminDataGrid.razor.cs` |
| CSS (scoped) | `OMM.Admin/Components/Shared/DataGrid/AdminDataGrid.razor.css` |
| Column child | `OMM.Admin/Components/Shared/DataGrid/GridColumn.razor` |
| Column C# | `OMM.Admin/Components/Shared/DataGrid/GridColumn.razor.cs` |
| Sort args | `OMM.Admin/Components/Shared/DataGrid/SortChangedArgs.cs` |

**Capabilities delivered:**

- Generic over `TItem` — works for any EF entity.
- `ServerSide="true"` mode — delegates paging/sort/search to the host page via events.
- Declarative `<GridColumn>` children with `Field`, `Title`, `Sortable`, `Template`, `Value`, and `SortKey` parameters.
- `<ActionColumn>` slot — typed `RenderFragment<TItem>` for per-row buttons.
- `<EmptyTemplate>` and `<LoadingTemplate>` slots.
- Debounced search (300 ms default) with a clear button.
- Client-side pagination with ellipsis bar (max 7 visible pages).
- Two-way binding via `@bind-PageSize` and `@bind-CurrentPage`.
- Sorting with tri-state toggle: Ascending -> Descending -> None.
- Skeleton loading rows animated with CSS pulse.

**Integrated into:**

- `/admin/klse-stocks` (`KlseStocks.razor`) — server-side mode, full sort/search/page.
- `/admin/reference-data` (`ReferenceData.razor`) — client-side mode, 5 tabbed grids (Markets, Sectors, Sub-Sectors, Exchanges, Countries).

---

### 2.2 KLSE Stocks Page Summary Cards

Added four stat badge cards above the data grid on `/admin/klse-stocks`:

| Card | Value |
|---|---|
| Total Securities | Live COUNT(*) from DB |
| Active Listings | COUNT WHERE IsActive = true |
| Shariah Compliant | COUNT WHERE ShariahCompliant = true |
| Markets | Static "MAIN / ACE / LEAP" label |

Summary metrics load separately via `LoadSummaryCounts()` so grid loading does not block them.

---

### 2.3 Security Hardening

**Problem:** Deploying to Render redirected to `/Account/Login?ReturnUrl=%2Fadmin` (ASP.NET Identity default), not `/login`. Worse, `/Account/Register` was publicly accessible.

**Fixes applied in `Program.cs`:**

```csharp
builder.Services.ConfigureApplicationCookie(o =>
{
    o.LoginPath = "/login";
    o.AccessDeniedPath = "/Account/AccessDenied";
});
```

`/Account/Register` and `/Account/ExternalLogin` now redirect to `/Account/AccessDenied`.

> NOTE: A future "Invite Admin" flow (Phase 7) will allow Super Admin / Admin to send invitation emails with a secure one-time link so new admins never self-register. See `docs/phase-7-admin-user-management.md`.

---

## 3. Bugs Fixed

### Bug 1 — Double LoadData() on Page-Size Change

**Symptom:** Changing the rows-per-page selector fired two DB queries every time.

**Root cause:** `HandlePageSizeChange()` was invoking both `OnPageSizeChanged` AND `OnPageChanged`. Both callbacks independently called `LoadData()` in the host page.

**Fix:** Removed the `OnPageChanged` invocation from `HandlePageSizeChange`. A page-size change already implies "reset to page 1". The comment in code documents this:

```csharp
// NOTE: OnPageChanged is intentionally NOT fired here.
// OnPageSizeChanged already implies "reset to page 1".
// Firing both would cause consumers to call LoadData() twice.
```

---

### Bug 2 — Sortable Navigation-Property Columns Not Sorting

**Symptom:** Clicking column headers for "Exchange", "Country", and "Parent Sector" in `ReferenceData.razor` had no effect.

**Root cause:** `GridColumn.GetRawValue()` requires either a `Field` expression or a `Value` delegate. These columns only had a `Template`, so `GetRawValue()` returned `null`.

**Fix:** Added a `Value` delegate to each affected column:

```razor
<!-- Before (broken) -->
<GridColumn TItem="Market" Title="Exchange" Sortable="true">
    <Template Context="m">@m.Exchange?.ExchangeName_EN</Template>
</GridColumn>

<!-- After (fixed) -->
<GridColumn TItem="Market" Title="Exchange" Sortable="true"
            Value="@(m => m.Exchange?.ExchangeName_EN)">
    <Template Context="m">@m.Exchange?.ExchangeName_EN</Template>
</GridColumn>
```

Columns fixed: Markets/"Exchange", Sectors/"Country", Sub-Sectors/"Parent Sector", Exchanges/"Country".

---

### Bug 3 — InvalidOperationException: PageSizeChanged Parameter Missing

**Symptom:** Runtime crash: `Object of type 'AdminDataGrid\`1[Stock]' does not have a property matching the name 'PageSizeChanged'.`

**Root cause:** `@bind-PageSize` requires Blazor to find a matching `[Parameter] EventCallback<int> PageSizeChanged` on the component. It was missing.

**Fix:** Added to `AdminDataGrid.razor.cs`:

```csharp
/// <summary>Required for Blazor @bind-PageSize two-way binding.</summary>
[Parameter]
public EventCallback<int> PageSizeChanged { get; set; }
```

---

## 4. Key Gotchas for the Next Engineer

| Gotcha | Detail |
|---|---|
| `GetRawValue()` needs `Field` OR `Value` | If only `Template` is provided, sorting silently returns null. Always add `Value` on navigation-property columns. |
| `@bind-X` requires `XChanged` callback | Standard Blazor two-way binding. Missing it causes a runtime crash, not a compile error. |
| Do NOT fire `OnPageChanged` inside `HandlePageSizeChange` | Causes double `LoadData()`. The rule: page-size change owns its own callback; page navigation owns its own. |
| Local DLL file locks | Running OMM.Admin locally locks binaries. Run `Stop-Process -Name dotnet -Force` before a rebuild. |
| Render deployment | Migrations run at startup via `db.Database.MigrateAsync()` — not gated behind `IsDevelopment()`. |

---

## 5. Acceptance Criteria — Status

| Criterion | Status |
|---|---|
| AdminDataGrid renders with 1+ columns and 100+ rows | PASS |
| Pagination navigates correctly with ellipsis | PASS |
| Sort toggle cycles Ascending -> Descending -> None | PASS |
| Search fires OnSearchChanged after debounce only | PASS |
| IsLoading = true shows skeleton, not data | PASS |
| Items = [] shows EmptyTemplate | PASS |
| ActionColumn renders per-row with typed context | PASS |
| Fully integrated in /admin/klse-stocks (ServerSide) | PASS |
| All icons use Tabler Icons only | PASS |
| /Account/Register redirects to AccessDenied | PASS |
| Auth challenge goes to /login, not /Account/Login | PASS |
| Page-size change triggers a single LoadData() call | PASS (Bug 1 fixed) |
| Navigation-property columns sort correctly | PASS (Bug 2 fixed) |

---

## 6. Known Open Items

| Item | Impact | Phase |
|---|---|---|
| No unit/integration tests for AdminDataGrid | Low | Phase 5+ |
| PageSizeOptions hard-coded to [10, 20, 50, 100] | Low | As-needed |
| Institutions.razor and Users.razor are empty stubs | Medium | Phase 6, 7 |
| Edit/Delete buttons on /admin/klse-stocks are disabled placeholders | High — intentional | Phase 5 |

---

## 7. What is Next — Phase 5: Stock CRUD

| Task | Description |
|---|---|
| `StockEditModal.razor` | Create/Edit modal with form validation, Market/Sector dropdowns |
| Wire "Add Stock" button | Open modal with blank Stock instance |
| Wire "Edit" button | Open modal pre-populated with selected row data |
| Soft-delete | Toggle IsActive = false with confirmation dialog |
| Refresh after save | Reload grid and summary cards on successful save/delete |
| Bulk CSV import | Optional stretch goal |

---

*Handoff prepared — Phase 4 is complete and stable. Proceed to Phase 5.*
