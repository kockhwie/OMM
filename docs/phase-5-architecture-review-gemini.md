# Phase 5 Architecture Review

> **Reviewer**: Principal Architect  
> **Document**: [phase-5-production-system-prompts-and-revised-architecture.md](file:///c:/Users/User/source/repos/OMMv2/docs/phase-5-production-system-prompts-and-revised-architecture.md)  
> **Date**: 2026-10-28  
> **Codebase state**: No `SubscriptionPlan`, `MemberSubscription`, `AiPromptVersion`, `IPromptProvider`, or emergency-prompt code exists yet. Current [GoogleAiIntentInterpreter.cs](file:///c:/Users/User/source/repos/OMMv2/OMM.Shared/DecisionSupport/GoogleAiIntentInterpreter.cs) uses a hardcoded system prompt with no tier awareness. [DeterministicDecisionEngine.cs](file:///c:/Users/User/source/repos/OMMv2/OMM.Shared/DecisionSupport/DeterministicDecisionEngine.cs) has static C# data with no Admin-configurable rules.

---

## Overall Verdict

### **Ready for foundation implementation only**

The document is well-structured and internally consistent on subscription/prompt/fallback architecture. However, it bundles ~4 independent workstreams (subscription infra, prompt management, inquiry/impact-analysis pipeline, Admin taxonomy/rule engine) into one monolithic phase. The inquiry pipeline (§10–§14) and Admin rule engine (§13) are design-level descriptions, not implementation-ready specifications. The compliance posture (§4.2 Pro) lacks a concrete product-scope definition. Foundation items (subscription, prompt versioning, effective-tier, fallback, simulator shell) can proceed. The inquiry/impact pipeline and Admin rule engine need further specification.

---

## A. Critical Blockers Before Implementation

| # | Section | Severity | Issue | Correction | Blocks |
|---|---------|----------|-------|------------|--------|
| A1 | §4.2 L153–165 | **BLOCKER** | Pro mode lists advisory fields (action, conviction, allocation, price boundaries) but does not define the **product scope** — which CMSL licence category, which specific advisory fields are approved, which securities are in-scope, and what disclosures are legally required. Without this, server-side validation rules (L155–164) cannot be implemented. | Before coding Pro validation: obtain written compliance sign-off defining the exact licence category (e.g., CMSL for investment advice under Schedule 2 Part 1 of CMSA 2007), approved output fields, in-scope security types, and mandatory disclosure text. Ref: [SC Malaysia — Licensing Handbook](https://www.sc.com.my/regulation/licensing). | **Prevents implementation of Pro validation and Pro prompt content.** |
| A2 | §3.6 L129–131 | **BLOCKER** | Migration ownership is stated as a requirement but not decided. The document says "Identify the single migration-owning DbContext/project" but does not specify which project owns `SubscriptionPlan`, `MemberSubscription`, `AiPromptVersion`, `AiPromptAuditEvent`, and all §15 analytics tables. Both `OMM.Admin` and `OMM.Public` have separate `ApplicationDbContext` instances over the same PostgreSQL `public` schema. Per AGENTS.md rules, `OMM.Shared` must not create competing migrations. | **Decision required**: Nominate `OMM.Public` as migration owner for all new `public` schema tables (subscription, prompt, inquiry analytics). `OMM.Admin` reads/writes via its existing `MasterDataDbContext` or a new shared-schema context that does **not** produce migrations. Document this in §3.6. | **Prevents any data-model implementation.** |
| A3 | §10–§14 (entire inquiry pipeline) | **BLOCKER** | The inquiry classification (§10.1), multi-purpose selector (§10.2), four-stage UX (§10.3), global-to-Malaysia impact map (§10.4), scenario planning (§10.5), risk context (§11), alerts (§12), Admin taxonomy (§13), and safety handling (§14) are described at design level. There are no JSON schemas, API contracts, DTO definitions, state-machine specifications, or deterministic-engine interface changes. The current `IDeterministicDecisionEngine` has 5 methods with hardcoded static data — it does not accept Admin-configurable rules, entity relationships, or impact graphs. | Split §10–§14 into a separate **Phase 5B specification document** with: (1) inquiry pipeline DTOs and state machine, (2) `IDeterministicDecisionEngine` interface changes, (3) Admin taxonomy entity model, (4) impact-graph data model, (5) JSON schemas for AI classification response. Implement Phase 5A (subscription + prompt + effective-tier + simulator shell) first. | **Prevents implementation of inquiry pipeline. Does not block subscription/prompt foundation.** |

---

## B. Architectural Contradictions or Missing Decisions

| # | Section | Severity | Issue | Correction | Blocks |
|---|---------|----------|-------|------------|--------|
| B1 | §3.3 L94 | **HIGH** | `AiPromptVersion.Tier` is `text` constrained to `STANDARD` or `PRO`. But `SubscriptionPlan.Code` (§3.1 L59) is also `text` with the same values. The document says "Do not store both SubscriptionTier and a separate plan code" (L85) but introduces `Tier` on `AiPromptVersion` as a separate unlinked text column. | Add a FK from `AiPromptVersion.Tier` to `SubscriptionPlan.Code` or use a shared `TierCode` enum type. Alternatively, accept the text column but add a CHECK constraint and document that `Tier` values must match `SubscriptionPlan.Code` values. | No — implementation choice. |
| B2 | §5 L184 | **HIGH** | `IPromptProvider` is placed in `OMM.Shared`. Its implementation needs database access (`GetActivePromptAsync`). `OMM.Shared` does not own a DbContext. The document does not specify where the EF implementation class lives. | Specify: `IPromptProvider` interface in `OMM.Shared`. `EfPromptProvider` implementation in `OMM.Public` (if Public owns migrations) or a shared infrastructure project. `OMM.Admin` uses the same interface but may need its own implementation or a direct DbContext query for the simulator. | No — but must decide before coding. |
| B3 | §5 L198 | **MEDIUM** | Cross-process cache invalidation via PostgreSQL `LISTEN/NOTIFY` is mentioned as an option, but the current codebase has no `LISTEN/NOTIFY` infrastructure. This is non-trivial (requires a persistent connection, a background listener service, and reconnection logic). | For Phase 5A, use database revision-token polling (simple `SELECT MAX(ActivatedAt)` with short TTL cache) as the initial strategy. Reserve `LISTEN/NOTIFY` for Phase 5B optimization if polling latency is unacceptable. | No. |
| B4 | §8 L273 | **HIGH** | "The Public pathway UI should be shared through a reusable component/Razor Class Library or clearly separated shared UI layer." The current [DecisionPathwayContent.razor](file:///c:/Users/User/source/repos/OMMv2/OMM.Public/Components/Shared/DecisionPathwayContent.razor) is a Public component. Moving it to an RCL is a significant refactor that the document does not scope. | Defer RCL extraction. For Phase 5A, the Admin Simulator can render a separate copy of the pathway UI that delegates to the same `IDeterministicDecisionEngine` and `IInquiryIntentInterpreter` interfaces but uses Admin-scoped adapters. Shared UI extraction is Phase 5B. | No — but clarify scope. |
| B5 | §7.1 L238 + §3.4 L119 | **MEDIUM** | Emergency prompts are described as both: (a) immutable JSON resources shipped with both applications (§3.4 L110), and (b) Admin-creatable/editable with publication as deployment resources (§3.4 L119). These are contradictory — an immutable shipped resource cannot also be Admin-editable at runtime. | Clarify: Admin manages emergency prompt *content* in the database, identical to regular prompts but tagged as emergency. A separate **publish** step exports the active emergency prompt to a static JSON file in the deployment artifact. The runtime fallback reads the static file only when the database is unreachable. The shipped file is immutable *per deployment*, not permanently. | No — but design must be clear. |
| B6 | §2.1 L43 | **LOW** | `Pro advisory access = Pro membership` is redundant. The document already states there is no global license gate (agreed rule #3). This line adds no information and could cause confusion if someone interprets it as a separate check. | Remove or annotate: "This is intentionally identical to Pro membership. There is no additional license gate." | No. |

---

## C. CMSA/CMSL Compliance Risks

| # | Section | Severity | Issue | Correction | Blocks |
|---|---------|----------|-------|------------|--------|
| C1 | §4.2 L153 | **BLOCKER** | (Same as A1) Pro advisory output scope is undefined. Under CMSA 2007 §58, carrying on a regulated activity (investment advice) without or outside a CMSL is an offence. The document correctly says "compliance sign-off" is needed (§19 L547) but does not gate Pro implementation on it. | Pro prompt content, Pro response validation rules, and Pro UI rendering must not be implemented until the product-scope document from compliance counsel is available. Foundation infrastructure (subscription model, effective-tier, prompt versioning) can proceed. | Blocks Pro production release. |
| C2 | §4.1 L137–146 | **HIGH** | Standard prohibited list is comprehensive but missing: **"portfolio allocation percentages"**, **"position sizing"**, and **"stop-loss levels"**. These could be generated by AI and might constitute investment advice under SC Malaysia Guidelines on Investment Advice (revised 2022). | Add to §4.1 prohibited list: portfolio allocation percentages, position sizing recommendations, stop-loss or take-profit levels. Ensure server-side schema validation rejects these fields in Standard mode. | Does not block foundation, blocks production. |
| C3 | §11 L383–393 | **HIGH** | Risk-context collection (approach, objective, time horizon) is described for all tiers including Standard. If Standard output is shaped by user risk profile, it could be interpreted as *personalized* analysis — which edges toward investment advice under CMSA. | Clarify: Standard mode may collect risk context for **display emphasis only** (which scenarios to highlight) but must not change the content, wording, or recommendations. Add explicit server-side policy: Standard risk-context usage is limited to scenario ordering, not content generation. | Does not block foundation. |
| C4 | §14 L446–453 | **MEDIUM** | Sensitive-event handling describes analysing "governance, leadership continuity, legal, regulatory, reputation" consequences. For Standard tier, this could produce output that reads like advice (e.g., "if CEO resigns, stock may drop 15%, consider reducing exposure"). | Add explicit rule: Standard sensitive-event analysis must present scenarios as educational facts only ("historically, leadership changes in similar companies resulted in X% volatility") without action suggestions. Server-side policy must strip any action language from Standard responses to sensitive events. | Does not block foundation. |

---

## D. Prompt Injection and Unsafe-Output Risks

| # | Section | Severity | Issue | Correction | Blocks |
|---|---------|----------|-------|------------|--------|
| D1 | §4.3 L169 | **HIGH** | "Member text is untrusted data. It must be passed in a distinct user content part with clear delimiters." The current [GoogleAiIntentInterpreter.cs L155](file:///c:/Users/User/source/repos/OMMv2/OMM.Shared/DecisionSupport/GoogleAiIntentInterpreter.cs#L155) uses string interpolation: `$"Classify this member message:\n\n{memberText.Trim()}"`. No structural delimiter or content-part separation. | Refactor to use Gemini's multi-part content structure with the member text in a separate `Part` with a role delimiter. Add input sanitization (strip control characters, enforce max length). This must be done in Phase 5A when refactoring the interpreter. | Does not block start, blocks production. |
| D2 | §4.3 L180 | **HIGH** | "Raw model output is untrusted and must pass JSON schema validation plus semantic policy validation before rendering." The current interpreter (L96–98) deserializes JSON and passes it through `ToResult()` with no schema validation or semantic policy check. | Implement a `IResponseValidator` interface with: (1) JSON schema validation against tier-specific schema, (2) prohibited-field rejection for Standard, (3) semantic checks (no action verbs in Standard summary, no specific price targets, etc.). | Does not block foundation, blocks production. |
| D3 | §4.3 L171–172 | **MEDIUM** | Admin-editable templates "allow only approved placeholders". The document lists `{memberText}` but does not define the complete set of approved placeholders or the validation regex. | Define approved placeholder allowlist (e.g., `{memberText}`, `{tierName}`, `{securityName}`, `{sectorName}`, `{portfolioSummary}`). Implement regex-based validation on save that rejects any `{...}` token not in the allowlist. | No. |

---

## E. Incorrect or Excessive AI-Call Design

| # | Section | Severity | Issue | Correction | Blocks |
|---|---------|----------|-------|------------|--------|
| E1 | §16 L491 | **MEDIUM** | "Standard clarification: 1 per clarification turn." This means a user who submits a vague query, gets a clarification question, answers it, and gets another clarification could burn 3+ AI calls. §10.1 L323 says "at most one focused clarification question" — but if the clarification answer is also ambiguous, the policy is unclear. | Add explicit cap: Standard gets max 2 AI calls per inquiry session (initial + 1 clarification). If still ambiguous after 1 clarification, fall back to guided flow. | No. |
| E2 | §16 L496 | **LOW** | "Cached repeated classification: 0." The cache key strategy is not defined. Without a clear cache-key definition (user text hash + tier + session?), this is aspirational. | Define cache key: SHA-256 of normalized lowercase trimmed member text + tier code. TTL: 15 minutes. Session-scoped only (do not share across users). | No. |
| E3 | §16 L498 | **MEDIUM** | "Call 1 extracts intent, event, entities, ambiguity, safety/relevance status, and verification status." The current AI schema ([GoogleAiIntentInterpreter.cs L163–179](file:///c:/Users/User/source/repos/OMMv2/OMM.Shared/DecisionSupport/GoogleAiIntentInterpreter.cs#L163-L179)) returns 10 fields. The Phase 5 classification (§10.1 L313–321) requires ~15 fields including `inputType`, `verificationStatus`, `needsClarification`, multiple entity arrays. This is a significantly larger schema — validate that Gemini can return this reliably in structured output mode with acceptable latency. | Prototype the expanded schema with Gemini's `responseSchema` before committing. If unreliable, consider two-pass: (a) lightweight intent classification, (b) entity extraction only when needed. Budget remains 1 call for Standard if schema works. | No — but test early. |
| E4 | §10.4 + §16 | **MEDIUM** | The impact map (§10.4) says "Every impact link includes direction, strength, time horizon, confidence, assumptions, evidence, and Malaysia relevance." If this is generated by AI Call 1, it massively expands the response schema. If deterministic, the [DeterministicDecisionEngine](file:///c:/Users/User/source/repos/OMMv2/OMM.Shared/DecisionSupport/DeterministicDecisionEngine.cs) currently has no Admin-configurable impact data. | Clarify: AI Call 1 classifies intent and entities only. The impact map is **deterministic** — built from Admin-managed entity relationship data. AI does not generate the impact chain. This matches the AI-call budget but requires the Admin taxonomy data model (Phase 5B). | No. |

---

## F. Data-Model and Migration Problems

| # | Section | Severity | Issue | Correction | Blocks |
|---|---------|----------|-------|------------|--------|
| F1 | §3.2 L78 | **HIGH** | `MemberSubscription.Status` uses text values `Active`, `Expired`, `Cancelled`, `ManuallyGranted`. Missing: `PendingPayment`, `Suspended`, `GracePeriod`. Payment integrations (even manual) need intermediate states. | Add at minimum: `PendingPayment` (awaiting confirmation), `Suspended` (Admin action). Define state-transition rules as a state machine diagram before implementation. | No — but must decide before coding model. |
| F2 | §3.2 L74 | **MEDIUM** | `MemberSubscription.Id` is `bigint` PK. This suggests a historical table (new row per subscription change). But the document also says "Stores current and historical subscription state" (L70). If each row is a point-in-time record, there's no explicit `IsCurrent` flag or unique constraint for "latest active subscription per user." | Add either: (a) an `IsCurrent` boolean with a unique partial index `(UserId) WHERE IsCurrent = true`, or (b) use a view/query pattern that selects `MAX(Id)` per user. Option (a) is safer for concurrent updates. | No. |
| F3 | §3.3 L104 | **HIGH** | "Only one prompt may be active for each tier." Requires a unique partial index: `CREATE UNIQUE INDEX ... ON AiPromptVersion (Tier) WHERE Status = 'Active'`. The document mentions this but does not specify the EF Core mapping. PostgreSQL supports this natively but EF Core requires `HasFilter()`. | Document the exact index: `entity.HasIndex(e => e.Tier).IsUnique().HasFilter("\"Status\" = 'Active'")` in the EF configuration. Test that activation/deactivation runs in a serializable transaction or uses `SELECT FOR UPDATE`. | No — but must implement correctly. |
| F4 | §15 L461–481 | **MEDIUM** | Five analytics tables (`InquirySession`, `InquiryTurn`, `InquiryEntityLink`, `InquirySelectionEvent`, `InquiryOutcome`) are defined with field descriptions only, no column types, constraints, or indexes. The document says "Define retention, redaction... before enabling analytics" (L481) but does not provide retention periods. | Defer analytics tables to Phase 5B. For Phase 5A, add a single append-only `InquiryLog` table with session ID, timestamp, event type, and JSON payload. Design the full normalized schema after UX is stable. | No. |
| F5 | §3.5 L127 | **LOW** | `SubscriptionDowngradeEvent` requires a "unique deduplication key" but does not define its composition. | Define: `UNIQUE (UserId, PreviousPlanId, ResultingPlanId, DetectedAt::date)` — one downgrade event per user per plan transition per day. | No. |

---

## G. Cache and Emergency-Prompt Failure Handling

| # | Section | Severity | Issue | Correction | Blocks |
|---|---------|----------|-------|------------|--------|
| G1 | §2.1 L48 | **HIGH** | "Use a bounded last-known entitlement cache." No TTL, eviction policy, or storage mechanism is specified. If the cache is in-memory only, app restart loses all entitlements during a DB outage. | Specify: (1) `IMemoryCache` with 15-minute sliding expiration, (2) max 10,000 entries, (3) on DB recovery, revalidate all cached entries within 60 seconds. For persistent cache across restarts, consider a local file-based fallback (SQLite or JSON) — but this adds complexity, so defer to Phase 5B and accept in-memory for Phase 5A. | No — but design must be explicit. |
| G2 | §17 L513 | **MEDIUM** | "Cache invalidation failure: Do not claim instant activation; expose stale-state telemetry." No metric or health-check endpoint is defined. Ops has no way to know if prompt activation is stale. | Add a `/health/prompt-cache` endpoint (or extend existing health checks) that reports: last DB sync time, active prompt version per tier, whether the cached version matches the DB version. | No. |
| G3 | §3.4 L121 | **MEDIUM** | "If an emergency resource is missing or invalid, the matching AI mode is unavailable." The document does not specify what "invalid" means for the emergency JSON. | Define validation: emergency JSON must (1) deserialize to `PromptVersionDto`, (2) have non-empty `SystemInstruction`, (3) have `Tier` matching filename, (4) have `ContentHash` matching computed SHA-256. Validate at app startup; log critical error and expose in health check if invalid. | No. |

---

## H. Entity-Resolution and Global-to-Malaysia Impact Accuracy

| # | Section | Severity | Issue | Correction | Blocks |
|---|---------|----------|-------|------------|--------|
| H1 | §10.4 L356–366 | **HIGH** | The impact map requires entity relationships (country→commodity→sector→stock) managed by Admin (§13). These relationships do not exist in the current data model. The [ApplicationDbContext](file:///c:/Users/User/source/repos/OMMv2/OMM.Public/Data/ApplicationDbContext.cs) has Country→Exchange→Market→Stock and Country→Sector→SubSector→Stock, but no commodity, mechanism, transmission channel, or impact-direction tables. | This is a Phase 5B data-model design task. For Phase 5A, the impact map remains hardcoded in [DeterministicDecisionEngine.cs](file:///c:/Users/User/source/repos/OMMv2/OMM.Shared/DecisionSupport/DeterministicDecisionEngine.cs) (current state). Phase 5B adds: `EventType`, `TransmissionMechanism`, `EntityRelationship`, `ImpactRule` tables with Admin CRUD. | Blocks inquiry pipeline, not foundation. |
| H2 | §10.1 L318 | **MEDIUM** | AI classification must resolve "countries, markets, stocks, sectors, subsectors, commodities, currencies, rates, and policies." The current DB has no commodity, currency, or policy-rate master data. | For Phase 5A, entity resolution uses Bursa stock codes and sector codes from existing master data. Commodity and currency resolution is deferred to Phase 5B when the impact-graph data model exists. | No. |
| H3 | §10.4 L364 | **MEDIUM** | "The AI must not invent an unrelated connection." Enforcement requires the server to validate AI-suggested entity links against Admin-approved relationships. This validation does not exist and requires the Phase 5B data model. | For Phase 5A: AI Call 1 returns entities; server validates stock/sector codes against existing master data. Unrecognized entities are flagged as "unresolved" and excluded from the impact map. Full relationship validation is Phase 5B. | No. |

---

## I. Portfolio-Linking and Alert Risks

| # | Section | Severity | Issue | Correction | Blocks |
|---|---------|----------|-------|------------|--------|
| I1 | §12 L399 | **HIGH** | "OMM must not create alerts automatically from an AI response." Good. But the alert monitoring mechanism is undefined. Who evaluates "stock price or percentage movement" (L404)? This requires either real-time market data feeds or periodic price polling, neither of which exist in the codebase. | Defer alert evaluation to Phase 5B/6. For Phase 5A: allow users to create alert *records* (intent to monitor). Evaluation and notification require market-data infrastructure that is out of scope per §19 L558. | Does not block foundation. |
| I2 | §10.4 L364 + §9 L279 | **MEDIUM** | Portfolio linking ("link impacts to the user portfolio") requires reading the user's portfolio holdings. The current [IPortfolioContextProvider](file:///c:/Users/User/source/repos/OMMv2/OMM.Shared/DecisionSupport/IPortfolioContextProvider.cs) exists but its implementation scope is unclear. If portfolio data is sparse (user hasn't entered holdings), the "Relate to me" stage (§10.3) produces empty output. | Add a graceful fallback: if portfolio data is insufficient, skip the "Relate to me" stage and show a prompt to add holdings. Do not call AI with empty portfolio context. | No. |

---

## J. UX Complexity, Unnecessary Screens, and Abandonment Risk

| # | Section | Severity | Issue | Correction | Blocks |
|---|---------|----------|-------|------------|--------|
| J1 | §10.2 L327 | **MEDIUM** | Nine purpose options + "I'm not sure — guide me." For Standard users who asked a simple question ("what happened to Maybank?"), presenting 9 checkboxes after the first AI response adds friction. The document says "simple inquiries should complete in two screens" (§10.3 L350) but the purpose selector is an additional screen. | For simple, high-confidence classifications (confidence > 0.8, single clear intent), auto-select the most relevant 1–2 purposes and skip the purpose selector. Show the selector only when confidence is low or intent is ambiguous. | No — but design before building. |
| J2 | §10.3 L343–350 | **LOW** | Four adaptive stages are described but the document does not define which stages are shown for which combination of tier × purpose × confidence. Without a stage-visibility matrix, implementers will guess. | Create a stage-visibility matrix: rows = purpose combinations, columns = tier (Standard/Pro), cells = which stages are shown. Include in the Phase 5B spec. | No. |
| J3 | §11 L383 | **MEDIUM** | Risk-context collection asks 3 questions (approach, objective, time horizon) before showing portfolio analysis. For a returning user, this is redundant if their risk profile hasn't changed. | Cache risk-context per user with a "use previous" option. Only prompt for updates if > 90 days old or user explicitly requests. | No. |

---

## K. Admin Usability and Rule-Maintenance Risks

| # | Section | Severity | Issue | Correction | Blocks |
|---|---------|----------|-------|------------|--------|
| K1 | §13 L415–427 | **HIGH** | Admin-managed taxonomy includes 10+ configuration domains (event types, keywords, aliases, entity relationships, impact rules, thresholds, clarification questions, routing defaults, response templates, sensitive-event rules). This is a significant Admin application in itself. No wireframes, page structure, or information architecture is provided. | Break into discrete Admin pages with a priority order: (1) event taxonomy CRUD, (2) entity relationship CRUD, (3) impact-rule CRUD. Design wireframes before implementation. Response templates and clarification questions can be Phase 5C. | Does not block subscription/prompt foundation. |
| K2 | §13 L427 | **MEDIUM** | "Rules must be testable in the Admin Simulator." The simulator (§8) is designed for prompt testing. Testing Admin taxonomy rules (e.g., "does this event correctly map to this sector?") requires a separate rule-testing UI, not just running a pathway simulation. | Add a rule-testing mode to the simulator: Admin enters an event type and the system shows the deterministic impact map without AI. This is simpler than full pathway simulation. | No. |
| K3 | §7.1 L242 | **LOW** | Cache-propagation status display in Admin UI. Useful but undefined. What does "stale" look like? What action can Admin take? | Define: Admin sees a banner "Prompt version X active since [time]. Last confirmed propagation to Public: [time]. Status: Synced / Stale [N seconds]". If stale > 5 minutes, show a warning with a "Force re-check" button. | No. |

---

## L. Analytics Privacy and Data-Retention Issues

| # | Section | Severity | Issue | Correction | Blocks |
|---|---------|----------|-------|------------|--------|
| L1 | §15 L467 | **HIGH** | `InquiryTurn` stores "original prompt" (the user's message). This is PII under Malaysia's PDPA 2010 (Personal Data Protection Act). The document says "privacy-safe prompt hash" is also stored, but retaining the original text requires: consent notice, retention period, encryption at rest, access controls, and deletion mechanism. | Specify: (1) do NOT store the original user message in analytics. Store only the SHA-256 hash and the classified intent/entities. (2) If original text is needed for debugging, store in a separate short-retention table (7 days max) with encryption and Admin-audit access only. (3) Update the Public privacy notice. Ref: PDPA 2010 §7 (General Principle), §10 (Retention Principle). | Does not block foundation, blocks production analytics. |
| L2 | §15 L481 | **MEDIUM** | "Define retention, redaction, Admin access, anonymization, and deletion behavior before enabling analytics." Correct — but the document does not define any of these. | Provide default retention periods: InquirySession/Turn/EntityLink/Selection = 12 months rolling, then anonymize (null UserId, hash session ID). InquiryOutcome = 24 months for business reporting. Admin access requires `ViewAnalytics` policy. Hard-delete available via Admin with audit log. | No — but must define before enabling. |
| L3 | §5 L211 | **LOW** | "Do not log API keys, full member messages, or raw model output by default." Good. But "by default" implies they could be logged. Clarify that API keys must NEVER be logged, and member messages / raw output may only be logged at `Debug` level with explicit opt-in via config, never in production default. | Replace "by default" with: "API keys must never be logged at any level. Full member messages and raw model output may only be logged when `Logging:DecisionSupport:LogSensitiveData` is explicitly set to `true` in non-production environments." | No. |

---

## M. Testing Gaps and Production-Readiness Concerns

| # | Section | Severity | Issue | Correction | Blocks |
|---|---------|----------|-------|------------|--------|
| M1 | §18 L518–543 | **MEDIUM** | 34 test categories listed. No test infrastructure exists for: prompt activation transactions, cross-process cache invalidation, emergency JSON validation, or simulator persona isolation. The current [DeterministicDecisionEngineTests.cs](file:///c:/Users/User/source/repos/OMMv2/OMM.Shared.Tests/DecisionSupport/DeterministicDecisionEngineTests.cs) tests the existing engine only. | Prioritize tests in implementation order: (1) effective-tier calculation unit tests, (2) prompt activation/deactivation integration tests, (3) emergency fallback tests, (4) Standard advisory-field rejection tests. Inquiry pipeline tests are Phase 5B. | No — but plan test infra early. |
| M2 | §18 (missing) | **HIGH** | No load/stress testing mentioned. If the prompt provider uses `IMemoryCache` + DB polling, what happens under 1000 concurrent inquiry requests? Cache stampede on expiry? | Add a test scenario: simulate cache expiry under concurrent requests. Ensure only one DB query is issued (use `SemaphoreSlim` or `Lazy<Task<T>>` pattern in the cache layer). | Does not block start, blocks production. |
| M3 | §18 (missing) | **MEDIUM** | No contract/integration test for Gemini API schema changes. If Google changes the `generateContent` response format, the system breaks silently. | Add a periodic (weekly CI) integration test that sends a known prompt to Gemini and validates the response schema. Alert on failure. | Does not block start. |

---

## N. Items That Can Be Simplified or Postponed

| # | Item | Current Phase | Recommendation |
|---|------|---------------|----------------|
| N1 | Admin taxonomy and impact-rule engine (§13) | Phase 5 | **Postpone to Phase 5B**. Foundation can use hardcoded rules in `DeterministicDecisionEngine`. |
| N2 | Full analytics schema (§15, 5 tables) | Phase 5 | **Simplify to Phase 5A**: single `InquiryLog` append-only table with JSON payload. Normalize in Phase 5B. |
| N3 | PostgreSQL LISTEN/NOTIFY for cache invalidation (§5) | Phase 5 | **Simplify**: use DB revision-token polling with 30-second TTL. |
| N4 | Shared Razor Class Library for pathway UI (§8 L273) | Phase 5 | **Postpone**. Admin Simulator uses its own pathway rendering. Extract RCL when both UIs are stable. |
| N5 | Alert evaluation and monitoring (§12 L401–409) | Phase 5 | **Postpone to Phase 6**. Requires market-data infrastructure. Phase 5A: alert record creation only. |
| N6 | Risk-context collection for Standard tier (§11 L383) | Phase 5 | **Simplify**: Standard shows all scenarios equally. Risk-context is Pro-only for Phase 5A. |
| N7 | A/B testing, payment gateway, licensing retrieval (§19 L558) | Explicitly out of scope | Confirmed — keep out. |
| N8 | Nine-purpose selector UI (§10.2) | Phase 5 | **Simplify for Phase 5A**: show 3–4 core purposes (Explain, Impact on Malaysia, Impact on portfolio, Decision Pathway). Expand to 9 in Phase 5B after UX testing. |

---

## Must Resolve Before Coding — Checklist

- [ ] **Migration ownership** (A2): Decide which project owns all new `public` schema tables.
- [ ] **Pro product scope** (A1/C1): Obtain compliance sign-off defining CMSL category, approved advisory fields, in-scope securities, and mandatory disclosures. *Pro content cannot be implemented without this.*
- [ ] **Phase split** (A3): Formally split Phase 5 into 5A (subscription + prompt + effective-tier + simulator shell + basic inquiry) and 5B (inquiry pipeline + Admin taxonomy + analytics + alerts).
- [ ] **Emergency prompt lifecycle** (B5): Document the Admin-edit → publish → static-file deployment pipeline.
- [ ] **Interpreter refactor scope** (D1): Confirm that `GoogleAiIntentInterpreter` will be refactored to accept `IPromptProvider` and use structural content parts.
- [ ] **MemberSubscription status values** (F1): Finalize the status enum and state-transition rules.
- [ ] **Standard prohibited-field list** (C2): Add portfolio allocation percentages, position sizing, and stop-loss levels.
- [ ] **Analytics PII policy** (L1): Decide whether to store original user messages (requires PDPA compliance) or hash-only.

---

## Recommended Phase 5 Implementation Order

### Phase 5A — Foundation (4–6 weeks)

1. **Data model**: `SubscriptionPlan`, `MemberSubscription`, `SubscriptionDowngradeEvent`, `AiPromptVersion`, `AiPromptAuditEvent` — migration in the decided owner project.
2. **Effective-tier service**: `IEffectiveTierResolver` in `OMM.Shared` with unit tests for all tier/expiry/payment combinations.
3. **Prompt provider**: `IPromptProvider` + `EfPromptProvider` with `IMemoryCache` and DB revision-token polling.
4. **Emergency prompt resources**: Ship `standard-emergency-v1.json` and `pro-emergency-v1.json`. Startup validation.
5. **Prompt fallback resolver**: Chain: DB active → emergency JSON → unavailable. Unit + integration tests.
6. **Interpreter refactor**: `GoogleAiIntentInterpreter` accepts `PromptVersionDto` from `IPromptProvider`. Structural content-part separation. Standard response validation.
7. **Admin prompt management UI**: CRUD, activation/deactivation, audit log, emergency prompt management.
8. **Admin subscription management**: Assign plan, set expiry, view history. Downgrade detection + notification.
9. **Public pathway integration**: `DecisionPathwayContent.razor` receives execution context with effective tier. Non-member, Standard, Pro rendering. Upgrade callout. Downgrade notice.
10. **Admin Simulator shell**: Persona selector, prompt-version override, inspector panel. Uses same interpreter + engine interfaces.
11. **Tests**: Effective-tier, prompt activation/deactivation/fallback, Standard field rejection, non-member no-AI, simulator isolation.

### Phase 5B — Inquiry Pipeline & Admin Rules (6–8 weeks, after 5A stabilizes)

1. Expanded AI classification schema (§10.1).
2. Multi-purpose selector UX (§10.2).
3. Four-stage adaptive UI (§10.3).
4. Admin taxonomy data model (event types, entity relationships, impact rules).
5. Admin taxonomy CRUD pages.
6. Deterministic impact-map refactor (read from Admin rules instead of static C#).
7. Scenario planning with Admin-managed templates.
8. Full analytics schema (5 normalized tables).
9. Risk-context collection (Pro only initially).
10. Alert record creation (evaluation deferred to Phase 6).
11. RCL extraction for shared pathway UI.
12. LISTEN/NOTIFY cache invalidation (if polling latency is unacceptable).

---

## Assumptions Made

1. **CMSL status**: I assume OMM does not yet hold a CMSL for investment advice. If it does, the Pro scope should be defined by the licence conditions. If applying, Pro cannot go live until the licence is granted. Ref: CMSA 2007 Part IV.
2. **No existing subscription infrastructure**: Confirmed by codebase search — no `SubscriptionPlan`, `MemberSubscription`, or `IsPaid` exists.
3. **Single PostgreSQL database**: Both `OMM.Admin` and `OMM.Public` connect to the same PostgreSQL instance with separate schemas (`admin` for identity, `public` for master data and member data).
4. **No market-data feed**: Confirmed by §19 L558 (out of scope). Alert evaluation requires this and must be deferred.
5. **Gemini as sole AI provider**: The document and codebase reference only Google Gemini. No multi-provider abstraction is needed for Phase 5.
6. **PDPA 2010 applies**: Malaysia's Personal Data Protection Act governs user data handling including inquiry analytics. Ref: [PDPA 2010](https://www.pdp.gov.my/).
7. **No approval workflow**: Confirmed by agreed rule #5. Admin prompt activation does not require a second approver.
8. **`DeterministicDecisionEngine` will be refactored**: Its current hardcoded static data will eventually be replaced by Admin-managed rules, but this is a Phase 5B task.
