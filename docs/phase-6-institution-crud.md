# Phase 6 — Institution CRUD Management

> **Document Status:** Complete — see [`handoff-phase-6.md`](./handoff-phase-6.md)
> **Phase:** 6 of N
> **Predecessor:** [`handoff-phase-5.md`](./handoff-phase-5.md)
> **Successor:** Phase 7 — Wire Existing Features to the New Tables
> **Target Audience:** Developer / Agent implementing Institution CRUD for the Admin console

---

## 1. Objective

Deliver full **Create, Read, Update, Soft-Delete (CRUD)** capabilities for Financial Institutions on the `/admin/institutions` portal page.

Financial institutions are a master-data lookup used by the portfolio tracker for banks, brokers, EPF/KWSP accounts, gold providers, and insurance accounts. Phase 6 gives administrators the ability to manage this catalog via the same UI patterns already established in Phase 5 (Stock CRUD).

Key requirements:
1. **Interactive Modal Form (`InstitutionEditModal.razor`):** Add and edit institution records with DataAnnotations validation and a Country dropdown.
2. **Category Classification:** Each institution belongs to an `InstitutionCategory` enum (`Bank`, `Broker`, `EpfKwsp`, `GoldProvider`, `Insurance`, `Other`). The modal must expose this as a `<select>` with human-readable labels.
3. **Multilingual Names:** Institutions carry three name variants (`_EN`, `_ZH_TW`, `_ZH_CN`). All three are stored and editable.
4. **Safe Soft-Deletion (`DeleteInstitutionModal.razor`):** Never hard-delete. Soft-delete sets `IsDeleted = true`, `DeletedAt`, `DeletedByUserId`.
5. **Audit Trail:** Populate `CreatedByUserId`/`CreatedAt` on insert; `ModifiedByUserId`/`ModifiedAt` on update.
6. **Full grid page (`Institutions.razor`):** Replace the current placeholder stub with a working `AdminDataGrid<Institution>` integrated page, including a filter bar and summary stat cards.

---

## 2. Data Model Reference

### 2.1 `Institution` entity (`OMM.Shared/Models/MasterData/Institution.cs`)

| Property | Type | Notes |
|---|---|---|
| `Id` | `int` | PK |
| `InstitutionCode` | `string` (required) | Short unique identifier, e.g. `MAYBANK`, `CIMBINVEST` |
| `InstitutionName_EN` | `string` (required) | English display name |
| `InstitutionName_ZH_TW` | `string` (required) | Traditional Chinese name |
| `InstitutionName_ZH_CN` | `string` (required) | Simplified Chinese name |
| `InstitutionCategory` | `InstitutionCategory` enum | Bank / Broker / EpfKwsp / GoldProvider / Insurance / Other |
| `CountryId` | `int?` | FK to Country.Id (nullable) |
| `IsActive` | `bool` | Whether institution is active/available for selection |
| *(inherited)* | `AuditableEntity` | `CreatedAt`, `CreatedByUserId`, `ModifiedAt`, `ModifiedByUserId`, `DeletedAt`, `DeletedByUserId`, `IsDeleted` |

### 2.2 `InstitutionCategory` enum (same file)

| Enum Value | Human-Readable Label |
|---|---|
| `Bank` | Bank |
| `Broker` | Stockbroker / Investment |
| `EpfKwsp` | EPF / KWSP |
| `GoldProvider` | Gold Provider |
| `Insurance` | Insurance |
| `Other` | Other |

### 2.3 `Country` dependency (`OMM.Shared/Models/MasterData/Country.cs`)

Used for the Country dropdown in the modal.

| Property | Used in UI |
|---|---|
| `Id` | FK value stored on `Institution.CountryId` |
| `CountryCode` | Dropdown option label prefix (e.g. `MY`) |
| `CountryName_EN` | Dropdown option label (e.g. `Malaysia`) |

`MasterDataDbContext.Countries` is already registered and queryable.

---

## 3. Component Architecture & File Layout

```
OMM.Admin/
└── Components/
    └── Pages/
        └── Admin/
            ├── Institutions.razor              [MODIFY] Replace placeholder with full CRUD page
            ├── InstitutionEditModal.razor       [NEW] Add / Edit modal
            └── DeleteInstitutionModal.razor     [NEW] Soft-delete confirmation dialog
```

