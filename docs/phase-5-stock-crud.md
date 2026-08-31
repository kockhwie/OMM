# Phase 5 — Stock CRUD Management

> **Document Status:** Complete  
> **Phase:** 5 of N  
> **Predecessor:** Phase 4 — AdminDataGrid Component ([`phase-4-admin-datagrid.md`](./phase-4-admin-datagrid.md))  
> **Handoff:** [`handoff-phase-5.md`](./handoff-phase-5.md)  
> **Successor:** Phase 6 — Institution CRUD Management  

---

## 1. Objective

Deliver full **Create, Read, Update, Soft-Delete (CRUD)** capabilities for Malaysian equity securities on the `/admin/klse-stocks` portal page, transitioning the database from a seeded read-only store into a fully manageable master record of truth.

Key requirements:
1. **Interactive Modal Form (`StockEditModal.razor`):** Add and edit stock records with multi-tab layout, form validation, and cascading taxonomy dropdowns.
2. **Read-Only Fundamentals Protection:** Display financial fundamentals (`CurrentPrice`, `PE`, `PB`, `DividendYield`, `EPS`, `DPS`, `NTA`, `ROE`, `ROA`, `DebtToEquity`, `CurrentRatio`, timestamps) as strictly read-only metrics managed exclusively by automated scrapers and background calculation jobs.
3. **Safe Soft-Deletion (`DeleteStockModal.razor`):** Never hard-delete stock rows from PostgreSQL. Deletions flag `IsDeleted = true` and record `DeletedByUserId` / `DeletedAt`.
4. **Audit Trail Enactment:** Automatically populate `CreatedByUserId`/`CreatedAt` and `ModifiedByUserId`/`ModifiedAt` from the logged-in administrator session.
5. **Advanced Filter Toolbar:** Real-time filtering by Market Board, Sector, Shariah compliance, and Listing status.
6. **Quick Status Toggling:** 1-click active/inactive listing status toggle directly from the data grid.

---

## 2. Component Architecture & File Layout

```
OMM.Admin/
└── Components/
    └── Pages/
        └── Admin/
            ├── KlseStocks.razor            # Main listing page, filter bar, grid, modal orchestration
            ├── StockEditModal.razor        # Add / Edit modal dialog with DataAnnotations validation
            └── DeleteStockModal.razor      # Soft-delete confirmation dialog
```

---

## 3. UI/UX & Form Design Specification

### 3.1 Tabbed Organization in `StockEditModal.razor`

To maintain clean ergonomics across 25+ model properties, the modal is organized into 4 logical tabs:

| Tab | Fields / Display | Notes |
|---|---|---|
| **1. General & Security Info** | `StockCode`, `ShortName_EN`, `LegalName_EN`, `RicCode`, `YahooSymbol`, `IsinCode`, `Currency`, `IsActive`, `ShariahCompliant` | `StockCode` and `ShortName_EN` are mandatory. Duplicate `StockCode` check executed before save. |
| **2. Market & Classification** | `MarketId` (Board dropdown), `SectorId` (Sector dropdown), `SubSectorId` (Cascading Sub-Sector dropdown) | When `SectorId` changes, `SubSectorId` options dynamically re-filter. If the current sub-sector does not belong to the newly chosen sector, `SubSectorId` resets to null. |
| **3. Multilingual Names** | Traditional Chinese (`ZH-TW`) & Simplified Chinese (`ZH-CN`) Short and Legal names | Grouped in dual side-by-side cards with font accents. |
| **4. Fundamentals & Metrics** | 12 financial ratios and metrics + scrape/calculation timestamps | **Read-only**. Visible only on existing records (`Id > 0`). Rendered as metric cards, never as editable text inputs. |

---

### 3.2 Read-Only Fundamentals Rule

Per `docs/market-data-design.md` §4.7 and `docs/admin-backend-tasks.md`:
- Admin users must not arbitrarily mutate automated market-scraped values in standard CRUD flows.
- The fundamental properties:
  - `CurrentPrice`, `MarketCap`
  - `PE`, `PB`, `DividendYield`
  - `EPS`, `DPS`, `NTA`, `ROE`, `ROA`, `DebtToEquity`, `CurrentRatio`
  - `LastScrapedAt`, `LastCalculatedAt`
