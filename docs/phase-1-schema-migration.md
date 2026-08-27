# Phase 1 — Schema & Migration (Standalone Task Doc)

> **This doc is self-contained.** You should be able to execute this phase from this
> file alone, without needing `market-data-design.md` or `admin-backend-tasks.md`
> open. Those two docs remain the source of truth for later phases and for context —
> if anything here seems to contradict them, stop and flag it rather than guessing.

## Project context

- ASP.NET Core Blazor Server app (`omm.csproj`, `net10.0`), EF Core + SQL Server,
  ASP.NET Identity already scaffolded (`Data/ApplicationUser.cs`,
  `Data/ApplicationDbContext.cs`).
- Existing stock data lives as static JSON at `wwwroot/data/klse-stocks.json`
  (~900 rows, shape: `{ "name": "...", "code": "...", "price": "" }`), read at runtime
  by `Services/IKlseStockLookupService.cs`. **This phase does not touch that service**
  — it only gets the data into the database. Wiring the service to read from the DB
  instead of the JSON file is Phase 7, later.
- No existing `Country`/`Exchange`/`Market`/`Sector`/`SubSector`/`Institution`/`Stock`
  tables exist yet. This phase creates all seven from scratch.

## Goal

Get all seven tables into the database via EF Core migration, with Phase-1 reference
data seeded (Malaysia only) and the existing ~900 KLSE stocks migrated in from the
JSON file.

## Naming & conventions (non-negotiable)

- **No "Master" suffix** on any table or entity class. `Stock`, not `StockMaster`.
- Every table gets the same audit block (see below) — no exceptions.
- Localization: name fields use flat suffix columns `_EN` / `_ZH_TW` / `_ZH_CN` (three
  physical columns, not a separate localization table).
- Soft delete only. No hard deletes anywhere. EF Core global query filter applies
  `IsDeleted == false` automatically.

## Shared audit columns (apply to all 7 entities below)

| Column | Type | Notes |
|---|---|---|
| `CreatedByUserId` | `string?` FK → `AspNetUsers.Id` | **Nullable.** Use `null` for all seed data in this phase — see below |
| `CreatedAt` | `DateTimeOffset` | UTC |
| `ModifiedByUserId` | `string?` FK → `AspNetUsers.Id` | Null until first edit |
| `ModifiedAt` | `DateTimeOffset?` | Null until first edit |
| `DeletedByUserId` | `string?` FK → `AspNetUsers.Id` | Set on soft delete |
| `DeletedAt` | `DateTimeOffset?` | Set on soft delete |
| `IsDeleted` | `bool` | Default `false` |

