# OMMv2 Handoff: Malaysian Asset-Class Adaptive Forms

> **Status:** Core adaptive-form functionality is implemented. UX refinements, contextual validation, stock-picker integration, atomic yield persistence, and PostgreSQL integration coverage have been added. `OMM.Public` builds successfully. PostgreSQL integration-test runtime verification remains blocked by EF model/migration drift.

## Scope

`OMM.Public` provides a three-step Blazor Add/Edit Mine modal:

1. Asset category, type, and identity.
2. Asset-specific financial details.
3. Review and confirmation.

Metadata remains type-specific and is serialized to the existing PostgreSQL `jsonb` field; no additional per-asset columns are required.

## Completed implementation

- Seven asset categories and dynamic asset-type selection.
- Asset-specific forms for EPF/KWSP, ASB/ASM, fixed deposits, Bursa and US stocks, REITs, ETFs, general unit trusts, property, precious metals, cryptocurrency, savings, cash, and other assets.
- Reactive calculations for balances, capital, values, growth, income, dividend, interest, and yield estimates.
- EPF and ASB/ASM summaries use a consistent flow: editable inputs first, computed results last.
- ASB/ASM clearly separates invested capital (`units × RM 1.00`) from projected annual and monthly dividend income.
- Currency units are displayed in input adornments, not repeated in labels.
- Long fields use full-width rows where needed; responsive layouts avoid squeezing long values beside unrelated fields.
- Category cards keep icon and title together, with the description on a separate row.
- EPF account selector wraps on narrow screens.
- Bursa Stocks, REITs, and ETFs use the shared `StockSearchPicker`.
- Exchange fields are marked required where applicable.
- The shared Step 1 Institution / Provider field was removed because it was irrelevant to many asset types. Contextual provider fields can be added later inside relevant asset forms.
- Field-level validation highlights the relevant control, displays an inline message, and scrolls/focuses the invalid field after failed Next/Save validation.
- Growth progression badge added to MineDetail Yield History.
- Lightweight Log Rate Declaration modal added to MineDetail.
- Yield declaration and optional income-record creation are persisted atomically through `DatabaseMineService.RecordYieldDeclarationAsync`.

## Persistence and testing

### Existing migrations

- `20261001182346_AddMineMetadataJson`
- `20261001183936_AddMineYieldHistory`

### Integration coverage

`OMM.Integration.Tests/PublicYieldDeclarationTests.cs` creates a temporary PostgreSQL database, applies migrations, seeds minimal data, and covers successful atomic persistence and rollback when income-record persistence is forced to fail.

## Current verification status

Verified:

- `OMM.Public` builds successfully.
- `AddMineModal.razor` compiles without reported component errors.
- Integration test project compiles in isolated output.

Blocked or pending:

- A PostgreSQL integration-test run reached migration application but failed with EF Core `PendingModelChangesWarning`: the current model differs from the committed migration snapshot. Do not mark integration tests green until this is reconciled.
- Recreate the temporary local PostgreSQL test database after migration alignment and rerun the focused tests.
- Per repository instructions, do not run the test suite automatically without user approval.

## Remaining work

1. Compare the current `ApplicationDbContext` model with `OMM.Public/Data/Migrations/ApplicationDbContextModelSnapshot.cs`.
2. Add and review a migration if the model changes are intentional, or correct unintended model drift.
3. Rerun `PublicYieldDeclarationTests` against local PostgreSQL after migration alignment.
4. Add the FD Calculator to Mine bridge if that feature remains in scope.
5. Consider contextual provider fields for Savings/FD, Stocks/ETF/REIT, general Unit Trust, Property, and Crypto rather than restoring a universal provider field.
6. Perform a browser pass at mobile, tablet, and desktop widths for all asset types, especially long labels, stock-picker suggestions, EPF scheme controls, and validation focus behavior.

## UI standards to preserve

- Use Bootstrap and existing `omm-form-*` / `dc-currency-group` conventions.
- Keep long fields full-width on narrow screens; only place related short fields together when they fit without clipping.
- Keep currency codes in input adornments and labels currency-neutral.
- Show required-field errors next to the invalid field and move focus to the first invalid control.
- Keep optional fields contextual rather than displaying irrelevant shared fields.
- Use Tabler Icons rather than emoji icons.
- Preserve semantic navigation and existing application styling.

## Key files

- `OMM.Public/Components/Shared/Mines/AddMineModal.razor`
- `OMM.Public/Components/Pages/MineDetail.razor`
- `OMM.Public/Components/Pages/Mines.razor`
- `OMM.Public/Components/Shared/StockSearchPicker.razor`
- `OMM.Public/Components/Shared/Tools/CalculatorDividend.razor`
- `OMM.Public/Models/Mine.cs`
- `OMM.Public/Services/DatabaseMineService.cs`
- `OMM.Public/Services/FormatHelper.cs`
- `OMM.Public/Data/Migrations/`
- `OMM.Integration.Tests/PublicYieldDeclarationTests.cs`
- `OMM.Integration.Tests/OMM.Integration.Tests.csproj`
