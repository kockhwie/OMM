# Decision Pathway — Phase 2 Handoff
**Date:** 2026-10-09  
**Repository:** `C:\Users\User\source\repos\OMMv2`  
**Status:** Phase 1 complete and building cleanly (`0 Warning(s)`, `0 Error(s)`). Ready for Phase 2.

---

## 1. Background & Product Vision

OMM (OhMyMine) is a Malaysian personal-finance / investment portfolio tracker built with **Blazor (.NET 8, SSR + Interactive Server)**, **PostgreSQL**, and **ASP.NET Identity**. There are two separate web projects:

| Project | Purpose | Identity Schema |
|---|---|---|
| `OMM.Admin` | Staff admin panel | `admin` PostgreSQL schema |
| `OMM.Public` | Member-facing app | `public` PostgreSQL schema |
| `OMM.Shared` | Shared library (services, models, DI) | — |

The **Decision Pathway** lives entirely in `OMM.Public`. It is a disciplined, 8-step investment decision support system:

```
Signal → Member Concern → Scope → Objective → Impact Map → Options → Scenario Branches → Monitoring Checkpoint
```

All financial calculations are **100% deterministic inside `OMM.Shared`**. Google AI is used **only** to classify free-text queries into structured `InquiryIntentResult` objects.

---

## 2. What Has Been Built (Phase 1 — Complete)

### 2.1 File Inventory

| File | Status | Purpose |
|---|---|---|
| `OMM.Shared/DecisionSupport/GoogleAiOptions.cs` | Committed | Config options (`Enabled`, `ApiKey`, `Models[]`, `TimeoutSeconds`) |
| `OMM.Shared/DecisionSupport/InquiryIntent.cs` | Committed | `IInquiryIntentInterpreter` interface + `InquiryIntentResult` record |
| `OMM.Shared/DecisionSupport/GoogleAiIntentInterpreter.cs` | Modified (uncommitted) | Multi-model fallback HTTP client; Protobuf-compatible JSON schema; generic-command short-circuit |
| `OMM.Shared/DecisionSupport/DecisionPathwayModels.cs` | New (uncommitted) | All domain records: `SignalTemplate`, `MemberConcernOption`, `ScopeOption`, `ObjectiveOption`, `ImpactMapResult`, `ActionOption`, `ScenarioBranch`, `ScenarioCalculationInput`, `MonitoringCheckpoint`, `FullDecisionPathwayResult` |
| `OMM.Shared/DecisionSupport/DeterministicDecisionEngine.cs` | New (uncommitted) | `IDeterministicDecisionEngine` + full implementation: curated signals, concerns, scopes, objectives, impact maps, action evaluation, 3-branch scenario math, checkpoint generation |
| `OMM.Shared/DecisionSupport/DecisionSupportServiceCollectionExtensions.cs` | Modified (uncommitted) | `services.AddSharedDecisionSupport(config)` — called from both `OMM.Public/Program.cs` and `OMM.Admin/Program.cs` |
| `OMM.Public/Components/Pages/DecisionPathway.razor` | Modified (uncommitted) | Full 8-step interactive UI |
| `OMM.Public/Components/Pages/DecisionPathway.razor.css` | Modified (uncommitted) | Scoped CSS for the page |

### 2.2 Google AI Interpreter

**File:** `OMM.Shared/DecisionSupport/GoogleAiIntentInterpreter.cs`

Configured via `appsettings.json` section `"GoogleAi"`:
```json
"GoogleAi": {
  "Enabled": true,
  "ApiKey": "",
  "Models": ["gemma-4-31b-it", "gemini-flash-lite-latest", "gemini-2.5-flash"],
  "BaseUrl": "https://generativelanguage.googleapis.com/v1beta",
  "TimeoutSeconds": 20
}
```

- API key is in **User Secrets** — never commit it.
- Fallback order: `gemma-4-31b-it` → `gemini-flash-lite-latest` → `gemini-2.5-flash`. First success wins.
- `gemini-2.5-flash-lite` returns HTTP 404 — **do not add it**.
- Generic-command short-circuit: single-word inputs (`"proceed"`, `"help"`, `"ok"`, `"next"`, etc.) are intercepted locally (no network call) returning `IsAvailable: true` with `SignalType: "unknown"` and a concrete clarifying question.
- Schema: use Protobuf-enum `type` values (`"string"`, `"object"`, `"array"`, `"number"`), `nullable: true` for optional fields — never `type: ["string","null"]`.