The implementation also updates the database mappings because admin identity lives
in the `admin` schema while master data lives in `public`:
- `MasterDataDbContext.cs` maps Institution audit IDs as nullable text.
- `OMM.Public/Data/ApplicationDbContext.cs` no longer creates identity foreign keys
  for shared master-data audit fields.
- A drift-safe migration removes legacy audit foreign keys and indexes.

No changes were required to:
- `OMM.Shared` — Entity and enum were already defined.
- `AdminDataGrid<TItem>` — Fully reusable; no modifications needed.

---

## 4. UI/UX Specification

### 4.1 `Institutions.razor` — Main Listing Page

#### Header
- Page title: **"Financial Institutions"**, subtitle: *"Configure banking partners, brokers, EPF/KWSP, and gold providers."*
- Toolbar: **Refresh** button + **Add Institution** button (danger/red, `ti-plus` icon).
- Alert banner slot for success/warning/error feedback messages (same pattern as `KlseStocks.razor`).

#### Summary Stat Cards (3 cards, same card style as `KlseStocks.razor`)

| Card | Icon | Metric |
|---|---|---|
| Total Institutions | `ti-building-bank` | Count of all non-deleted institutions |
| Active | `ti-check` | Count where `IsActive = true` |
| Categories in Use | `ti-category` | Count of distinct institution categories |

#### Filter Bar

| Filter | Control | Behaviour |
|---|---|---|
| **Category** | `<select>` | All / Bank / Broker / EPF-KWSP / Gold Provider / Insurance / Other |
| **Country** | `<select>` | All Countries / populated from DB |
| **Listing Status** | `<select>` | All Statuses / Active Only / Inactive Only |
| **Clear Filters** | `btn-outline-secondary` | Shown when any filter is active |

#### `AdminDataGrid<Institution>` Columns

| Column | Field / Template | Sortable |
|---|---|---|
| Code | `InstitutionCode` — monospace badge | Yes |
| Name | `InstitutionName_EN` (bold) + `InstitutionName_ZH_TW` (muted small) | Yes |
| Category | Colour-coded badge per `InstitutionCategory` | Yes |
| Country | `Country.CountryName_EN` or `—` | Yes (SortKey="Country") |
| Status | Clickable Active/Inactive toggle badge | Yes |

#### ActionColumn
Edit (`ti-edit`) + Delete (`ti-trash`) btn-group, same as KlseStocks.

---

### 4.2 `InstitutionEditModal.razor` — Add / Edit Modal