- These are displayed in Tab 4 as read-only cards and are **excluded** from `StockFormModel` update payloads so form submissions never overwrite scrapers.

---

## 4. Soft Delete & Auditing Specification

### 4.0 Master-data identity boundary

Admin identity records live in the `admin` PostgreSQL schema, while shared master
data lives in `public`. Master-data audit user IDs are therefore stored as nullable
text values and must not be foreign keys to either application's `AspNetUsers` table.
When correcting an existing database, use a migration with PostgreSQL
`DROP CONSTRAINT IF EXISTS` and `DROP INDEX IF EXISTS`, because environments may
already have some of the legacy audit constraints removed. See [`../AGENTS.md`](../AGENTS.md).

### 4.1 Soft Delete Contract

When an administrator clicks "Delete" on a stock:
1. `DeleteStockModal.razor` opens, displaying the stock code, name, market, and an explanatory warning.
2. Upon confirmation:
   ```csharp
   entity.IsDeleted = true;
   entity.DeletedAt = DateTimeOffset.UtcNow;
   entity.DeletedByUserId = currentAdminUserId;
   entity.IsActive = false;
   await DbContext.SaveChangesAsync();
   ```
3. Because `MasterDataDbContext` defines global query filters:
   ```csharp
   modelBuilder.Entity<Stock>(entity =>
   {
       entity.HasQueryFilter(e => !e.IsDeleted);
   });
   ```
   The record immediately disappears from all admin and public queries without violating foreign key constraints in existing miner portfolios or dividend histories.

### 4.2 Audit Trail Contract

- `AuthStateProvider.GetAuthenticationStateAsync()` resolves `ClaimTypes.NameIdentifier`.
- **Create:** Sets `CreatedAt = DateTimeOffset.UtcNow` and `CreatedByUserId = userId`.
- **Update:** Sets `ModifiedAt = DateTimeOffset.UtcNow` and `ModifiedByUserId = userId`.
- **Delete:** Sets `DeletedAt = DateTimeOffset.UtcNow` and `DeletedByUserId = userId`.

---

## 5. Filter Toolbar & Search Integration

`KlseStocks.razor` includes a dedicated multi-parameter filter toolbar above the grid:
- **Market Board:** Filters `Stocks` where `MarketId == selectedMarketId`.
- **Sector:** Filters `Stocks` where `SectorId == selectedSectorId`.
- **Shariah Status:** Filters `ShariahCompliant == true` or `false`.
- **Listing Status:** Filters `IsActive == true` or `false`.
- **Full Search:** Matches against `StockCode`, `ShortName_EN`, `RicCode`, `LegalName_EN`, and `YahooSymbol`.
- **Clear Filters:** Resets all filter dropdowns and search inputs with a single click.

---

## 6. Acceptance Criteria & Verification

- [x] **Add Stock:** Can create a new stock record with valid code, names, currency, market board, and sector.
- [x] **Edit Stock:** Can modify existing security attributes, names, and classifications.
- [x] **Duplicate Check:** Attempting to create or rename a stock to an existing `StockCode` throws a user-friendly validation error banner.
- [x] **Cascading Taxonomy:** Changing Sector updates available Sub-Sectors and resets invalid selections.
- [x] **Read-Only Metrics:** Fundamentals cannot be altered through the form.
- [x] **Soft Delete:** Deleting a stock marks `IsDeleted = true` and updates `DeletedAt`/`DeletedByUserId` while removing it from the grid.
- [x] **Quick Status Toggle:** Clicking Active/Inactive badge toggles `IsActive` in real-time.
- [x] **Compilation:** `dotnet build` succeeds with 0 warnings and 0 errors.

---

## 7. Next Steps

Proceed to **Phase 6: Institution CRUD Management** (`docs/phase-6-institution-crud.md`), applying the same modal, server-side grid, audit, and soft-delete patterns to the `/admin/institutions` portal. Institution CRUD does not require the stock page's cascading taxonomy behavior.
