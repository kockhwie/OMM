# OMMv2 Handoff Document: Malaysian Asset Class Adaptive Form & Yield Tracking System

> **Date:** 2026-10-02  
> **Repository:** `OMMv2`  
> **Status:** All core phases implemented (Phases 1–4). Build verified cleanly (0 warnings, 0 errors).

---

## 1. Executive Summary

This feature implements an **adaptive asset-tracking system** tailored for Malaysian personal finance asset classes in OhMyMine (`OMM.Public`), based on the 37-page Malaysian Asset Class research specification.

The feature replaces the legacy flat, single-form Add Mine dialog with a **3-step adaptive modal** supporting:
- 7 visual category cards & dynamic type pills.
- 10 type-specific sub-forms capturing granular metadata (e.g. EPF Akaun 1/2/3 tranches, ASB fixed RM1.00 unit pricing, FD tenure/rollover/maturity, Bursa/US stocks, Unit Trust NAVs, Property with mortgage linking, Gold/Silver grams & forms, Crypto wallets, High-yield savings/cash).
- Rate & Yield History tracking (`MineYieldHistoryEntity`) to trace year-over-year rate growth (e.g. EPF dividend 5.4% → 6.0%, FD renewals).
- Tools bridge allowing direct addition from `/tools/dividend-calculator` ("Save as Mine to Track") with typed metadata.
- Full Edit Mine support reusing the same modal.

---

## 2. Solution Architecture & Key Patterns

### A. Metadata JSON Architecture
- **Problem:** Different asset classes require different fields (EPF has 3 accounts; FDs have tenure/maturity; Stocks have shares & tickers; Gold has grams & purity). Adding 20+ columns to `Mines` table would cause massive null bloat.
- **Solution:** `MineMetadata` POCO serialized as a PostgreSQL `jsonb` column (`MineEntity.MetadataJson`).
- **Entity:** `OMM.Public.Data.Entities.MineEntity.MetadataJson` mapped as `jsonb` in `ApplicationDbContext.MemberData.cs`.
- **Domain Model:** `OMM.Public.Models.Mine.Metadata` (type: `MineMetadata`).