Modal size: `modal-lg` (smaller than Stock's `modal-xl` — fewer fields).

The implemented modal uses one compact form rather than tabs because it has only six
editable values. It includes `InstitutionCode`, all three required names,
`InstitutionCategory`, optional `CountryId`, and `IsActive`. There are no read-only
fundamentals and no cascading taxonomy.

#### Form Validation (`InstitutionFormModel` — DataAnnotations)

| Field | Constraint |
|---|---|
| `InstitutionCode` | Required, max 50 chars, unique check before save |
| `InstitutionName_EN` | Required, max 200 chars |
| `InstitutionName_ZH_TW` | Required, max 200 chars |
| `InstitutionName_ZH_CN` | Required, max 200 chars |
| `InstitutionCategory` | Required (Range validation on int cast) |
| `CountryId` | Optional |
| `IsActive` | Default `true` |

#### Duplicate Code Check

```csharp
var duplicateExists = await db.Institutions
    .AnyAsync(i => i.InstitutionCode.ToLower() == cleanCode.ToLower() && i.Id != model.Id);
```

---

### 4.3 `DeleteInstitutionModal.razor` — Soft-Delete Confirmation

Same structure as `DeleteStockModal.razor`:
- Displays: institution code, English name, category badge.
- Warning: "This institution will be soft-deleted. Existing portfolio accounts linked to this institution will be retained."
- On confirm: `IsDeleted = true`, `DeletedAt = DateTimeOffset.UtcNow`, `DeletedByUserId = currentUserId`, `IsActive = false`.

---

## 5. Server-Side Data Pattern

Follows the same server-side pattern from `KlseStocks.razor`.

State fields:
```csharp
private List<Institution> _institutions = [];
private List<Country> _filterCountries = [];
private int _totalCount, _activeCount, _bankBrokerCount, _distinctCountryCount;
private int _pageSize = 20;
private int _currentPage = 1;
private bool _isLoading = true;
private string _searchTerm = string.Empty;
private string _sortColumn = "InstitutionCode";
private SortDirection _sortDirection = SortDirection.Ascending;
private string _filterCategory = string.Empty;
private int _filterCountryId = 0;
private string _filterActive = string.Empty;
```

Sort cases: `InstitutionCode`, `InstitutionName_EN`, `InstitutionCategory`, `Country` (via `Country.CountryCode`), `IsActive`.

Search targets: `InstitutionCode`, `InstitutionName_EN`, `InstitutionName_ZH_TW`, `InstitutionName_ZH_CN`.

Include: `.Include(i => i.Country)` on all queries.

---

## 6. InstitutionCategory Badge Colour Mapping

| Category | Bootstrap style |
|---|---|
| `Bank` | `bg-primary-subtle text-primary border-primary-subtle` |
| `Broker` | `bg-info-subtle text-info border-info-subtle` |
| `EpfKwsp` | `bg-warning-subtle text-warning border-warning-subtle` |
| `GoldProvider` | `bg-success-subtle text-success border-success-subtle` |
| `Insurance` | `bg-secondary-subtle text-secondary border-secondary-subtle` |
| `Other` | `bg-light text-muted border` |

---

## 7. Audit Trail

| Operation | Fields Set |
|---|---|
| Create | `CreatedAt = DateTimeOffset.UtcNow`, `CreatedByUserId = userId`, `IsDeleted = false` |
| Update | `ModifiedAt = DateTimeOffset.UtcNow`, `ModifiedByUserId = userId` |
| Soft-Delete | `IsDeleted = true`, `DeletedAt = DateTimeOffset.UtcNow`, `DeletedByUserId = userId`, `IsActive = false` |

`userId` resolved from `AuthStateProvider` via `ClaimTypes.NameIdentifier`.

Because the admin and public applications use separate Identity stores, audit IDs in
shared master-data tables must remain text values rather than `AspNetUsers` foreign
keys. See [`../AGENTS.md`](../AGENTS.md) for the repeatable migration fix, including
the requirement to use `IF EXISTS` when database environments have drifted.

---

## 8. Resolved Design Decisions

- All three name variants are required, matching the shared `Institution` model.
- `CountryId` remains optional and is not pre-selected for new institutions.
- The status badge supports a quick active/inactive toggle, matching Stocks.
- Institution codes are trimmed and checked case-insensitively for duplicates; no additional format restriction is imposed.

---

## 9. Acceptance Criteria

- [x] `Institutions.razor` renders with full `AdminDataGrid<Institution>`, filter bar, stat cards, and action column.
- [x] **Add Institution:** Can create a new institution with valid code, names (all 3 languages), category, and optional country.
- [x] **Edit Institution:** Can modify an existing institution's attributes and save successfully.
- [x] **Duplicate Check:** Creating/renaming to an existing `InstitutionCode` shows a user-friendly error banner inside the modal.
- [x] **Soft Delete:** Deleting an institution sets `IsDeleted = true`, `DeletedAt`, `DeletedByUserId` and removes the row from the grid.
- [x] **Audit Fields:** `CreatedByUserId`/`CreatedAt` set on create; `ModifiedByUserId`/`ModifiedAt` set on update.
- [x] **Filter Bar:** Category, Country, and Status filters narrow the grid correctly and stack with the search box.
- [x] **Sort:** All five sortable columns function correctly server-side.
- [x] **All icons use Tabler Icons (`ti ti-*`)** — no emoji or other icon library.
- [x] `dotnet build` succeeds with **0 warnings, 0 errors**.

---

## 10. Out of Scope for Phase 6

- Institution-to-portfolio account linking (handled in portfolio module).
- CSV import from an external institution registry.
- Institution logo / image upload.
- Role-based visibility of institutions.

---

## 11. What is Next After Phase 6

| Phase | Feature |
|---|---|
| **7** | Admin User Management — list admin accounts via `AdminDataGrid`, invite new admins, assign roles, force password reset (see [`phase-7-admin-user-management.md`](./phase-7-admin-user-management.md)) |
