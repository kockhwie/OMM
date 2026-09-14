# Internal Calculation Job — Design

> **Status: DESIGN ONLY — Phase 8, not yet implemented.**
> This document answers the four questions required by the task plan (Task C3).
> Implementation should not begin until this design is reviewed and approved.

---

## 1. Which Fields Are Computed

The calculation job owns exactly **three derived columns** on `Stock`, plus the
audit timestamp `LastCalculatedAt`. It reads from raw fundamentals columns and
never touches them.

| Computed Column | Formula | Notes |
|---|---|---|
| `PE` | `CurrentPrice / EPS` | Price-to-Earnings ratio. `null` if either input is `null` or if `EPS` ≤ 0 (avoid divide-by-zero and negative PE) |
| `PB` | `CurrentPrice / NTA` | Price-to-Book ratio (using book value per share as NTA proxy). `null` if either input is `null` or if `NTA` ≤ 0 |
| `DividendYield` | `(DPS / CurrentPrice) × 100` | Percentage. `null` if either input is `null` or if `CurrentPrice` ≤ 0. Stored as a percentage, not a decimal fraction (e.g. `4.5` means 4.5%, not 0.045) |
| `LastCalculatedAt` | `DateTimeOffset.UtcNow` | Set only when at least one of PE/PB/DividendYield changes or was previously null |

**Columns the job must never touch:**
- All raw fundamentals (`CurrentPrice`, `EPS`, `DPS`, `NTA`, `ROE`, `ROA`,
  `DebtToEquity`, `CurrentRatio`, `MarketCap`, `LastScrapedAt`)
- All identity/classification fields (`StockCode`, `ShortName_*`, `MarketId`,
  `SectorId`, etc.)
- All audit columns (`CreatedByUserId`, `ModifiedByUserId`, etc.)

---

## 2. Trigger

**Automatic: run immediately after each scraper run completes successfully.**

The external fundamentals scraper (see `docs/scraper-design.md`) writes
`CurrentPrice`, `EPS`, `DPS`, and `NTA`. The calculation job should fire as
soon as those values are fresh.

### Trigger mechanism

The scraper's `FundamentalsScraper` calls `ICalculationJob.RunAsync()` at the
end of each successful full-scrape run. This is a direct in-process call — no
message queue, no timer — because both services run in `OMM.Admin` and the
calculation job is cheap (pure arithmetic, no HTTP calls).

```csharp
// At the end of FundamentalsScraper.RunFullScrapeAsync():
await _calculationJob.RunAsync(cancellationToken);
```

### On-demand trigger (future)

An admin-triggered **"Recalculate"** button on `/admin/scraper` can call
`ICalculationJob.RunAsync()` directly via a scoped service. This covers:
- Corrections made via the stock admin form (e.g. admin manually fixes a bad
  `EPS` value and wants derived fields updated immediately without waiting for
  the next scrape).
- Testing or validation after a deployment.

### Separate scheduled trigger (not recommended)

A standalone cron-style timer for the calculation job alone is unnecessary.
The job is idempotent and cheap, so coupling it to the scraper is simpler than
scheduling it independently. If the scraper is disabled, there is nothing new to
calculate.

---

## 3. Update Strategy

**Only write if raw values changed since `LastCalculatedAt`.**

### Algorithm

```
For each active, non-deleted Stock:
  1. Skip if LastScrapedAt is null (never scraped — no raw data to calculate from)
  2. Skip if LastCalculatedAt >= LastScrapedAt
     (calculated values are already current with the latest scrape)
  3. Compute PE, PB, DividendYield using the formulas above
  4. If computed values differ from current stored values (or were null):
       UPDATE Stock SET PE=..., PB=..., DividendYield=..., LastCalculatedAt=NOW()
       WHERE Id = ...
  5. Else: skip (no write)
```

### Why store-on-change rather than always-write

- Avoids spurious `ModifiedAt` / `ModifiedByUserId` churn on rows whose inputs
  did not change.
- Reduces PostgreSQL WAL pressure on the Neon free tier.
- Makes `LastCalculatedAt` a meaningful "this row was actually updated" signal
  rather than "the job ran and touched this row".

### Null/zero guard

If `EPS ≤ 0` or `CurrentPrice ≤ 0` or any required input is `null`, store
`null` for the affected derived field. Do not store `0`, `Infinity`, or throw.
Log at `Debug` level which stocks were skipped and why.

### Batch size

Process stocks in batches of **100** using `EF Core AsNoTracking` queries.
Build update commands in-memory and execute with `ExecuteUpdateAsync` (EF Core
7+ bulk updates) rather than fetching and `SaveChangesAsync`-ing each row
individually. This avoids loading 900 tracked entities into memory.

---

## 4. Where the Job Lives

**`OMM.Admin` — same service layer as the scraper**

### Rationale

Per `AGENTS.md` service placement rule: *"Could any other project in this
solution ever need this?"* — No. `PE`, `PB`, and `DividendYield` are computed
from `Stock` columns that only `OMM.Admin` owns the write path for. The public
app (`OMM.Public`) reads these columns via Dapper; it does not write them.

### File placement

```
OMM.Admin/
  Services/
    Scraper/
      ICalculationJob.cs      ← single method: Task RunAsync(CancellationToken)
      CalculationJob.cs       ← implementation; injected into FundamentalsScraper
```

### DI registration

```csharp
// In OMM.Admin/Program.cs (alongside the scraper registration):
builder.Services.AddScoped<ICalculationJob, CalculationJob>();
builder.Services.AddScoped<IFundamentalsScraper, FundamentalsScraper>();
builder.Services.AddHostedService<ScraperBackgroundService>();
```

`CalculationJob` is `Scoped` (not `Singleton`) so it receives a fresh
`IDbContextFactory<MasterDataDbContext>` instance per invocation and does not
hold open a long-lived connection during the hours between scrape runs.

---

## Implementation Checklist (for execution phase)

- [ ] `ICalculationJob.cs` with `Task RunAsync(CancellationToken ct)` signature
- [ ] `CalculationJob.cs` — batched EF Core `ExecuteUpdateAsync` implementation
- [ ] Unit tests: formula correctness (PE, PB, DividendYield), null/zero guards,
      skip-if-current logic
- [ ] Integration: `FundamentalsScraper` calls `ICalculationJob.RunAsync()` at end
- [ ] Admin page (future): "Recalculate Now" button that calls the job on-demand
- [ ] Logging: structured log entries for run start, row counts updated/skipped,
      run duration

---

## Open Questions (for implementation phase)

1. **Manual admin edit path:** if an admin corrects `EPS` via the stock form,
   should the form immediately trigger `ICalculationJob` to recalculate that
   single stock? This is desirable UX but requires the admin CRUD form to
   inject `ICalculationJob`. Leave as a follow-up — the nightly scraper will
   correct it within 24 hours regardless.
2. **`ModifiedByUserId` on calc updates:** the job is automated, not a human
   admin. Options: leave `ModifiedByUserId` null (signals "system update"), or
   set it to a dedicated "system" sentinel value. Recommend: leave null, since
   per `AGENTS.md` master-data audit columns are nullable text and null already
   means "automated/no human".
3. **Historical PE/PB tracking:** out of scope for Phase 8. If time-series
   charting of PE bands is required (a common retail-investor feature), a
   separate `StockFundamentalsHistory` table will be needed.
