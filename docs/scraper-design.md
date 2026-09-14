# External Fundamentals Scraper — Design

> **Status: DESIGN ONLY — Phase 8, not yet implemented.**
> This document answers the five questions required by the task plan (Task C2).
> Implementation should not begin until this design is reviewed and approved.

---

## 1. Data Source

**Primary: Yahoo Finance (via `YahooSymbol`)**

Each `Stock` row already carries a `YahooSymbol` column (e.g. `1155.KL` for Maybank).
Yahoo Finance exposes an unofficial JSON endpoint:

```
https://query1.finance.yahoo.com/v8/finance/chart/{symbol}?interval=1d&range=1d
```

and a quoteSummary endpoint for fundamentals:

```
https://query1.finance.yahoo.com/v11/finance/quoteSummary/{symbol}?modules=defaultKeyStatistics,financialData,price
```

These endpoints are rate-limited at roughly **2,000 requests / hour** without
authentication and have been stable for several years, though Yahoo does not
publish a formal SLA for them.

**Why Yahoo over Bursa's own portal:**
- Bursa Malaysia's `my.bursamalaysia.com` does not publish a documented public API.
- `RicCode` (e.g. `MBBM.KL`) can feed a Refinitiv/LSEG integration later if
  budget allows, without changing the scraper's interface.
- Yahoo Finance already covers the full ~900-stock KLSE universe.

**Fallback: Bursa Malaysia HTML scrape (contingency only)**
If Yahoo Finance becomes unavailable, Bursa's stock detail page at
`https://www.bursamalaysia.com/market_information/equities_prices/ui` can be
parsed. This is a last resort — HTML structure changes break scrapers silently.

---

## 2. Field Mapping

The scraper is responsible for **raw input fields only**. Calculated fields
(`PE`, `PB`, `DividendYield`, `LastCalculatedAt`) are **never touched** by the
scraper. The calculation job (see `docs/calculator-job-design.md`) owns those.

| `Stock` Column | Yahoo Finance source | Notes |
|---|---|---|
| `CurrentPrice` | `price.regularMarketPrice` | Real-time or last close |
| `MarketCap` | `price.marketCap` | In local currency (MYR for KLSE) |
| `EPS` | `defaultKeyStatistics.trailingEps` | Trailing twelve months |
| `DPS` | `defaultKeyStatistics.trailingAnnualDividendRate` | Trailing annual |
| `NTA` | `defaultKeyStatistics.bookValue` | Book value per share — closest available proxy |
| `ROE` | `financialData.returnOnEquity` | Expressed as a decimal (0.12 = 12%); store as decimal (0.12) |
| `ROA` | `financialData.returnOnAssets` | Same convention as ROE |
| `DebtToEquity` | `financialData.debtToEquity` | Yahoo returns this as a ratio × 100; divide by 100 before storing |
| `CurrentRatio` | `financialData.currentRatio` | |
| `LastScrapedAt` | Set to `DateTimeOffset.UtcNow` by the scraper after a successful row update | |

> **NTA note:** Yahoo Finance does not expose a dedicated NTA (Net Tangible
> Assets per share) field. `bookValue` is the closest available metric and is
> widely used as a proxy in Malaysian retail investing. If a more precise NTA
> is required, an admin override via the stock edit form is the intended path.

---

## 3. Manual-Edit Protection — How to Detect and Skip

**Decision from `market-data-design.md` §4.7 (locked):** there is no
`IsManuallyOverridden` flag. A manual edit in the admin form is authoritative
**until the next scrape overwrites it**.

This means: the scraper does **not** skip manually-edited fields — it simply
overwrites all raw fundamentals on each successful scrape.

**Rationale:** manual edits exist to fix bad or missing source data. If Yahoo
Finance starts returning correct data, the next scrape should automatically
restore it. Requiring an admin to remove an override flag before data flows
again adds friction with no clear benefit.

**Operator expectation:** admins who manually set a fundamentals value should
understand it will be overwritten on the next scheduled scrape. An audit trail
of manual edits exists in `AdminAuditLog` (if the admin form calls
`_auditLogger.LogAsync` for fundamentals changes — a future consideration).

**Exception case:** if a stock has no `YahooSymbol` set (null or empty string),
the scraper skips that row entirely and logs it at `Warning` level. This is the
explicit "do not scrape" signal — an admin clears `YahooSymbol` to suppress
scraping for a delisted or data-problematic stock.

