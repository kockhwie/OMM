# OMM Master Data Design (v2 — Reconciled)

> **Status: LOCKED.** This document supersedes the original `market-data-design.md`
> and reconciles it with `implementation_plan.md`. Any future schema change should
> update this doc first, get sign-off, then flow into code — not the other way round.
>
> Naming convention: **no "Master" suffix on any table.** Tables are named for what
> they are: `Country`, `Exchange`, `Market`, `Sector`, `SubSector`, `Institution`, `Stock`.

---

## 1. Why Country → Exchange → Market (3 levels, from Phase 1)

Malaysia and the US don't shape the same way:

- **Malaysia**: one exchange (Bursa Malaysia), three tiers under it — Main Market, ACE
  Market, LEAP Market.
- **US**: multiple *separate* exchanges (Nasdaq, NYSE), each with its own tiers —
  Nasdaq Global Select / Global / Capital Market; NYSE / NYSE American / NYSE Arca.

A flat `Market` table with just a name would need breaking changes the moment Phase 2
(US) starts. Since this is foundational data every `Stock` row depends on, we build
the 3-level hierarchy now, in Phase 1, even though only Malaysia is seeded initially.

---

## 2. Shared audit columns

Every table below (`Country`, `Exchange`, `Market`, `Sector`, `SubSector`,
`Institution`, `Stock`) gets the same audit block, so every change is traceable to a
user and nothing is hard-deleted:

| Column | Type | Notes |
|---|---|---|
| `CreatedByUserId` | `string?` (FK → `AspNetUsers.Id`) | **Nullable.** `null` means the row was inserted by an automated seed/migration, not a human — this is the normal case for Phase 1's bootstrap reference data, since no admin user exists yet when that data is seeded. Populated with a real user ID for anything created afterward through the admin UI. |
| `CreatedAt` | `DateTimeOffset` | UTC |
| `ModifiedByUserId` | `string?` (FK → `AspNetUsers.Id`) | Null until first edit |
| `ModifiedAt` | `DateTimeOffset?` | Null until first edit |
| `DeletedByUserId` | `string?` (FK → `AspNetUsers.Id`) | Set on soft delete |
| `DeletedAt` | `DateTimeOffset?` | Set on soft delete |
| `IsDeleted` | `bool` | Default `false`. All queries filter `IsDeleted == false` by default (EF Core global query filter) |

All `*ByUserId` columns are real, nullable FKs into `AspNetUsers.Id` (your existing
Identity table) — not free-text names — since Identity auth is already wired up. A
`null` value on any of them simply means "no human has done this yet" (never
created/edited/deleted by a person) — it does not weaken the FK constraint or allow
arbitrary strings in.

Phase 1's seed data (countries, exchanges, markets, sectors, institutions, the ~900
migrated stocks) uses `CreatedByUserId = null` throughout, since no admin user exists
at that point in the build order. Once Phase 2 seeds the `superadmin` account and
admin CRUD pages exist (Phases 5–6), every row an admin actually creates or edits
through the UI gets a real, non-null `CreatedByUserId`/`ModifiedByUserId`. No
coordination between Phase 1 and Phase 2 on user IDs is needed as a result of this
change.

---

## 3. Localization convention (unchanged from v1)

Flat column-suffix localization stays: `_EN` / `_ZH_TW` / `_ZH_CN`. Applies to every
human-readable name field across every table below. Cross-language search stays
always-on regardless of active UI locale. `_ZH_CN` is derived from `_ZH_TW` via
OpenCC-style automated conversion, not hand-typed (per prior decision).

---

## 4. Table definitions

### 4.1 `Country`

| Column | Type | Notes |
|---|---|---|
| `Id` | `int` PK | |
| `CountryCode` | `string(2)` | ISO 3166-1 alpha-2, e.g. `MY`, `US` |
| `CountryName_EN` / `_ZH_TW` / `_ZH_CN` | `string` | |
| `DefaultCurrencyCode` | `string(3)` | e.g. `MYR`, `USD` — default only; a `Stock` can still trade in a different currency |
| `IsActive` | `bool` | |
| *(audit columns)* | | |

Phase 1 seed: `MY` only.

### 4.2 `Exchange`