### 2.3 Deterministic Decision Engine

**File:** `OMM.Shared/DecisionSupport/DeterministicDecisionEngine.cs`

**Curated signals (12 total):**
- Individual Stock: Maybank (1155), Tenaga Nasional (5347), Inari Amertron (0166)
- Sector: Banking NIM, CPO Plantation
- Malaysian Market: BNM Rate decision, MYR FX movement
- Global Economy: US Fed rate, US tariffs
- Portfolio: Concentration risk, Cash drag
- Opportunity: Dividend stalwart pullback, Market overreaction

**Concerns:** `price_drop`, `dividend`, `macro_shock`, `concentration`, `opportunity`  
**Scopes:** `individual_stock`, `sector`, `malaysian_market`, `global_economy`, `portfolio`  
**Objectives:** `capital_preservation`, `income`, `balanced`, `opportunity`, `risk_reduction`, `growth`, `patient_monitor`

**Scenario branches (always 3):**
- **Bull:** +10% price, +5% dividend
- **Base:** 0% price, unchanged dividend
- **Bear:** -12% price (+ optional `StressAdjustmentPercent`), -10% dividend

**Action options:** Hold, Trim, Sell, Accumulate, Set Checkpoint (with pros/cons and suitability rationale).

**Checkpoint:** Returns a `MonitoringCheckpoint` keyed on action (`sell`, `trim`, `accumulate`, or default hold).

### 2.4 UI — DecisionPathway.razor

Route: `/decision-pathway`  
Injects: `IInquiryIntentInterpreter`, `IDeterministicDecisionEngine`

**8 screens** (`int screen` state variable):

1. **Signal** — Free-text AI intake + curated signal card grid (filterable by `SignalCategory`)
2. **Concern** — 5 concern option cards
3. **Scope** — 5 scope option cards
4. **Objective** — 5 objective option cards
5. **Impact Map** — 4-node transmission chain + vulnerability / resilience lists
6. **Options** — Action evaluation cards (Hold / Trim / Sell / Accumulate / Checkpoint)
7. **Scenarios** — 3 deterministic scenario branch cards + "What If" fork buttons
8. **Checkpoint** — `MonitoringCheckpoint` display + save button (placeholder — not yet persisted)

**AI result card — 3 visual states:**
- `is-ready` (green border): `signalType != "unknown"` AND `confidence >= 0.4m` → tags + 5 concern chips + "Proceed" button
- `is-vague` (orange border): `signalType == "unknown"` OR `confidence < 0.4m` → "More detail needed" badge + 5 chips front-and-centre
- `is-unavailable` (amber border): `!intentResult.IsAvailable` → error message

---

## 3. What is NOT Yet Built (Phase 2 Priorities)

### Priority 1 — Persist the Completed Pathway

When the user completes Step 8, save a pathway snapshot to PostgreSQL.

**Tasks:**
1. Add EF entity `DecisionPathwayRecord` to `OMM.Public/Data/ApplicationDbContext.cs`:
   - `Id` (Guid PK), `UserId` (string nullable — store as text, NO FK constraint; see AGENTS.md), `CreatedAt` (DateTimeOffset)
   - `SignalId`, `SignalTitle`, `ConcernKey`, `ScopeKey`, `ObjectiveKey`, `SelectedActionKey` (all string)
   - `CurrentSharePrice`, `AnnualDividendPerShare` (decimal)
   - `HorizonLabel`, `CheckpointTitle`, `CheckpointTriggerCondition` (string)
   - `Notes` (string, nullable)
2. Add EF Core migration.
3. Add `IDecisionPathwayService` interface + implementation in `OMM.Shared/DecisionSupport/`:
   - Method: `Task<Guid> SavePathwayAsync(DecisionPathwayRecord record, CancellationToken ct = default)`
   - Register via `AddSharedDecisionSupport()`.
4. In `DecisionPathway.razor` Screen 8, wire "Save Checkpoint" button:
   - Gather current state into `DecisionPathwayRecord`.
   - Get `UserId` from injected `AuthenticationState`.
   - Call service, show inline success confirmation.