---

## 4. Retry and Backoff Policy

```
Per-stock attempt:
  Attempt 1:  immediate
  Attempt 2:  2 s wait
  Attempt 3:  8 s wait
  (give up after 3 attempts per stock per run)

Per-run circuit breaker:
  If ≥ 10 consecutive stocks fail → pause the entire run for 5 minutes, then
  resume from the next stock (not a retry of the failed stocks).
  If ≥ 30 total failures in one run → abort the run and emit a structured
  Error-level log entry.

Rate limiting (Yahoo Finance):
  Insert a 200 ms sleep between each successful stock request.
  This keeps throughput at ~5 req/s, well under the ~0.55 req/s sustained
  limit implied by the 2,000/hr cap (but leaves room for concurrent admin
  usage of the admin app).

HTTP client:
  Timeout: 10 s per request.
  User-Agent: set to a realistic browser string — Yahoo blocks the default
  HttpClient agent.
```

All retry decisions use jitter (±20%) to avoid thundering-herd on Yahoo's
CDN after a brief blackout.

---

## 5. Where It Runs

**`OMM.Admin` — as a registered `IHostedService` (background service)**

### Rationale

| Option | Pros | Cons |
|---|---|---|
| `IHostedService` in `OMM.Admin` | No new process; shares DI with admin app; already has `ApplicationDbContext` and EF Core | Shares memory and CPU with the admin web app |
| Separate worker process | Clean isolation | New project to deploy and manage; adds Render service cost |
| Neon Function | Serverless; no infra to manage | No `DbContext` or EF; harder to integrate; cold start latency; no .NET runtime on Neon Functions today |

`OMM.Admin` already runs 24/7 on Render. Adding a hosted service adds no new
deployment surface. The ~900-stock KLSE universe takes roughly **3 minutes per
full run** at 200 ms/stock (900 × 200 ms ≈ 3 min) — well within background-
service tolerance for a low-traffic admin app.

### Trigger

**Scheduled: once per trading day, at 18:00 MYT (10:00 UTC).**

Bursa Malaysia closes at 17:00 MYT. A 18:00 trigger gives one hour for
closing prices to settle into Yahoo's data feed.

Implementation: a `PeriodicTimer`-based `BackgroundService` that calculates
the next 10:00 UTC wake-up, sleeps until then, runs the scrape, then
recalculates the next run.

An admin-triggered **"Run now"** button on a future `/admin/scraper` page would
call the same scrape logic via a scoped service, bypassing the timer. This lets
operators refresh data after a market event without waiting for the next
scheduled run.

### Placement within `OMM.Admin`

```
OMM.Admin/
  Services/
    Scraper/
      IFundamentalsScraper.cs     ← interface (scrape one stock or all)
      FundamentalsScraper.cs      ← Yahoo Finance HTTP implementation
      ScraperBackgroundService.cs ← IHostedService, owns the timer
      ScraperOptions.cs           ← config: schedule, rate limit, retry settings
```

`IFundamentalsScraper` is in `OMM.Admin/Services/Scraper/` because it is
tightly coupled to the admin `DbContext` (`MasterDataDbContext` + EF Core). No
other project in the solution needs it. Per `AGENTS.md` service placement rule:
**Admin-only → `OMM.Admin`**.

### Configuration (`appsettings.json`)

```json
"Scraper": {
  "ScheduledUtcHour": 10,
  "RequestDelayMs": 200,
  "MaxRetriesPerStock": 3,
  "CircuitBreakerConsecutiveFailures": 10,
  "CircuitBreakerAbortTotalFailures": 30
}
```

---

## Open Questions (for implementation phase)

1. **Yahoo Finance T&C:** Yahoo's terms technically prohibit automated scraping.
   Acceptable risk for an internal admin tool with low volume; evaluate a paid
   source (Alpha Vantage, Refinitiv, Bursa Data Hub) if this becomes a
   compliance concern.
2. **Historical data:** this design covers only the *latest* snapshot per stock
   (overwrite in place). A time-series history table is out of scope for Phase 8.
3. **Admin notification on run failure:** should the scraper email the admin on
   circuit-breaker abort? Requires email to be configured (Task C1) and a
   suitable notification hook in `DatabaseAlerting` or a new alert type.