| Column | Type | Notes |
|---|---|---|
| `Id` | `int` PK | |
| `CountryId` | `int` FK → `Country` | |
| `ExchangeCode` | `string` | Short code for UI, e.g. `BURSA`, `NASDAQ`, `NYSE` |
| `ExchangeName_EN` / `_ZH_TW` / `_ZH_CN` | `string` | Full name, e.g. "Bursa Malaysia" |
| `IsActive` | `bool` | |
| *(audit columns)* | | |

Phase 1 seed: `BURSA` (Bursa Malaysia) under `MY`.

### 4.3 `Market` (tier within an exchange)

| Column | Type | Notes |
|---|---|---|
| `Id` | `int` PK | |
| `ExchangeId` | `int` FK → `Exchange` | |
| `MarketCode` | `string` | **Short code for dashboards/charts** — `MAIN`, `ACE`, `LEAP` (Phase 2 US: `GS`, `GM`, `CM`, `NYSE`, `NYSEAM`, `ARCA`) |
| `MarketName_EN` / `_ZH_TW` / `_ZH_CN` | `string` | Full name — "Main Market", "ACE Market", "LEAP Market" |
| `IsActive` | `bool` | |
| *(audit columns)* | | |

Use `MarketCode` everywhere space is tight (chart labels, compact badges); use
`MarketName_*` in forms and full listings.

Phase 1 seed: `MAIN`, `ACE`, `LEAP` under Bursa Malaysia.

### 4.4 `Sector` / `4.5 SubSector`

| `Sector` Column | Type | Notes |
|---|---|---|
| `Id` | `int` PK | |
| `CountryId` | `int` FK → `Country` | **Locked decision — see note below** |
| `SectorCode` | `string` | Short code |
| `SectorName_EN` / `_ZH_TW` / `_ZH_CN` | `string` | |
| `IsActive` | `bool` | |
| *(audit columns)* | | |

| `SubSector` Column | Type | Notes |
|---|---|---|
| `Id` | `int` PK | |
| `SectorId` | `int` FK → `Sector` | |
| `SubSectorCode` | `string` | |
| `SubSectorName_EN` / `_ZH_TW` / `_ZH_CN` | `string` | |
| `IsActive` | `bool` | |
| *(audit columns)* | | |

Phase 1 seed: the 13 official Bursa Malaysia sectors + their sub-sectors, all scoped
to `CountryId = MY`.