5. Add `/decision-history` page listing past pathways (paginated, newest first).

### Priority 2 — Pre-fill Share Price & Dividend from Portfolio

When AI identifies a known security, look up the member's actual holding to pre-fill scenario inputs.

**Tasks:**
1. Inject the existing portfolio/mine data service into `DecisionPathway.razor`.
2. On successful AI interpretation with non-empty `MentionedSecurity`, query member holding and pre-fill `sharePrice` / `dividendPerShare` state variables.
3. Show subtle "Pre-filled from your Maybank holding" hint near the inputs on Screen 7.

### Priority 3 — Monitoring Alert Dispatch

Schedule a background check at the checkpoint `ReviewHorizon` date and email/notify the member.

**Tasks:**
1. Add a hosted service in `OMM.Shared` using `IHostedService` or Hangfire.
2. Query saved checkpoints where `ReviewDate <= now` and `IsTriggered = false`.
3. Dispatch email via existing `IEmailSender` in `OMM.Shared`.

### Priority 4 — UX Polish

- Step progress breadcrumb at content panel top.
- CSS animated transitions between steps.
- Responsive sidebar (horizontal scroll on mobile).
- Print / export button on Screen 8.

---

## 4. Architecture Constraints (READ BEFORE TOUCHING CODE)

From `AGENTS.md` at the repository root — these are **non-negotiable**:

1. **Service placement:** Reusable services go in `OMM.Shared`. Register via `AddSharedDecisionSupport(config)`.
2. **Audit FKs:** Never create a DB FK constraint from audit columns to `AspNetUsers` on shared/master-data tables. Store as nullable `text`.
3. **Blazor SSR boundary:** Never put `@rendermode` on a component that receives a `ChildContent` RenderFragment.
4. **No automatic `dotnet test`:** Ask the user before running tests.
5. **No automatic `git commit` / `git push`:** Present changes first.
6. **Never print the Google AI API key.**
7. **Never send portfolio data, user IDs, or emails to Google AI.**
8. **No Tailwind CSS.** Vanilla CSS + Bootstrap 5.3.x + Tabler Icons only (no emoji icons).
9. **Forms:** Follow `docs/ui-form-standards.md` for any new Public forms.

---

## 5. Technology Reference

| Layer | Technology |
|---|---|
| Framework | .NET 8, Blazor (SSR + Interactive Server) |
| Database | PostgreSQL via EF Core |
| CSS | Vanilla CSS + Bootstrap 5.3.x |
| Icons | Tabler Icons (CDN in `App.razor`) |
| AI | Google Generative Language REST API v1beta |
| Auth | ASP.NET Identity |
| Solution file | `OMMv2.slnx` |

---

## 6. Key File Paths Quick Reference

```
OMM.Shared/DecisionSupport/
  GoogleAiOptions.cs                              ← AI config options
  InquiryIntent.cs                                ← interface + InquiryIntentResult record
  GoogleAiIntentInterpreter.cs                    ← HTTP fallback interpreter
  DecisionPathwayModels.cs                        ← ALL domain records and enums
  DeterministicDecisionEngine.cs                  ← IDeterministicDecisionEngine + implementation
  DecisionSupportServiceCollectionExtensions.cs   ← DI registration

OMM.Public/Components/Pages/
  DecisionPathway.razor                           ← 8-screen Blazor page
  DecisionPathway.razor.css                       ← scoped CSS

OMM.Public/Program.cs                            ← calls AddSharedDecisionSupport(config)
OMM.Admin/Program.cs                             ← also calls AddSharedDecisionSupport(config)
docs/ui-form-standards.md                        ← required for new forms
```

---

## 7. Running the App Locally

```powershell
# Stop any running process (avoids file lock on rebuild)
Get-Process | Where-Object { $_.ProcessName -like "*OMM*" } | Stop-Process -Force

# Build
dotnet build OMM.Public/OMM.Public.csproj

# Run (User Secrets supplies the Google AI API key)
dotnet run --project OMM.Public/OMM.Public.csproj --launch-profile "http"
```

Navigate to `http://localhost:5128/decision-pathway`.

---

## 8. Uncommitted Files

Commit these as one logical changeset **before starting Phase 2**:

```
 M OMM.Public/Components/Pages/DecisionPathway.razor
 M OMM.Public/Components/Pages/DecisionPathway.razor.css
 M OMM.Shared/DecisionSupport/DecisionSupportServiceCollectionExtensions.cs
 M OMM.Shared/DecisionSupport/GoogleAiIntentInterpreter.cs
 M docs/handoffs/2026-10-09-decision-pathway-ai-interpreter-handoff.md
?? OMM.Shared/DecisionSupport/DecisionPathwayModels.cs
?? OMM.Shared/DecisionSupport/DeterministicDecisionEngine.cs
?? docs/handoffs/2026-10-09-decision-pathway-phase2-handoff.md
```

Suggested commit message:
```
feat: Decision Pathway Phase 1 — 8-step deterministic UI, AI classifier, 3-state result card
```

---

## 9. Ready-to-Paste Agent Prompt

Copy everything between the dashed lines and paste it as your opening prompt to the next agent:

```
---AGENT PROMPT START---

Repository: C:\Users\User\source\repos\OMMv2

You are taking over Phase 2 of the OMM Decision Pathway feature.

FIRST, read these files in full before writing any code:
1. AGENTS.md  (repo root — mandatory architecture rules)
2. docs/handoffs/2026-10-09-decision-pathway-phase2-handoff.md  (this handoff — all context)
3. docs/ui-form-standards.md  (required before building any new Public forms)

WHAT HAS ALREADY BEEN BUILT (do not re-implement):
- 8-step Blazor page:   OMM.Public/Components/Pages/DecisionPathway.razor
- Deterministic engine: OMM.Shared/DecisionSupport/DeterministicDecisionEngine.cs
- Domain models:        OMM.Shared/DecisionSupport/DecisionPathwayModels.cs
- Google AI interpreter with multi-model fallback and generic-command short-circuit:
                        OMM.Shared/DecisionSupport/GoogleAiIntentInterpreter.cs
- DI extension:         OMM.Shared/DecisionSupport/DecisionSupportServiceCollectionExtensions.cs

YOUR TASK — Priority 1 (start here):
Persist a completed Decision Pathway to PostgreSQL so members can review their past decisions.

Specifically:
1. Add EF entity `DecisionPathwayRecord` to OMM.Public/Data/ApplicationDbContext.cs:
   - Id (Guid PK), UserId (string nullable — stored as TEXT, no DB FK constraint per AGENTS.md)
   - CreatedAt (DateTimeOffset)
   - SignalId, SignalTitle, ConcernKey, ScopeKey, ObjectiveKey, SelectedActionKey (all string)
   - CurrentSharePrice, AnnualDividendPerShare (decimal)
   - HorizonLabel, CheckpointTitle, CheckpointTriggerCondition (string)
   - Notes (string nullable)

2. Add an EF Core migration (ask me before running it).

3. Add IDecisionPathwayService + implementation in OMM.Shared/DecisionSupport/:
   - Method: Task<Guid> SavePathwayAsync(DecisionPathwayRecord record, CancellationToken ct)
   - Register in AddSharedDecisionSupport() in DecisionSupportServiceCollectionExtensions.cs.

4. In DecisionPathway.razor Screen 8, wire the "Save Checkpoint" button:
   - Collect all screen state into a DecisionPathwayRecord.
   - Inject CascadingAuthenticationState and read UserId from ClaimsPrincipal.
   - Call IDecisionPathwayService.SavePathwayAsync(...).
   - Show a success inline confirmation.

5. Add /decision-history Blazor page listing saved pathways (newest first, paginated).

CONSTRAINTS (AGENTS.md — no exceptions):
- Do NOT run dotnet test automatically. Ask permission.
- Do NOT run git commit or git push. Present changes for review first.
- Never send user IDs, portfolio data, or emails to Google AI.
- Never commit an API key.
- Bootstrap 5.3.x + Tabler Icons only. No Tailwind. No emoji icons.
- All reusable services in OMM.Shared. Register via AddSharedDecisionSupport().
- UserId on DecisionPathwayRecord = nullable text column, no FK constraint.

After Priority 1 is done, check with the user before starting Priority 2
(pre-fill share price from the member's existing portfolio holding).

---AGENT PROMPT END---
```