**`CreatedByUserId` is nullable, and every row seeded in this phase uses `null`.** No
admin user exists yet at this point in the build order (that's Phase 2), so there is
nothing to point this column at. `null` correctly means "inserted by an automated
seed/migration, not a human" — it is not a placeholder or a workaround, it's the
correct value for this case. Do **not** invent a placeholder GUID or attempt to
pre-create an `AspNetUsers` row here — that belongs entirely to Phase 2, and mixing it
into this phase blurs a boundary that's deliberately kept clean. No coordination with
Phase 2 is needed as a result.

## Entity definitions

### `Country`
- `Id` (int, PK)
- `CountryCode` (string(2)) — ISO 3166-1 alpha-2
- `CountryName_EN` / `_ZH_TW` / `_ZH_CN` (string)
- `DefaultCurrencyCode` (string(3))
- `IsActive` (bool)
- *(+ shared audit columns)*

**Seed:** one row — `MY`, "Malaysia", currency `MYR`.

### `Exchange`
- `Id` (int, PK)
- `CountryId` (int, FK → `Country`)
- `ExchangeCode` (string) — e.g. `BURSA`
- `ExchangeName_EN` / `_ZH_TW` / `_ZH_CN` (string)
- `IsActive` (bool)
- *(+ shared audit columns)*

**Seed:** one row — `BURSA`, "Bursa Malaysia", under `MY`.

### `Market` (tier within an exchange)
- `Id` (int, PK)
- `ExchangeId` (int, FK → `Exchange`)
- `MarketCode` (string) — short code for compact UI, e.g. `MAIN`, `ACE`, `LEAP`
- `MarketName_EN` / `_ZH_TW` / `_ZH_CN` (string) — full name, e.g. "Main Market"
- `IsActive` (bool)
- *(+ shared audit columns)*

**Seed:** three rows under `BURSA` — `MAIN`/"Main Market", `ACE`/"ACE Market",
`LEAP`/"LEAP Market".

### `Sector`
- `Id` (int, PK)
- `CountryId` (int, FK → `Country`) — **sectors are scoped per country**; Malaysia's
  13-sector taxonomy is Bursa-specific and must not be reused for other countries
  later
- `SectorCode` (string)
- `SectorName_EN` / `_ZH_TW` / `_ZH_CN` (string)
- `IsActive` (bool)
- *(+ shared audit columns)*

### `SubSector`
- `Id` (int, PK)
- `SectorId` (int, FK → `Sector`)
- `SubSectorCode` (string)
- `SubSectorName_EN` / `_ZH_TW` / `_ZH_CN` (string)
- `IsActive` (bool)
- *(+ shared audit columns)*

**Seed for both, under `CountryId = MY`,** the 13 official Bursa Malaysia sectors and
their sub-sectors:

```
1. Financial Services
   → Banking, Insurance, Other Financial Services
2. Consumer Products & Services
   → Food & Beverages, Retailers, Automotive, Consumer Services, Household Goods,
     Agricultural Products, Travel Leisure & Hospitality
3. Industrial Products & Services
   → Building Materials, Chemicals, Metals, Packaging Materials,
     Diversified Industrials, Industrial Engineering
4. Technology
   → Semiconductors, Software, Digital Services, Hardware
5. Telecommunications & Media
   → Telecommunications Service Providers, Media & Advertising,
     Telecommunications Equipment
6. Health Care
   → Healthcare Providers, Pharmaceuticals, Healthcare Equipment & Supplies
7. Property
   → Property Development, Property Investment & Management
8. Real Estate Investment Trusts (REITs)
   → Commercial, Retail, Industrial, Hospitality, Healthcare
9. Plantation
   → Upstream Plantation, Integrated Cultivation
10. Energy
    → Oil & Gas Producers, Oil & Gas Equipment & Services, Renewable Energy
11. Construction
    → Civil Engineering, Heavy Construction, Specialised Construction
12. Transportation & Logistics
    → Logistics Services, Ports & Shipping, Airlines & Aviation, Road & Rail
13. Utilities
    → Electricity, Gas & Water Distribution
```

Generate short `SectorCode`/`SubSectorCode` values yourself (e.g. `FIN-SVC`,
`BANKING`) — no specific codes are mandated, just be consistent and readable.

### `Institution`
- `Id` (int, PK)
- `CountryId` (int?, FK → `Country`)
- `InstitutionCode` (string)
- `InstitutionName_EN` / `_ZH_TW` / `_ZH_CN` (string)
- `InstitutionCategory` (enum: `Bank`, `Broker`, `EpfKwsp`, `GoldProvider`,
  `Insurance`, `Other`)
- `IsActive` (bool)
- *(+ shared audit columns)*

**Seed:** Maybank (`Bank`), CIMB (`Bank`), Public Bank (`Bank`), KWSP (`EpfKwsp`),
Bursa Malaysia (`Other`) — all under `CountryId = MY`. These match names already
referenced in `Services/MockMineService.cs` so existing mock data has a real row to
eventually point to.

### `Stock`
- `Id` (int, PK)
- `StockCode` (string) — **the Bursa numeric code**, e.g. `"1155"` (this is the `code`
  field from `klse-stocks.json`)
- `ShortName_EN` / `_ZH_TW` / `_ZH_CN` (string) — from the JSON's `name` field for
  `_EN`; leave `_ZH_TW`/`_ZH_CN` null at seed time (no source data for these yet)
- `LegalName_EN` / `_ZH_TW` / `_ZH_CN` (string) — leave null at seed time, no source
  data yet
- `RicCode` (string?)
- `YahooSymbol` (string?)
- `IsinCode` (string?)
- `MarketId` (int, FK → `Market`) — default every seeded row to `MAIN`
- `SectorId` (int?, FK → `Sector`) — nullable; leave `null` at seed time, the JSON has
  no sector data to migrate
- `SubSectorId` (int?, FK → `SubSector`) — nullable, same reason
- `ShariahCompliant` (bool) — default `false` at seed time, no source data
- `Currency` (string(3)) — default `MYR`
- `IsActive` (bool) — default `true`
- Raw fundamentals (all nullable, all `null` at seed time): `CurrentPrice`,
  `MarketCap`, `EPS`, `DPS`, `NTA`, `ROE`, `ROA`, `DebtToEquity`, `CurrentRatio`,
  `LastScrapedAt` (`DateTimeOffset?`)
- Calculated fundamentals (all nullable, all `null` at seed time — **do not compute
  these during seeding**, that's a future job's responsibility): `PE`, `PB`,
  `DividendYield`, `LastCalculatedAt` (`DateTimeOffset?`)
- *(+ shared audit columns)*

**Seed:** one `Stock` row per entry in `wwwroot/data/klse-stocks.json` (~900 rows).
Map `code` → `StockCode`, `name` → `ShortName_EN`. Ignore the JSON's `price` field
entirely (it's always empty in the source file and superseded by `CurrentPrice`
anyway).

## Migration & DbContext work

1. Create entity classes for all 7 tables above in a sensible location (e.g.
   `Models/MasterData/` or alongside existing `Models/*.cs` — match whatever
   convention the rest of `Models/` already uses).
2. Add a `DbSet<T>` for each to `Data/ApplicationDbContext.cs`.
3. In `OnModelCreating` (override it — `ApplicationDbContext` doesn't currently
   override this, so you're adding the override), configure:
   - All FK relationships listed above.
   - Global query filters: `modelBuilder.Entity<T>().HasQueryFilter(e => !e.IsDeleted)`
     for all 7 entities.
   - Any `HasData(...)` seed calls for `Country`/`Exchange`/`Market`/`Sector`/
     `SubSector`/`Institution` (the small reference tables). The ~900-row `Stock` seed
     is large enough that a separate data-seeding method (run once, e.g. from
     `Program.cs` on startup if the table is empty, or a one-off console command) is
     cleaner than a giant `HasData` migration — your call, just document which
     approach you used.
4. `dotnet ef migrations add AddMasterDataSchema --project omm`
5. `dotnet build` — must succeed with no errors.
6. **Do not run `dotnet ef database update` against any shared/production database
   without explicit sign-off.** Running it against a local/throwaway dev DB to verify
   the migration applies cleanly is fine and expected.

## Acceptance criteria (report these back explicitly)

- [ ] `dotnet build` succeeds.
- [ ] Migration applies cleanly to a fresh local DB.
- [ ] `SELECT COUNT(*) FROM Stock` (or EF equivalent) returns the same row count as
      entries in `klse-stocks.json`.
- [ ] Soft-deleting a `Stock` row (set `IsDeleted = true` and refresh) makes it
      disappear from a normal EF query, but the row is still present if you bypass the
      filter (`IgnoreQueryFilters()`).
- [ ] Confirm `CreatedByUserId` is nullable on all 7 tables and every seeded row has
      it set to `null` (not a placeholder string of any kind).
- [ ] State explicitly which `Stock` seeding approach you used (`HasData` vs. separate
      seed method).

## Explicitly out of scope for this phase

- Anything about roles, the `superadmin` account itself, or admin UI/pages — that's
  Phases 2–6.
- Wiring `IKlseStockLookupService` to read from the DB — that's Phase 7.
- Any scraper or calculation job for fundamentals — that's Phase 8, not scheduled.