> **✅ Locked:** `Sector` is scoped by `CountryId` because different countries'
> exchanges use different sector taxonomies (Bursa Malaysia's 13-sector system vs. the
> US's GICS convention). This lets Phase 2 seed a completely separate sector list for
> `US` without touching or restructuring Malaysia's rows.

### 4.6 `Institution`

Reused across `Stock` (as issuer/exchange-listing context is actually on `Market`, not
here), and — more importantly — reused as the shared dropdown for `Mine.Institution`
across FD placements, savings mines, EPF/KWSP, gold providers, etc.

| Column | Type | Notes |
|---|---|---|
| `Id` | `int` PK | |
| `CountryId` | `int?` FK → `Country` | Nullable — some institutions may be multi-country later |
| `InstitutionCode` | `string` | Short code, e.g. `MAYBANK`, `CIMB`, `KWSP` |
| `InstitutionName_EN` / `_ZH_TW` / `_ZH_CN` | `string` | |
| `InstitutionCategory` | `enum` | `Bank`, `Broker`, `EpfKwsp`, `GoldProvider`, `Insurance`, `Other` |
| `IsActive` | `bool` | Status flag used to hide retired institutions from new dropdowns without deleting history |
| *(audit columns)* | | |

Phase 1 seed: the institutions already referenced in `MockMineService` — Maybank,
CIMB, Public Bank, KWSP, Bursa Malaysia (as a data-source reference, category `Other`)
— so existing mock data has somewhere real to point to.

### 4.7 `Stock`

| Column | Type | Notes |
|---|---|---|
| `Id` | `int` PK | |
| `StockCode` | `string` | **The Bursa numeric code**, e.g. `"1155"` — matches Bursa terminology and the existing `klse-stocks.json` |
| `ShortName_EN` / `_ZH_TW` / `_ZH_CN` | `string` | Display name Malaysians actually search by, e.g. "Maybank" |
| `LegalName_EN` / `_ZH_TW` / `_ZH_CN` | `string` | Full registered name, e.g. "Malayan Banking Berhad" |
| `RicCode` | `string?` | e.g. `MBBM.KL` — for `my.bursamalaysia.com` |
| `YahooSymbol` | `string?` | e.g. `1155.KL` — for Yahoo Finance live price polling |
| `IsinCode` | `string?` | e.g. `MYL1155OO000` — regulatory / corporate-action mapping |
| `MarketId` | `int` FK → `Market` | |
| `SectorId` | `int?` FK → `Sector` | Nullable — the migrated JSON has no sector data, so seeded rows leave this `null` until classified later |
| `SubSectorId` | `int?` FK → `SubSector` | |
| `ShariahCompliant` | `bool` | |
| `Currency` | `string(3)` | Defaults from `Country.DefaultCurrencyCode` via `Market → Exchange → Country`, but stays an explicit, overridable column (a stock can be foreign-currency-denominated) |
| `IsActive` | `bool` | |
| *(audit columns)* | | |

**Raw fundamentals — pulled by future external scraper, nullable, default `null`:**

| Column | Type | Notes |
|---|---|---|
| `CurrentPrice` | `decimal?` | |
| `MarketCap` | `decimal?` | |
| `EPS` | `decimal?` | |
| `DPS` | `decimal?` | |
| `NTA` | `decimal?` | |
| `ROE` | `decimal?` | Not derivable from other fields — must come from source |
| `ROA` | `decimal?` | Not derivable — must come from source |
| `DebtToEquity` | `decimal?` | Not derivable — must come from source |
| `CurrentRatio` | `decimal?` | Not derivable — must come from source |
| `LastScrapedAt` | `DateTimeOffset?` | When raw fields were last pulled externally |

**Calculated fundamentals — stored columns, but populated by an internal calculation
job (not the scraper), per your decision to keep the scrape job and the calc job
separate:**

| Column | Type | Formula |
|---|---|---|
| `PE` | `decimal?` | `CurrentPrice / EPS` |
| `PB` | `decimal?` | `CurrentPrice / NTA` |
| `DividendYield` | `decimal?` | `DPS / CurrentPrice × 100` |
| `LastCalculatedAt` | `DateTimeOffset?` | When the calc job last ran for this row |

**Manual-edit rule (locked):** there is **no** `IsManuallyOverridden` flag. A manual
edit in the admin form is authoritative until the next scrape/calc job overwrites it.
This is intentional — you've said manual edits exist specifically to correct bad or
missing source data, and re-flagging every field would add friction for little
benefit.

**Admin form behavior (locked):** every column above the "raw fundamentals" divider is
a normal editable field. Every column from "raw fundamentals" down — including the
calculated ones — renders as **read-only** in the standard admin form. (A future
superadmin-only override mode is a possibility, not built now.)

Phase 1 seed: migrate all ~900 rows currently in `wwwroot/data/klse-stocks.json` into
`Stock`, with `MarketId` defaulted to `MAIN` and `SectorId`/`SubSectorId` left
unassigned (nullable at seed time, or a placeholder "Unclassified" sector — decide
during Phase 1 execution since the JSON has no sector data to migrate from).

---

## 5. What replaced what

| Old (`implementation_plan.md` draft) | New (this doc) | Why |
|---|---|---|
| `StockCode` = mnemonic ("MAYBANK") | Dropped — `StockCode` is the numeric Bursa code | Avoids two different tables meaning different things by the same name |
| `StockNumber` = numeric ("1155") | Folded into `StockCode` | Same reason |
| `CompanyName` | Split into `ShortName_*` / `LegalName_*` | Keeps the locked localization + dual-name convention |
| `Market` as flat string enum | 3-level `Country → Exchange → Market` tables | US Phase 2 needs exchange-level grouping, not just tier names |
| No `InstitutionMaster` mentioned | `Institution` table, reused beyond stocks | Reuse across FD/savings mine dropdowns was the original point of this table |
| No audit fields | Full audit block on every table | Traceability requirement added this round |