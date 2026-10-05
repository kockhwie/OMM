# Add Mine — Adaptive Form: Final Implementation Plan

> **Scope locked on 2026-10-01**  
> Based on 37-page Malaysian Asset Class research + OMMv2 codebase analysis.  
> Execute tasks **in order**. Each task is self-contained and agent-ready.

---

## Confirmed Scope Decisions

| Item | Decision |
|---|---|
| ASB / ASM | **MVP** — included in this sprint |
| Cryptocurrency | **MVP** — included in this sprint |
| US Stocks | **MVP** — manual MYR entry for current price/value (no exchange rate API needed) |
| PRS | **Phase 3** — stub only (`Others` catch-all for now) |
| Tabung Haji | **Phase 2** — stub only in this sprint |
| Edit Mine | **Yes** — same adaptive modal re-used for editing |
| Delete Mine | **Yes** — already exists via `SoftDeleteMineAsync`, expose in UI |
| Income History (Cash Events) | **Yes** — via existing `IncomeRecordEntity` (already has `MineId`) |
| Yield & Rate History (Growth Tracing) | **Yes** — via `MineYieldHistoryEntity` (Task 1.4 & 4.3) |
| Tools Integration ("Save as Mine to Track") | **Yes** — upgrade Dividend Calculator + FD Calculator to save rich metadata (Task 3.4) |

---

## Architecture Decision: Income, Rate History & Tools Bridge

### 1. Cash Income Events vs. Rate / Yield History

There are two fundamentally different types of records users need to track:

#### A. Cash Income Event Log (`IncomeRecordEntity` — already exists)
> *"EPF credited RM 2,400 dividend into my account in February 2025."*
- **What it is:** Real cashflow received or credited.
- **Table:** Reuses existing [`IncomeRecordEntity`](file:///c:/Users/User/source/repos/OMMv2/OMM.Public/Data/Entities/IncomeRecordEntity.cs) (already has `MineId` FK, `Amount`, `RecordDate`, `Frequency`).
- **Where it feeds:** Feeds the **Freedom Ratio**, Monthly Passive Income gauges, and [`Income.razor`](file:///c:/Users/User/source/repos/OMMv2/OMM.Public/Components/Pages/Income.razor).

#### B. Yield & Rate Snapshots (`MineYieldHistoryEntity` — Task 1.4)
> *"EPF declared 5.4% dividend in 2024, and 6.0% in 2025 (+0.60% growth)."*  
> *"FD placed at 3.85% for 12 months in 2024, renewed at 4.10% in 2025."*
- **What it is:** Historical timeline of declared percentage rates and baseline metrics.
- **Why it matters:** Allows the user to trace rate/dividend growth over years (e.g. EPF/ASB dividend rate trend, FD placement history, stock yield changes) without overwriting past data.
- **Edit Mine UX Pattern:** When a user updates their EPF dividend rate, FD renewal rate, or stock yield in the Edit Mine modal, an option prompts: *"Save previous rate to history timeline for growth tracing?"* (checked by default).

---

### 2. Tools Integration Bridge: "Save as Mine to Track"

In `/tools/dividend-calculator` (and `/tools/fd-calculator`), users calculate dividend yields, share purchases, and FD projections. Currently, `CalculatorDividend.razor` has a `"Save as Mine to Track"` button that creates a generic `Mine` with flat fields.

**The Upgrade (Task 3.4):**
- When the user clicks `"Save as Mine to Track"` in `/tools/dividend-calculator`, it directly builds the typed `MineMetadata` (`StockCode`, `SharesOwned`, `CurrentSharePrice`, `PurchasePricePerShare`, `DividendYieldPct`, `Exchange = Bursa`) and attaches it to `Mine.Metadata`.
- Direct call to `MineService.AddMineAsync(newMine)` preserves all granular position calculations.
- Optional: Initial rate snapshot saved into `MineYieldHistoryEntity`.
- Notification toast gives direct interactive link: *"Saved to Mines! [View in Mines →]"*.
- Extends the same pattern to `/tools/fd-calculator` so users can calculate FD returns and immediately track them as an FD Mine.

---

## Context for Every Agent

Before touching code, every agent must read:
- [`OMM.Public/Models/Mine.cs`](file:///c:/Users/User/source/repos/OMMv2/OMM.Public/Models/Mine.cs) — enums + domain model
- [`OMM.Public/Data/Entities/MineEntity.cs`](file:///c:/Users/User/source/repos/OMMv2/OMM.Public/Data/Entities/MineEntity.cs) — EF Core entity
- [`OMM.Public/Data/Entities/MinePositionEntity.cs`](file:///c:/Users/User/source/repos/OMMv2/OMM.Public/Data/Entities/MinePositionEntity.cs) — sub-positions
- [`OMM.Public/Data/Entities/IncomeRecordEntity.cs`](file:///c:/Users/User/source/repos/OMMv2/OMM.Public/Data/Entities/IncomeRecordEntity.cs) — income log (MineId FK already present)
- [`OMM.Public/Services/FormatHelper.cs`](file:///c:/Users/User/source/repos/OMMv2/OMM.Public/Services/FormatHelper.cs) — labels, icons, category/type mappings
- [`OMM.Public/Services/DatabaseMineService.cs`](file:///c:/Users/User/source/repos/OMMv2/OMM.Public/Services/DatabaseMineService.cs) — service layer
- [`OMM.Public/Components/Pages/Mines.razor`](file:///c:/Users/User/source/repos/OMMv2/OMM.Public/Components/Pages/Mines.razor) — current Add Mine modal
- [`OMM.Public/Components/Pages/Income.razor`](file:///c:/Users/User/source/repos/OMMv2/OMM.Public/Components/Pages/Income.razor) — income page + modal
- [`docs/ui-form-standards.md`](file:///c:/Users/User/source/repos/OMMv2/docs/ui-form-standards.md) — mandatory form CSS primitives

---

## Phase 1 — Data Model & Enum Expansion

### Task 1.1 — Expand `MineCategory` and `MineType` Enums + Add `MineMetadata` POCO

**File:** `OMM.Public/Models/Mine.cs`

**Replace `MineCategory`:**
```csharp
public enum MineCategory
{
    Retirement,         // EPF, PRS (Phase 3)
    CashAndDeposits,    // Savings Account, FD, Cash in Hand
    Investments,        // Stocks (MY + US), REIT, Unit Trust, ETF, ASB
    Property,           // Residential, Commercial
    PreciousMetals,     // Gold, Silver
    Digital,            // Cryptocurrency
    Other
}
```

**Replace `MineType`:**
```csharp
public enum MineType
{
    // Retirement
    EpfKwsp,
    // TabungHaji → Phase 2 (add here as stub, hide from UI picker for now)
    // Prs → Phase 3

    // Cash & Deposits
    SavingsAccount,
    FixedDeposit,
    CashInHand,

    // Investments
    UnitTrustAsb,       // ASB / ASM — fixed price RM 1.00/unit
    UnitTrustGeneral,   // General unit trust / mutual funds
    Stocks,             // Bursa Malaysia stocks
    StocksUs,           // US / foreign stocks (manual MYR entry)
    Reit,
    Etf,

    // Property
    PropertyResidential,
    PropertyCommercial,

    // Precious Metals
    Gold,
    Silver,

    // Digital
    Cryptocurrency,

    // Catch-all
    Others
}
```

**Add `MineMetadata` POCO** (NOT a DB entity — serialised to JSON):
```csharp
/// <summary>
/// Type-specific fields captured during Add/Edit Mine.
/// Serialised as JSON into MineEntity.MetadataJson (jsonb column).
/// All fields nullable — only populate what applies to the MineType.
/// </summary>
public class MineMetadata
{
    // --- Shared optional ---
    public string? AccountNumber           { get; set; } // last 4 digits, reference only
    public string? Notes                   { get; set; }

    // --- EPF / Savings ---
    public string? AccountType             { get; set; } // e.g. "Conventional", "Shariah"

    // --- ASB / Unit Trust ---
    public decimal? UnitsHeld              { get; set; }
    public decimal? NavPerUnit             { get; set; } // RM 1.00 for ASB, market NAV for others
    public string?  FundCode               { get; set; } // "ASB", "ASM 2", fund name

    // --- Fixed Deposit ---
    public decimal? Principal              { get; set; }
    public decimal? InterestRatePct        { get; set; } // % per annum
    public int?     TenureMonths           { get; set; }
    public DateOnly? MaturityDate          { get; set; }
    public string?  RolloverBehavior       { get; set; } // "auto-renew" | "cash-out"

    // --- Stocks / ETF ---
    public string?  StockCode              { get; set; } // e.g. "MAYBANK", "AAPL"
    public string?  Exchange               { get; set; } // "Bursa" | "NASDAQ" | "NYSE"
    public int?     SharesOwned            { get; set; } // in units (not lots)
    public decimal? CurrentSharePrice      { get; set; } // latest price in asset currency
    public decimal? PurchasePricePerShare  { get; set; } // average cost per share
    public decimal? DividendYieldPct       { get; set; } // % per annum estimate

    // --- Property ---
    public string?  Address                { get; set; }
    public string?  PropertyType           { get; set; } // "Condo", "Terraced", "Semi-D", "Apartment", "Bungalow"
    public decimal? OwnershipPct           { get; set; } // default 100
    public decimal? RentalIncomeMonthly    { get; set; }
    public decimal? MaintenanceCostMonthly { get; set; }
    public Guid?    LinkedMortgageBurdenId { get; set; }

    // --- Gold / Silver ---
    public decimal? WeightGrams            { get; set; }
    public string?  GoldForm               { get; set; } // "Physical Bar" | "Jewellery" | "Digital (GIA/GAP)"
    public decimal? BuybackPricePerGram    { get; set; }

    // --- Cryptocurrency ---
    public string?  CoinSymbol             { get; set; } // "BTC", "ETH", "BNB"
    public decimal? CoinsHeld              { get; set; } // high precision
    public decimal? PurchasePricePerCoin   { get; set; } // in MYR
    public string?  WalletOrExchange       { get; set; }
}
```

**Add `Metadata` to `Mine` model:**
```csharp
public MineMetadata? Metadata { get; set; }
```

---

### Task 1.2 — Add `MetadataJson` Column to `MineEntity` + EF Migration

**File:** `OMM.Public/Data/Entities/MineEntity.cs`

Add:
```csharp
/// <summary>Type-specific metadata. Schema varies by MineType. Stored as PostgreSQL jsonb.</summary>
public string? MetadataJson { get; set; }
```

**File:** `OMM.Public/Data/ApplicationDbContext.cs`

In `OnModelCreating`:
```csharp
modelBuilder.Entity<MineEntity>()
    .Property(e => e.MetadataJson)
    .HasColumnType("jsonb")
    .HasColumnName("metadata_json");
```

**Generate migration** (agent writes the command, does NOT run it):
```
dotnet ef migrations add AddMineMetadataJson --project OMM.Public --startup-project OMM.Public
```

> Tell the user: "Apply with `dotnet ef database update --project OMM.Public --startup-project OMM.Public`"

---

### Task 1.3 — Update `FormatHelper` for New Enums

**File:** `OMM.Public/Services/FormatHelper.cs`

1. **Expand `CategoryLabel`**:

| Category | Label |
|---|---|
| Digital | Digital Assets |
| Other | Others |

2. **Expand `MineTypeLabel`**:

| MineType | Display Label |
|---|---|
| EpfKwsp | EPF / KWSP |
| SavingsAccount | Savings Account |
| FixedDeposit | Fixed Deposit (FD) |
| CashInHand | Cash in Hand |
| UnitTrustAsb | ASB / ASM |
| UnitTrustGeneral | Unit Trust / Mutual Fund |
| Stocks | Stocks (Bursa Malaysia) |
| StocksUs | Stocks (US / Foreign) |
| Reit | REIT |
| Etf | ETF |
| PropertyResidential | Residential Property |
| PropertyCommercial | Commercial Property |
| Gold | Gold |
| Silver | Silver |
| Cryptocurrency | Cryptocurrency |
| Others | Others |

3. **Update `GetTypesForCategory`**:
```csharp
MineCategory.Retirement      => [EpfKwsp, Others],
MineCategory.CashAndDeposits => [SavingsAccount, FixedDeposit, CashInHand, Others],
MineCategory.Investments     => [UnitTrustAsb, UnitTrustGeneral, Stocks, StocksUs, Reit, Etf, Others],
MineCategory.Property        => [PropertyResidential, PropertyCommercial, Others],
MineCategory.PreciousMetals  => [Gold, Silver, Others],
MineCategory.Digital         => [Cryptocurrency, Others],
_                            => [Others]
```

4. **Update `CategoryToIcon`** — add `Digital` → `"ti-currency-bitcoin"`, `Other` → `"ti-folder"`.

5. **Add new method `CurrentValueLabel(MineType type)`**:

| Types | Label |
|---|---|
| EpfKwsp, SavingsAccount, CashInHand | Balance |
| FixedDeposit | Projected Value |
| UnitTrustAsb, UnitTrustGeneral | Market Value |
| Stocks, StocksUs, Reit, Etf | Market Value |
| PropertyResidential, PropertyCommercial | Valuation |
| Gold, Silver | Est. Value |
| Cryptocurrency | Value (MYR) |
| Others | Current Value |

6. **Add new method `MineTypeToIcon(MineType type)`**:

| Type | Tabler Icon |
|---|---|
| EpfKwsp | ti-shield-lock |
| SavingsAccount | ti-building-bank |
| FixedDeposit | ti-lock-dollar |
| CashInHand | ti-cash |
| UnitTrustAsb | ti-chart-area-filled |
| UnitTrustGeneral | ti-chart-pie |
| Stocks | ti-trending-up |
| StocksUs | ti-globe |
| Reit | ti-building-skyscraper |
| Etf | ti-chart-bar |
| PropertyResidential | ti-home-dollar |
| PropertyCommercial | ti-building-store |
| Gold | ti-coins |
| Silver | ti-coin |
| Cryptocurrency | ti-currency-bitcoin |
| Others | ti-folder |

7. **Update `InferPositionType`** in `DatabaseMineService.cs`:
```csharp
MineType.FixedDeposit                        => PositionType.FixedDeposit,
MineType.Stocks or MineType.StocksUs
    or MineType.Reit or MineType.Etf
    or MineType.UnitTrustGeneral
    or MineType.UnitTrustAsb                 => PositionType.StockLot,
MineType.PropertyResidential
    or MineType.PropertyCommercial           => PositionType.PropertyDetail,
MineType.Gold                                => PositionType.GoldLot,
MineType.Silver                              => PositionType.SilverLot,
_                                            => PositionType.Other
```

---

### Task 1.4 — Add `MineYieldHistoryEntity` + EF Migration (Yield & Rate Growth Tracing)

**Why:** To answer the user's need: *"EPF 2025 dividend was 5%, 2026 is 6%, how to trace growth? Same for FD renewal rate."*  
Tracks historical declared percentage yields and baseline amounts without conflating them with monthly cashflow logs (`IncomeRecordEntity`).

**File:** `OMM.Public/Data/Entities/MineYieldHistoryEntity.cs` (Create)

```csharp
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace OMM.Public.Data.Entities;

[Table("mine_yield_history")]
public class MineYieldHistoryEntity
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    [Column("mine_id")]
    public Guid MineId { get; set; }

    [ForeignKey(nameof(MineId))]
    public virtual MineEntity? Mine { get; set; }

    /// <summary>Calendar year of the declaration (e.g. 2024, 2025) or placement year.</summary>
    [Column("effective_year")]
    public int EffectiveYear { get; set; }

    /// <summary>Exact declaration or placement date.</summary>
    [Column("record_date")]
    public DateOnly RecordDate { get; set; }

    /// <summary>Declared rate % (e.g. 5.50 for 5.5% EPF dividend, or 3.85 for 3.85% FD rate).</summary>
    [Column("rate_pct", TypeName = "decimal(5,2)")]
    public decimal RatePct { get; set; }

    /// <summary>Actual dividend/interest amount in MYR (optional, e.g. RM 2,400).</summary>
    [Column("declared_amount", TypeName = "decimal(18,2)")]
    public decimal? DeclaredAmount { get; set; }

    /// <summary>Total balance or principal at the time of declaration/placement.</summary>
    [Column("balance_at_time", TypeName = "decimal(18,2)")]
    public decimal? BalanceAtTime { get; set; }

    /// <summary>Optional label or notes (e.g. "Simpanan Konvensional", "12-mo Promotional FD").</summary>
    [MaxLength(200)]
    [Column("notes")]
    public string? Notes { get; set; }

    [Column("created_on")]
    public DateTimeOffset CreatedOn { get; set; } = DateTimeOffset.UtcNow;
}
```

**File:** `OMM.Public/Data/Entities/MineEntity.cs`  
Add navigation collection:
```csharp
public virtual ICollection<MineYieldHistoryEntity> YieldHistories { get; set; } = new List<MineYieldHistoryEntity>();
```

**File:** `OMM.Public/Data/ApplicationDbContext.cs`  
Register DbSet and configure:
```csharp
public DbSet<MineYieldHistoryEntity> MineYieldHistories => Set<MineYieldHistoryEntity>();

// In OnModelCreating:
modelBuilder.Entity<MineYieldHistoryEntity>(b =>
{
    b.HasIndex(e => new { e.MineId, e.EffectiveYear });
    b.HasOne(e => e.Mine)
     .WithMany(m => m.YieldHistories)
     .HasForeignKey(e => e.MineId)
     .OnDelete(DeleteBehavior.Cascade);
});
```

**Generate migration** (agent writes command, asks user to apply):
```
dotnet ef migrations add AddMineYieldHistory --project OMM.Public --startup-project OMM.Public
```

---

## Phase 2 — Form UI/UX: Adaptive Add/Edit Mine Modal

### Task 2.1 — Extract `AddMineModal` as a Standalone Component

**Create:** `OMM.Public/Components/Shared/Mines/AddMineModal.razor`

**Parameters:**
```csharp
[Parameter] public bool IsOpen { get; set; }
[Parameter] public EventCallback OnClose { get; set; }
[Parameter] public EventCallback OnSaved { get; set; }

// For edit mode — pass null for add, pass existing Mine for edit
[Parameter] public Mine? ExistingMine { get; set; }
```

**Computed property:** `bool IsEditMode => ExistingMine is not null`

**Inject:** `DatabaseMineService`, `IMinerProfileService`, `IDbContextFactory<ApplicationDbContext>`

**Internal state:**
```csharp
private Mine workingMine = new();
private MineMetadata metadata = new();
private string? inputError;
private string currencySymbol = string.Empty;
private List<Institution> institutions = [];
private int step = 1; // 1 = Category & Type, 2 = Type-specific fields, 3 = Review
```

On `OnParametersSetAsync`: if `ExistingMine != null`, copy it to `workingMine` and deserialise `Metadata`.

**Update `Mines.razor`:**
- Remove the inline modal HTML block.
- Replace with: `<AddMineModal IsOpen="showAddModal" OnClose="CloseAddModal" OnSaved="HandleMineSaved" />`
- Add an Edit button on each Mine card that passes `ExistingMine="mine"`.

> All form CSS must follow `docs/ui-form-standards.md`. Use `omm-form-section`, `omm-form-field`, `omm-form-label`, `omm-form-help`, `omm-form-grid`, `dc-currency-group`. No inline styles.

---

### Task 2.2 — Build Step 1: Visual Category & Type Picker

**Inside `AddMineModal.razor` — Step 1 view:**

Replace the plain `<select>` dropdowns with a **card-grid picker**:

**Category grid (7 cards):**
- Retirement · Cash & Deposits · Investments · Property · Precious Metals · Digital Assets · Others
- Each card: Tabler icon + label. Selected state = dark card background + gold-coloured border.
- Layout: `row row-cols-3 g-2` on desktop, `row-cols-2` on mobile.

**Asset Type pills (shown below category grid):**
- Display pill buttons for the types in the selected category (`FormatHelper.GetTypesForCategory`).
- Selected pill = dark filled. Unselected = outline.

**"Next →"** button advances to Step 2. Disabled until both category + type selected.

**Progress indicator at top of modal:** `Step 1 of 3 — Choose Asset Type`

**Keyboard accessibility:** cards respond to `Enter` / `Space` keypress via `@onkeydown`.

---

### Task 2.3 — Build Step 2: Type-Specific Adaptive Fields

**Inside `AddMineModal.razor` — Step 2 view:**

Use `@switch (workingMine.Type)` to render a type-specific sub-form. The `CurrentValue` field meaning and label changes per type. Auto-calculations must update live (`@bind:event="oninput"`).

---

#### EPF / KWSP
| Field | Bound To | Label | Input | Required | Helper |
|---|---|---|---|---|---|
| Total EPF Balance | `workingMine.CurrentValue` | Account Balance | Currency | Yes | "Copy from i-Akaun. Includes Akaun Persaraan + Akaun Sejahtera + Akaun Fleksibel." |
| Account Type | `metadata.AccountType` | Account Type | Select: Conventional / Shariah i-Akaun | No | — |
| Total Contributions | `workingMine.PurchaseCost` | Total Contributions to Date | Currency | No | "Your total employee + employer contributions paid in." |
| Institution | `workingMine.Institution` | Institution | Select from institutions | No | Default: KWSP |
| Monthly Income | **Hidden** | — | — | — | EPF pays annual dividend, not monthly. Log under Income when declared. |

---

#### Savings Account
| Field | Bound To | Label | Input | Required | Helper |
|---|---|---|---|---|---|
| Balance | `workingMine.CurrentValue` | Account Balance | Currency | Yes | "Current balance in this account." |
| Account Ref | `metadata.AccountNumber` | Last 4 Digits (optional) | Text, maxlen 4 | No | "For your reference only." |
| Institution | `workingMine.Institution` | Bank | Select from institutions | No | — |
| Monthly Interest | `workingMine.MonthlyIncome` | Est. Monthly Interest | Currency | No | "Interest credited. Check your passbook or online banking." |

---

#### Cash in Hand
| Field | Bound To | Label | Input | Required | Helper |
|---|---|---|---|---|---|
| Amount | `workingMine.CurrentValue` | Cash Amount | Currency | Yes | "Physical cash or informal savings (e.g. in a jar, safe)." |
| Notes | `metadata.Notes` | Notes | Text | No | — |
| Monthly Income | **Hidden** | — | — | — | Cash in hand earns no income. |

---

#### Fixed Deposit (FD)
| Field | Bound To | Label | Input | Required | Helper |
|---|---|---|---|---|---|
| Principal | `metadata.Principal` | Principal Amount | Currency | Yes | "Amount placed in this FD." |
| Interest Rate | `metadata.InterestRatePct` | Interest Rate (% p.a.) | Number step 0.01 | Yes | "From your FD confirmation slip or online banking." |
| Tenure | `metadata.TenureMonths` | Tenure (months) | Number integer | Yes | — |
| Maturity Date | `metadata.MaturityDate` | Maturity Date | Date | Auto-calculated | Auto = placement date + tenure. Editable override. |
| Projected Value | `workingMine.CurrentValue` | Projected Value at Maturity | Currency **read-only** | Auto | Auto = Principal × (1 + Rate/100 × TenureMonths/12). Shown as a computed badge. |
| On Maturity | `metadata.RolloverBehavior` | On Maturity | Select: Auto-renew / Cash out | No | — |
| Institution | `workingMine.Institution` | Bank | Select from institutions | No | — |
| Monthly Interest | `workingMine.MonthlyIncome` | Monthly Interest | Currency **read-only** | Auto | Auto = Principal × Rate/100 / 12 |

> **Key UX:** `CurrentValue` and `MonthlyIncome` are locked/read-only — computed from Principal+Rate+Tenure. Show them as amber-badge "calculated" fields with a `ti-calculator` icon.

---

#### ASB / ASM
| Field | Bound To | Label | Input | Required | Helper |
|---|---|---|---|---|---|
| Fund | `metadata.FundCode` | Fund Name | Select: ASB / ASM 2 / ASM 3 Didik / ASM Merdeka | Yes | — |
| Units Held | `metadata.UnitsHeld` | Units Held | Number decimal step 0.001 | Yes | "From your ASB/ASM statement or MyASNB app." |
| Total Value | `workingMine.CurrentValue` | Total Value | Currency **read-only** | Auto | Auto = Units × RM 1.00. Locked. |
| Total Invested | `workingMine.PurchaseCost` | Total Amount Invested | Currency | No | "Net subscriptions you have paid in over the years." |
| Est. Monthly Dividend | `workingMine.MonthlyIncome` | Est. Monthly Dividend | Currency | No | "Annual dividend ÷ 12. Update each year after ASNB announcement." |

> **Key UX:** Price is always RM 1.00/unit. `CurrentValue` is locked and auto-fills. Show a helper: "ASB/ASM price is always RM 1.00 per unit — no market risk."

---

#### Unit Trust / Mutual Fund (General)
| Field | Bound To | Label | Input | Required | Helper |
|---|---|---|---|---|---|
| Fund Name | `metadata.FundCode` | Fund Name | Text | No | "e.g. Public Growth Fund, Kenanga Growth Fund" |
| Units Held | `metadata.UnitsHeld` | Units Held | Number decimal | No | — |
| NAV per Unit | `metadata.NavPerUnit` | Current NAV per Unit (RM) | Currency | No | "From fund manager's website or your statement." |
| Market Value | `workingMine.CurrentValue` | Current Market Value | Currency | Yes | "Auto-calculated if Units and NAV provided. Otherwise enter manually." |
| Total Cost | `workingMine.PurchaseCost` | Total Amount Invested | Currency | No | — |
| Monthly Distribution | `workingMine.MonthlyIncome` | Est. Monthly Distribution | Currency | No | — |
| Institution | `workingMine.Institution` | Fund Manager / Platform | Select from institutions | No | — |

> Auto-calculate `CurrentValue = UnitsHeld × NavPerUnit` when both are provided, but allow manual override.

---

#### Bursa Malaysia Stocks
| Field | Bound To | Label | Input | Required | Helper |
|---|---|---|---|---|---|
| Stock Code | `metadata.StockCode` | Stock Code | Text uppercase maxlen 8 | Yes | "e.g. MAYBANK, PBBANK, TENAGA" |
| Exchange | `metadata.Exchange` | Exchange | Hidden: "Bursa" | — | Auto-set |
| Shares Owned | `metadata.SharesOwned` | Shares Owned | Number integer | Yes | "Total shares (not lots). 1 lot = 100 shares." |
| Share Price | `metadata.CurrentSharePrice` | Current Share Price (RM) | Currency step 0.005 | Yes | "Last traded price from Bursa or your broker." |
| Market Value | `workingMine.CurrentValue` | Market Value | Currency **read-only** | Auto | Auto = Shares × Price |
| Avg Purchase Price | `metadata.PurchasePricePerShare` | Avg Purchase Price per Share | Currency | No | "Average cost per share including brokerage." |
| Total Cost | `workingMine.PurchaseCost` | Total Cost | Currency **read-only** | Auto | Auto = Shares × Avg Purchase Price |
| Dividend Yield | `metadata.DividendYieldPct` | Est. Dividend Yield (% p.a.) | Number step 0.01 | No | — |
| Monthly Income | `workingMine.MonthlyIncome` | Est. Monthly Dividend | Currency **read-only** | Auto | Auto = MarketValue × DivYield/100/12 |
| Institution | `workingMine.Institution` | Broker | Select from institutions | No | "e.g. Rakuten Trade, Maybank IB" |

---

#### US / Foreign Stocks
Same layout as Bursa Stocks with these differences:
| Difference | Value |
|---|---|
| Exchange | Select: NASDAQ / NYSE / Others (manual text) |
| Share Price label | Current Share Price (MYR equivalent) |
| Helper on price | "Convert from USD manually. e.g. USD × current rate." |
| Stock Code | Allow longer codes (maxlen 10) |

---

#### Residential Property
| Field | Bound To | Label | Input | Required | Helper |
|---|---|---|---|---|---|
| Property Type | `metadata.PropertyType` | Property Type | Select: Condo / Terraced / Semi-D / Apartment / Bungalow / Others | Yes | — |
| Address | `metadata.Address` | Property Address | Textarea rows 2 maxlen 300 | No | "Optional. For your reference." |
| Valuation | `workingMine.CurrentValue` | Current Valuation | Currency | Yes | "Use latest bank valuation or market estimate." |
| Ownership % | `metadata.OwnershipPct` | Your Ownership % | Number 0–100 default 100 | No | "If jointly owned, enter your share only." |
| Purchase Price | `workingMine.PurchaseCost` | Purchase Price | Currency | No | "Original purchase price paid." |
| Monthly Rental | `metadata.RentalIncomeMonthly` | Monthly Rental Income | Currency | No | "Gross rental received from tenant." |
| Monthly Maintenance | `metadata.MaintenanceCostMonthly` | Monthly Maintenance / Service Charge | Currency | No | — |
| Net Monthly Income | `workingMine.MonthlyIncome` | Net Monthly Income | Currency **read-only** | Auto | Auto = Rental − Maintenance. Editable override. |
| Linked Mortgage | `metadata.LinkedMortgageBurdenId` | Linked Mortgage | Dropdown from user's active Burdens | No | "Link to your mortgage burden for net equity view." |
| Institution | `workingMine.Institution` | Developer / Financier | Select from institutions | No | — |

---

#### Commercial Property
Same as Residential with different `PropertyType` options: Office / Shophouse / Factory / SOHO / Others.

---

#### Gold
| Field | Bound To | Label | Input | Required | Helper |
|---|---|---|---|---|---|
| Gold Form | `metadata.GoldForm` | Gold Form | Select: Physical Bar / Jewellery / Digital (GIA/GAP) | Yes | — |
| Weight | `metadata.WeightGrams` | Weight (grams) | Number decimal step 0.01 | Yes | "For jewellery, use gold content weight only." |
| Buyback Price | `metadata.BuybackPricePerGram` | Buyback Price per Gram (RM) | Currency | Yes | "From your bank's (e.g. Public Bank, BSN) or dealer's current buyback rate." |
| Est. Value | `workingMine.CurrentValue` | Estimated Value | Currency **read-only** | Auto | Auto = Weight × Buyback Price |
| Total Cost | `workingMine.PurchaseCost` | Total Cost Paid | Currency | No | "What you paid in total, including premium." |
| Institution | `workingMine.Institution` | Bank / Dealer | Select from institutions | No | — |

---

#### Silver
Same as Gold but label says "Silver" and no Digital form option.

---

#### Cryptocurrency
| Field | Bound To | Label | Input | Required | Helper |
|---|---|---|---|---|---|
| Coin | `metadata.CoinSymbol` | Coin Symbol | Text uppercase maxlen 10 | Yes | "e.g. BTC, ETH, BNB, SOL" |
| Coins Held | `metadata.CoinsHeld` | Amount / Coins Held | Number decimal 8dp | Yes | "From your exchange dashboard or wallet." |
| Value (MYR) | `workingMine.CurrentValue` | Current Value (MYR) | Currency | Yes | "Convert to MYR manually. Use current exchange rate from Binance, Luno, etc." |
| Cost Basis | `workingMine.PurchaseCost` | Total Cost Paid (MYR) | Currency | No | "What you paid in total in MYR." |
| Price per Coin | `metadata.PurchasePricePerCoin` | Avg Purchase Price per Coin (MYR) | Currency | No | — |
| Exchange / Wallet | `metadata.WalletOrExchange` | Exchange / Wallet | Text maxlen 100 | No | "e.g. Luno, Binance, Ledger hardware wallet" |
| Monthly Staking | `workingMine.MonthlyIncome` | Monthly Staking / Yield | Currency | No | "If you earn staking rewards, enter monthly MYR value." |

---

#### Others (Catch-all)
Show the original generic form:
- Mine Name · Institution (optional) · Current Value (required) · Purchase Cost (optional) · Monthly Income (optional) · Holdings free-text (optional).

---

### Task 2.4 — Build Step 3: Review & Confirm Panel

**Inside `AddMineModal.razor` — Step 3:**

A read-only summary before saving. Show:
- Mine name + type icon + category badge + type badge
- **Primary value** with type-correct label (from `FormatHelper.CurrentValueLabel`)
- Growth (if PurchaseCost provided and > 0)
- Monthly income (if > 0)
- Key type-specific highlights (2–3 most important metadata fields)
- For FD: Principal, Rate, Maturity Date
- For ASB: Units, Fund name
- For Stocks: Code, Shares, Market Price
- For Property: Type, Address snippet, Ownership %
- For Gold: Weight, Form, Buyback Price
- For Crypto: Coin, Coins Held, Exchange

Footer: **"← Back"** and **"Save Mine"** (or **"Update Mine"** in edit mode).

---

### Task 2.5 — Step Navigation, Validation & Auto-Calculation Logic

**Inside `AddMineModal.razor` — `@code` block:**

**Validation per step:**
- Step 1 → 2: Category selected AND Type selected. Always valid since defaults are set.
- Step 2 → 3: Per-type validation (see table below).
- Step 3: Call `SaveMine()` or `UpdateMine()`.

**Auto-calculation rules (live on `oninput`):**

| Type | Auto-calculates |
|---|---|
| FixedDeposit | `CurrentValue = Principal × (1 + Rate/100 × Tenure/12)`, `MonthlyIncome = Principal × Rate/100 / 12`, `MaturityDate = today + TenureMonths` |
| UnitTrustAsb | `CurrentValue = UnitsHeld × 1.00` |
| UnitTrustGeneral | `CurrentValue = UnitsHeld × NavPerUnit` (if both filled) |
| Stocks / StocksUs | `CurrentValue = SharesOwned × CurrentSharePrice`, `PurchaseCost = SharesOwned × PurchasePricePerShare`, `MonthlyIncome = CurrentValue × DividendYieldPct/100/12` |
| PropertyResidential/Commercial | `MonthlyIncome = RentalIncomeMonthly − MaintenanceCostMonthly` |
| Gold / Silver | `CurrentValue = WeightGrams × BuybackPricePerGram` |

**Required field validation per type:**

| Type | Required Fields |
|---|---|
| EpfKwsp | `CurrentValue > 0` |
| SavingsAccount | `CurrentValue > 0` |
| CashInHand | `CurrentValue > 0` |
| FixedDeposit | `Principal > 0`, `InterestRatePct > 0`, `TenureMonths > 0` |
| UnitTrustAsb | `UnitsHeld > 0`, `FundCode` selected |
| UnitTrustGeneral | `FundCode` not empty, `UnitsHeld > 0` |
| Stocks / StocksUs | `StockCode` not empty, `SharesOwned > 0` |
| PropertyResidential/Commercial | `Address` not empty, `PropertyType` selected |
| Gold / Silver | `GoldForm` selected, `WeightGrams > 0`, `PurchaseCost > 0` |
| Cryptocurrency | `CoinSymbol` not empty, `CoinsHeld > 0` |
| Others | `CurrentValue > 0` |

> **Tracking-first override:** Current market valuations, current share prices, buyback rates, dividend yields, staking yields, and growth data are optional. When these values are omitted, the mine is still saved for tracking and growth/income remains unavailable until the values are provided later. Purchase cost and placement/baseline details remain available as the tracking basis.

---

## Phase 3 — Service Layer & Persistence

### Task 3.1 — Update `DatabaseMineService.AddMineAsync` for Metadata

**File:** `OMM.Public/Services/DatabaseMineService.cs`

1. Accept `MineMetadata?` from `Mine.Metadata`.
2. Serialise: `JsonSerializer.Serialize(mine.Metadata, new JsonSerializerOptions { DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull })`.
3. Store in `MineEntity.MetadataJson`.
4. Update `MapMine()` to deserialise: `entity.MetadataJson is null ? null : JsonSerializer.Deserialize<MineMetadata>(entity.MetadataJson)`.

---

### Task 3.2 — Add `UpdateMineAsync` to `DatabaseMineService`

Check if `UpdateMineAsync` exists. If not, add it:

```csharp
public async Task<bool> UpdateMineAsync(Mine mine, bool archiveRateToHistory = false, CancellationToken cancellationToken = default)
```

Logic mirrors `AddMineAsync` but:
- Fetch the existing `MineEntity` by Id (including `MetadataJson`).
- **Rate/Yield History Archival:** If `archiveRateToHistory` is requested (or if rate changed and user checked "Keep previous in history"), snapshot the prior rate/balance into `MineYieldHistoryEntity` before updating:
  - Extract previous rate from prior metadata or `MineEntity`.
  - Add row to `MineYieldHistories` with `EffectiveYear = DateTime.Today.Year - 1` (or current), prior `RatePct`, prior `CurrentValue`.
- Update all mutable fields (`Name`, `Category`, `Type`, `InstitutionId`, `CurrencyId`, `CurrentValue`, `PurchaseCost`, `Growth`, `GrowthPct`, `MonthlyIncome`, `Holdings`, `MetadataJson`, `Status`, `UpdatedOn`).
- SaveChanges. Return true/false.

---

### Task 3.3 — Add Delete UI to Mine Cards

**File:** `OMM.Public/Components/Pages/Mines.razor`

Add a `Delete` button (three-dot overflow menu or a small `ti-trash` icon) on each Mine card. On click: show an inline confirmation prompt (not a full modal — a Bootstrap popover or inline `are-you-sure` div). On confirm: call `MineService.DeleteMineAsync(mine.Id)` → reload mines.

The service method `SoftDeleteMineAsync` already exists via `IMineRepository`.

---

### Task 3.4 — Upgrade Tools Integration: "Save as Mine to Track" in Dividend & FD Calculators

**Why:** Connect the standalone calculation tools directly to the Mine portfolio with rich, typed metadata and history tracing.

**File 1:** `OMM.Public/Components/Shared/Tools/CalculatorDividend.razor`

Upgrade the existing `SaveToMinesAsync()` (line 1002):
1. **Populate typed `MineMetadata`**:
   ```csharp
   newMine.Metadata = new MineMetadata
   {
       StockCode = !string.IsNullOrWhiteSpace(qcStockSymbol) ? qcStockSymbol.Trim().ToUpperInvariant() : null,
       Exchange = "Bursa",
       SharesOwned = (int)qcShares,
       CurrentSharePrice = qcSharePrice,
       PurchasePricePerShare = qcPurchasePrice > 0 ? qcPurchasePrice : qcSharePrice,
       DividendYieldPct = CurrentQuickCalc.DividendYieldPct
   };
   ```
2. **Initial Yield History Snapshot**:
   If `CurrentQuickCalc.AnnualDividend > 0` and `CurrentQuickCalc.DividendYieldPct > 0`, also call a helper to record the initial yield entry in `mine_yield_history` (`EffectiveYear = DateTime.Today.Year`, `RatePct = CurrentQuickCalc.DividendYieldPct`, `DeclaredAmount = CurrentQuickCalc.AnnualDividend`).
3. **Interactive Toast UX**:
   Replace the simple fading badge with a clean toast:
   > *"Saved **[StockName]** as a Mine! [View Mine →]"* (clicking navigates directly to `/mines` or `/mines/{id}`).

**File 2:** `OMM.Public/Components/Shared/Tools/CalculatorFd.razor`

Add a `"Next Actions"` section with a **"Save as Mine to Track"** button (matching the Dividend Calculator pattern):
1. On click: creates a `Mine` of `MineCategory.CashAndDeposits` and `MineType.FixedDeposit`.
2. Populates `MineMetadata`:
   ```csharp
   newMine.Metadata = new MineMetadata
   {
       Principal = fdPrincipal,
       InterestRatePct = fdRate,
       TenureMonths = fdTenureMonths,
       MaturityDate = DateOnly.FromDateTime(DateTime.Today.AddMonths(fdTenureMonths)),
       RolloverBehavior = "auto-renew"
   };
   ```
3. Auto-computes `CurrentValue = FdMaturityValue`, `MonthlyIncome = FdMonthlyEquivalent`, `PurchaseCost = fdPrincipal`.
4. Saves via `MineService.AddMineAsync(newMine)` and shows interactive toast with link to Mines.

---

## Phase 4 — Mine Cards & Detail Page

### Task 4.1 — Type-Aware Value Label on Mine Cards

**File:** `OMM.Public/Components/Pages/Mines.razor` (card section, line ~95)

Replace the hardcoded label `"Current"` with:
```razor
@FormatHelper.CurrentValueLabel(mine.Type)
```

---

### Task 4.2 — Update `MineDetail.razor` with Metadata Display

**File:** `OMM.Public/Components/Pages/MineDetail.razor`

1. Deserialise `mine.Metadata` from the loaded Mine.
2. Below the core metrics (CurrentValue / Growth / MonthlyIncome), render a contextual **Details** card per type:

| Type | Show |
|---|---|
| FixedDeposit | Principal, Rate, Tenure, Maturity Date, Days to Maturity countdown, Rollover Behavior |
| UnitTrustAsb | Fund, Units Held, Price/unit (RM 1.00) |
| UnitTrustGeneral | Fund, Units, NAV/unit |
| Stocks / StocksUs | Code, Exchange, Shares, Last Price, Avg Buy Price, Unrealised P&L |
| PropertyResidential/Commercial | Address, Type, Ownership %, Rental, Maintenance, Net Yield %, linked Burden summary |
| Gold / Silver | Form, Weight, Buyback Price, Value formula |
| Cryptocurrency | Coin, Coins Held, Exchange/Wallet, Cost Basis, Unrealised P&L |

3. Add an **Income History** section (filterable list of `IncomeRecord` rows where `MineId == this mine`). Show: Date · Source · Amount · Frequency.
4. Add **"Log Income Event"** button that opens the Income modal pre-linked to this Mine.
5. Add **"Edit Mine"** button that opens `AddMineModal` with `ExistingMine="mine"`.

---

### Task 4.3 — Add Yield & Rate History Timeline & Growth Tracking to `MineDetail.razor`

**Why:** Enables tracing year-on-year rate growth (e.g. EPF: 2024 at 5.4% → 2025 at 6.0% = +0.60% growth, FD renewal rate progression, stock dividend yield shifts).

**File:** `OMM.Public/Components/Pages/MineDetail.razor`

For yield-bearing mine types (`EpfKwsp`, `UnitTrustAsb`, `FixedDeposit`, `Stocks`, `StocksUs`, `Reit`):
1. **Load `MineYieldHistory` records** for `this.MineId` ordered by `EffectiveYear DESC, RecordDate DESC`.
2. **Growth Progression Badge:**
   - If >= 2 historical records exist, compute YoY rate difference:
     $$\Delta = \text{Rate}_{\text{current}} - \text{Rate}_{\text{previous}}$$
   - Display a visual trend badge: e.g. `<span class="badge bg-success-subtle text-success"><i class="ti ti-trending-up"></i> +0.60% YoY (+11.1% growth)</span>`.
3. **Timeline / History Table:**
   - Columns: Year / Date · Declared Rate (% p.a.) · Total Declared Dividend (MYR) · Balance at Declaration · Notes.
4. **Quick Action: "+ Log Rate Declaration":**
   - Lightweight modal or inline drawer:
     - Year (e.g. 2025)
     - Declared Rate % (e.g. 6.00%)
     - Declared Amount (optional, e.g. RM 2,500)
     - Notes (optional, e.g. "Declared KWSP Simpanan Konvensional")
     - Checkbox: "Also create matching cash income record in Freedom Income log" (checked by default).
   - Instant save without having to navigate away or redo the whole mine form.

---

## Implementation Order Summary & Status

```
Task 1.1  [DONE] Expand enums + add MineMetadata POCO to Mine.cs
Task 1.2  [DONE] Add MetadataJson column to MineEntity + EF migration (applied to DB)
Task 1.3  [DONE] Update FormatHelper (labels, icons, type mappings, CurrentValueLabel, MineTypeToIcon)
Task 1.4  [DONE] Add MineYieldHistoryEntity + EF migration (applied to DB)
Task 2.1  [DONE] Extract AddMineModal component from Mines.razor (Add + Edit mode)
Task 2.2  [DONE] Build Step 1: visual category card grid + type pill picker
Task 2.3  [DONE] Build Step 2: type-specific adaptive field sets (10 Malaysian asset classes)
Task 2.4  [DONE] Build Step 3: review & confirm panel + optional yield history archiving
Task 2.5  [DONE] Step navigation + per-type validation + auto-calculation logic
Task 3.1  [DONE] Update DatabaseMineService.AddMineAsync for metadata serialisation
Task 3.2  [DONE] Add DatabaseMineService.UpdateMineAsync & RecordYieldHistoryAsync
Task 3.3  [DONE] Add Delete UI with confirmation & Edit Mine actions on Mine cards
Task 3.4  [DONE] Upgrade Tools Integration: "Save as Mine to Track" in Dividend Calculator (Quick + Advanced) and FD Calculator
Task 4.1  [DONE] Type-aware value labels & metadata attributes on Mine cards
Task 4.2  [DONE] Update MineDetail.razor: metadata specifications panel + Edit Mine modal integration
Task 4.3  [DONE] Add Yield & Rate History Timeline & Growth Tracking to MineDetail.razor
```

> **Note on Phase 2 (future):** Tabung Haji type, `IncomeEventType` enum on `IncomeRecordEntity`, and portfolio value snapshots (`MineValueSnapshotEntity`) are deferred but the architecture above is designed to accommodate them without migrations.