### B. Enum Integer Stability (PostgreSQL Safety)
PostgreSQL maps EF Core enums as integers. To prevent breaking existing database records, explicit integers were assigned to `MineCategory` and `MineType` in [`OMM.Public/Models/Mine.cs`](file:///c:/Users/User/source/repos/OMMv2/OMM.Public/Models/Mine.cs):
- `MineCategory`: `Retirement = 0`, `CashAndDeposits = 1`, `Investments = 2`, `Property = 3`, `PreciousMetals = 4`, `Digital = 5`, `Other = 6`.
- `MineType`: `EpfKwsp = 0`, `FixedDeposit = 1`, `Stocks = 2`, `Reit = 3`, `UnitTrustGeneral = 4`, `PropertyResidential = 5`, `Gold = 6`, `Silver = 7`, `Others = 8`, `SavingsAccount = 10`, `CashInHand = 11`, `UnitTrustAsb = 12`, `StocksUs = 13`, `Etf = 14`, `PropertyCommercial = 15`, `Cryptocurrency = 16`.
- Obsolete aliases (`Funds = 4`, `Property = 5`) were preserved for backward compatibility.

### C. Cash Income Events vs. Rate / Yield History
There are two distinct tracking systems:
1. **Cash Income Events (`IncomeRecordEntity`):** Real cash received or credited (e.g., RM 2,400 dividend payout). Feeds the **Freedom Ratio** and the `/income` matrix.
2. **Yield & Rate Snapshots (`MineYieldHistoryEntity`):** Historical timeline of declared percentage rates (e.g., EPF declared 5.50% in 2024, 6.00% in 2025). Used for year-over-year rate growth tracing and FD renewal tracking.

---

## 3. Implementation Status Checklist

| Phase / Task | Description | Status |
|---|---|---|
| **Task 1.1** | Expand `MineCategory`, `MineType`, and add `MineMetadata` POCO | ✅ Complete |
| **Task 1.2** | Add `MetadataJson` jsonb column to `MineEntity` + EF Migration | ✅ Complete (Migrated) |
| **Task 1.3** | Update `FormatHelper.cs` (labels, icons, mappings, `CurrentValueLabel`) | ✅ Complete |
| **Task 1.4** | Create `MineYieldHistoryEntity` + EF Migration | ✅ Complete (Migrated) |
| **Task 2.1** | Standalone `AddMineModal.razor` supporting both Add & Edit modes | ✅ Complete |
| **Task 2.2** | Step 1: 7-category visual card grid + type pill selector | ✅ Complete |
| **Task 2.3** | Step 2: 10 type-specific adaptive sub-forms with auto-calculations | ✅ Complete |
| **Task 2.4** | Step 3: Review & confirm summary card + optional rate history archiving | ✅ Complete |
| **Task 2.5** | Dynamic reactive calculations & step validation | ✅ Complete |
| **Task 3.1** | Wire metadata JSON serialization into `DatabaseMineService` | ✅ Complete |
| **Task 3.2** | Add `RecordYieldHistoryAsync` and `GetYieldHistoriesAsync` in service | ✅ Complete |
| **Task 3.3** | Wire "Save as Mine to Track" bridge in `/tools/dividend-calculator` | ✅ Complete |
| **Task 4.1** | Display asset-specific metadata attributes on `MineDetail.razor` | ✅ Complete |
| **Task 4.2** | Render Yield History timeline on `MineDetail.razor` | ✅ Complete |
| **Task 4.3** | Add "Edit Mine" action to `MineDetail.razor` header | ✅ Complete |

---

## 4. Key Files Created and Modified

### Created Files:
1. [`OMM.Public/Components/Shared/Mines/AddMineModal.razor`](file:///c:/Users/User/source/repos/OMMv2/OMM.Public/Components/Shared/Mines/AddMineModal.razor)
   - 3-step modal (Step 1: Category/Type, Step 2: Adaptive Financial Details, Step 3: Review & Confirm).
   - Handles both Add mode (`ExistingMine == null`) and Edit mode (`ExistingMine != null`).
   - Includes real-time auto-calculation (EPF sum of 3 accounts, ASB RM 1.00 unit pricing, FD monthly return, Bursa/US stock P&L, property net rental, gold gram calculations).
   - In Edit mode: provides a switch to archive rate updates into `MineYieldHistoryEntity`.
2. [`OMM.Public/Data/Entities/MineYieldHistoryEntity.cs`](file:///c:/Users/User/source/repos/OMMv2/OMM.Public/Data/Entities/MineYieldHistoryEntity.cs)
   - Entity for storing historical rate snapshots (`EffectiveYear`, `RecordDate`, `RatePct`, `DeclaredAmount`, `BalanceAtTime`, `Notes`).
3. [`docs/add-mine-adaptive-form-plan.md`](file:///c:/Users/User/source/repos/OMMv2/docs/add-mine-adaptive-form-plan.md)
   - Comprehensive plan document reflecting all decisions, specifications, and completed tasks.

### Modified Files:
1. [`OMM.Public/Models/Mine.cs`](file:///c:/Users/User/source/repos/OMMv2/OMM.Public/Models/Mine.cs)
   - Added `MineCategory` and `MineType` values, `MineMetadata` POCO, and `Mine.Metadata` property.
2. [`OMM.Public/Data/Entities/MineEntity.cs`](file:///c:/Users/User/source/repos/OMMv2/OMM.Public/Data/Entities/MineEntity.cs)
   - Added `MetadataJson` string property and `YieldHistories` navigation collection.
3. [`OMM.Public/Data/ApplicationDbContext.MemberData.cs`](file:///c:/Users/User/source/repos/OMMv2/OMM.Public/Data/ApplicationDbContext.MemberData.cs)
   - Mapped `MetadataJson` as `jsonb` column.
   - Configured `MineYieldHistoryEntity` with index on `(UserId, MineId, EffectiveYear)`.
4. [`OMM.Public/Services/DatabaseMineService.cs`](file:///c:/Users/User/source/repos/OMMv2/OMM.Public/Services/DatabaseMineService.cs)
   - Updated `AddMineAsync` & `UpdateMineAsync` to serialize `Mine.Metadata` to `MetadataJson` and save `LinkedBurdenId`.
   - Updated `MapMine` to deserialize `MetadataJson` to `Mine.Metadata`.
   - Added `RecordYieldHistoryAsync` and `GetYieldHistoriesAsync`.
5. [`OMM.Public/Services/FormatHelper.cs`](file:///c:/Users/User/source/repos/OMMv2/OMM.Public/Services/FormatHelper.cs)
   - Updated `CategoryLabel`, `MineTypeLabel`, `CategoryToIcon`, `MineTypeToIcon`, `CurrentValueLabel`, and `GetTypesForCategory`.
6. [`OMM.Public/Components/Pages/Mines.razor`](file:///c:/Users/User/source/repos/OMMv2/OMM.Public/Components/Pages/Mines.razor)
   - Replaced old 120-line inline modal with `<AddMineModal>`.
   - Added Edit action to Mine cards.
7. [`OMM.Public/Components/Pages/MineDetail.razor`](file:///c:/Users/User/source/repos/OMMv2/OMM.Public/Components/Pages/MineDetail.razor)
   - Added "Edit Mine" action launching `AddMineModal`.
   - Added "Asset Class Attributes" card displaying all typed metadata.
   - Added "Yield History" tab with YoY rate timeline.
8. [`OMM.Public/Components/Shared/Tools/CalculatorDividend.razor`](file:///c:/Users/User/source/repos/OMMv2/OMM.Public/Components/Shared/Tools/CalculatorDividend.razor)
   - Updated `SaveToMinesAsync` in Quick and Simulator modes to attach typed `MineMetadata`.
   - Added "Save as Mine to Track" action in Simulator mode.
9. [`OMM.Public/Components/_Imports.razor`](file:///c:/Users/User/source/repos/OMMv2/OMM.Public/Components/_Imports.razor)
   - Added `@using OMM.Public.Components.Shared.Mines`.

---

## 5. Database Migrations Applied

Two migrations were created and applied to the local PostgreSQL database:
1. `20261001182346_AddMineMetadataJson`: Adds `MetadataJson` (jsonb) column to `Mine` table.
2. `20261001183936_AddMineYieldHistory`: Creates `MineYieldHistories` table with indexes and foreign keys.

*Note: Database is fully updated. Running `dotnet ef database update --no-build` confirms there are no pending migrations.*

---

## 6. Critical Developer & Environment Gotchas

1. **File Lock on Running Process:**
   - If the application is running locally (e.g. `OMM.Public.exe`), running a standard `dotnet build` will fail with MSB3021/MSB3027 file lock retries because it attempts to overwrite the locked `.exe`.
   - **Fix:** When checking compilation, always use:
     ```powershell
     dotnet build OMM.Public/OMM.Public.csproj -t:CoreCompile
     ```
   - When running EF Core commands, use `--no-build`.
2. **Project Rules (from `AGENTS.md`):**
   - **Do NOT run `dotnet test` automatically.** Prompt user for permission or ask them to run it manually.
   - **Do NOT run `git commit` or `git push` automatically.** Always present completed work for review.
   - **No emoji icons:** Always use Tabler Icons (`ti ti-*`).
   - **Form standards:** Follow `docs/ui-form-standards.md` (`omm-form-section`, `omm-form-field`, `omm-form-label`, `omm-form-help`, `omm-form-grid`, `dc-currency-group`).

---

## 7. Recommended Next Steps for Incoming Agent

If continuing to enhance this system, the recommended next tasks are:

1. **Growth Progression Badge in `MineDetail.razor`:**
   - In the Yield History tab of `MineDetail.razor`, add a visual badge comparing the 2 latest records:
     $$\Delta = \text{Rate}_{\text{current}} - \text{Rate}_{\text{previous}}$$
     e.g., `+0.60% YoY (+11.1% growth vs 2024)` with a green trending badge.
2. **Quick Action: "+ Log Rate Declaration" Modal directly on `MineDetail.razor`:**
   - Add a lightweight modal to log a rate update without opening the full edit modal.
   - Include a checkbox: *"Also create matching cash income record in Freedom Income log"* which calls `MemberRecordService.AddIncomeRecordAsync(...)`.
3. **FD Calculator to Mine Bridge:**
   - If `/tools/fd-calculator` is updated, attach the same "Save as Mine to Track" bridge to save FD Principal, Tenure, and Interest Rate into `MineMetadata`.
