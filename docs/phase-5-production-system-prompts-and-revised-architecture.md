# Phase 5 — CrystalBall Production Prompts, Subscription Tiers, and Revised Architecture

> **Document status:** Phase 5 design — Pro is intended to be available by default; Admin can adjust features or disable Public Pro
> **Phase:** 5 — CrystalBall tiered prompts, subscription infrastructure, and inquiry planning
> **Depends on:** Phases 0–4 complete; the existing Decision Pathway remains available as a legacy reference during CrystalBall development

## 1. Purpose and decisions

Phase 5 introduces database-managed AI prompts, database-driven subscription plans, the new CrystalBall inquiry and impact-analysis flow, and an Admin Journey Simulator. The existing `DecisionPathway` is not rewritten in this phase; its deterministic logic and Gemini/fallback patterns may be reused by CrystalBall. CrystalBall becomes the replacement only after production parity and migration checks pass.

| Experience | AI prompt | Result |
| --- | --- | --- |
| **Non-member** | No | Guided wizard only; no AI call and no saved member history |
| **Standard member** | Standard prompt | `Educational information only`; no advisory content |
| **Pro member** | Pro prompt by default; Standard mode may be selected | Display label: `Personalized Pro Advice`; paid personalized advisory service while the Pro subscription is valid and Public Pro is enabled |

Mandatory decisions:

1. `IsPaid` and subscription expiry are subscription facts, not the complete authorization boundary.
2. Standard is strictly non-advisory and educational. Pro is the paid licensed advisory mode. The distinction is enforced by server-side policy and response validation; prompt wording alone is not a compliance control.
3. Expired or invalid Pro subscriptions automatically become Standard and generate a downgrade notification.
4. Prompt versions are immutable after creation. Editing creates a new version.
5. Admins may activate or deactivate prompt versions without a separate approval workflow.
6. `PublicProEnabled` is an Admin-controlled `SystemSetting` feature setting. It defaults to `true`, does not change subscriptions, and is ignored by the Admin Simulator.
7. `AiClassificationEnabled` remains the global Public AI kill switch. When it is disabled, Standard and Pro use the guided wizard only; the Admin Simulator remains fully functional.
8. A Pro member may voluntarily select Standard educational mode. This does not downgrade the subscription or create a downgrade event.
9. Plan codes and prompt modes are database-driven. Application code must not assume that every future plan is named `PRO`.
10. Standard and Pro disclosure copy is centralized in one shared localization resource catalog in `OMM.Shared`; all Public and Admin pages use the same resource keys. English is provided first, with Chinese resource files supported later. Admin does not edit disclosure copy at runtime.

This document uses **CMSA** for the Capital Markets and Services Act and **CMSL/CMSRL** for applicable licences. SC feedback will be handled by the OMM team, which may adjust the feature configuration or disable Public Pro through Admin settings.

## 2. Effective access rules

### 2.1 Effective entitlement and runtime mode

The effective subscription entitlement is calculated server-side. The selected runtime mode is a separate, validated choice:

```text
Non-member = no authenticated Public member

On Public registration, create one active Standard lifetime subscription with a null expiry. Do not keep a second active Standard row while Pro is active.

Active Pro entitlement = authenticated member
                         AND a current MemberSubscription maps to a plan
                         AND that plan maps to an advisory-capable prompt mode
                         AND (IsPaid = true OR ManuallyGranted = true)
                         AND Status IN (Active, Scheduled)
                         AND StartedAt <= UtcNow
                         AND SubscriptionExpiryDate is not null
                         AND SubscriptionExpiryDate > UtcNow

Standard entitlement = authenticated member who is not currently entitled to an advisory-capable plan

Pro runtime mode = active Pro entitlement AND requested mode is Pro AND PublicProEnabled = true
Standard runtime mode = Standard entitlement, or a Pro member who selects Standard,
                       or a Pro member temporarily falling back because Public Pro is disabled/unavailable
```

`SubscriptionPlan.Code`, prompt mode, payment state, and expiry are read from the database. Plan codes are data, not application-level entitlement constants. A future plan may map to Standard, Pro, or another approved prompt mode without changing the resolver.

The Public client must not supply or override entitlement, prompt mode, or advisory access. A Pro member may request Standard mode, but a Standard member cannot request Pro mode. `PublicProEnabled` controls Public availability only and never changes the stored subscription.

If `PublicProEnabled = false`, a Pro member remains subscribed but is served Standard mode with a small `Standard mode — Pro analysis temporarily unavailable` badge. This is not a subscription downgrade and does not create a `SubscriptionDowngradeEvent`.

If `AiClassificationEnabled = false`, all Public AI modes are unavailable and the guided wizard remains available. The Admin Simulator ignores this setting.

If subscription state cannot be verified, use a bounded last-known entitlement cache with an absolute maximum-stale period. If no trusted entitlement is available, do not provide Pro advisory output; show a temporary unavailable state and retain the member's subscription data for recovery.

## 3. Database model

### 3.0 `AiPromptMode` master table

Stores server-owned prompt modes. Initial rows are `STANDARD` and `PRO`; the table allows future modes without making plan names or prompt modes C# authorization constants.

| Column | Type | Description |
| --- | --- | --- |
| `Code` | `text` PK | Stable mode code |
| `DisplayName` | `text` | Admin/public label |
| `IsAdvisory` | `bool` | Server-owned policy classification |
| `SchemaVersion` | `text` | Server-owned response contract |
| `IsActive` | `bool` | Whether plans/prompts may use the mode |

`IsAdvisory`, response schemas, and compliance policies are server-owned. Admins may edit prompt wording but may not turn an educational mode into an advisory mode through the prompt editor.

### 3.1 `SubscriptionPlan` master table

Stores available plans. It is not a user entitlement table.

| Column | Type | Description |
| --- | --- | --- |
| `Id` | `int` PK | Plan identifier |
| `Code` | `text` unique | Database business code; initially seeded with `STANDARD` and `PRO`, but future plans are not hardcoded in C# |
| `DisplayName` | `text` | Public-facing name |
| `Description` | `text` | Public-facing description |
| `PromptModeCode` | `text` FK | Database-managed AI mode used by the plan, for example `STANDARD` or `PRO` |
| `DurationMonths` | `int` nullable | Term length for finite plans; initial Pro plan is `12` calendar months; `null` is permitted only for lifetime Standard |
| `PriceAmount` / `CurrencyCode` | `numeric` / `text` nullable | Admin-configured price and currency for paid plans; initial Pro value is provisional `99.00` / `MYR` (`RM99` per 12 months) |
| `PaymentInstructions` | `text` nullable | Admin-configured manual payment instructions shown during upgrade |
| `IsActive` | `bool` | Whether the plan can be assigned |
| `SortOrder` | `int` | Display order |
| `CreatedAt` / `ModifiedAt` | `timestamptz` | Audit timestamps |

Compliance-critical wording must not be freely editable as ordinary marketing copy. Admin editing remains immediately usable without a second approval workflow, but server validation, authorization, activation/deactivation controls, and audit history are mandatory.

`PromptModeCode` references a small server-owned `AiPromptMode` master table. It is seeded with `STANDARD` and `PRO`; future modes may be added without hardcoding plan names into C# authorization logic. Prompt modes remain subject to server-owned schema and compliance policies.

### 3.2 `MemberSubscription`

Stores current and historical subscription state for a Public member.

| Column | Type | Description |
| --- | --- | --- |
| `Id` | `bigint` PK | Subscription record |
| `UserId` | `text` nullable FK | References `public.AspNetUsers.Id`; `ON DELETE SET NULL` preserves history while allowing account deletion |
| `SubscriptionPlanId` | `int` FK | Assigned plan |
| `IsPaid` | `bool` | Payment fact only; it is not a lifecycle or termination status |
| `ManuallyGranted` | `bool` | Whether an Admin/system grant provides entitlement independently of payment |
| `Status` | `text` | Lifecycle state: `Scheduled`, `Active`, `Suspended`, `Expired`, `Cancelled`, or `Terminated` |
| `StartedAt` | `timestamptz` | Subscription start |
| `SubscriptionExpiryDate` | `timestamptz` nullable | `null` is valid for Standard lifetime access; Pro must have a finite expiry |
| `TerminatedAt` | `timestamptz` nullable | Actual early termination time; preserve the original scheduled expiry for history |
| `UpdatedAt` | `timestamptz` | Last change |
| `UpdatedByUserId` | `text` nullable | Admin or system actor |
| `ProviderReference` | `text` nullable | Payment provider reference |

There is no automatic renewal. A member explicitly renews and pays (or receives a manual grant); if renewal happens before the current expiry, create a `Scheduled` record with `StartedAt` equal to the current expiry and `SubscriptionExpiryDate` equal to `StartedAt + DurationMonths` using calendar-month arithmetic. The current record remains `Active` until expiry. A scheduled record grants no access before its start and may be cancelled before it starts. At the start time, the lifecycle job changes it to `Active`; the entitlement resolver may also recognize its valid time window if the job is delayed. Enforce at most one current `Active` and one future `Scheduled` record per user.

On registration, create one `Active` Standard lifetime record with `SubscriptionExpiryDate = null`. When a member upgrades, terminate that Standard row with reason `Upgraded to Pro` and create one `Active` Pro row with a 12-calendar-month term in the same transaction. While Pro is active, Standard mode remains available as a runtime choice/fallback but does not require a second active subscription row. If the Pro term expires without a scheduled renewal, create a new `Active` Standard lifetime row. Historical Standard and Pro rows remain in the subscription history.

`IsPaid` records payment state and is not a termination flag. A false value never grants Pro access unless `ManuallyGranted = true`. If a member terminates an active Pro plan early, set `Status = Terminated` and `TerminatedAt`, cancel any future `Scheduled` renewal, end Pro access immediately, retain `IsPaid` as a payment fact, preserve the original scheduled expiry date, record the termination, and create one Standard lifetime subscription. No automatic refund is issued. Admin suspension temporarily blocks the account/subscription and can be reversed while the term remains valid. For confirmed fraud/account termination, disable the Public account and terminate its current and scheduled subscriptions; retain the records for audit. A Pro row with a null expiry is invalid and must be corrected to Standard rather than treated as lifetime Pro.

Add an append-only `SubscriptionChangeEvent` for every renewal, scheduled-renewal cancellation, early termination, suspension/resumption, account termination, plan change, and payment/grant change. Each event records the subscription/member, action, previous and new values, actor type and ID, timestamp, reason, and a `Notes` field so the member, Admin, or system actor can leave context. The scheduled lifecycle job expires current records and activates due scheduled renewals idempotently. A no-renewal expiry creates one Standard lifetime record and notification. A due scheduled renewal replaces that transition without creating a duplicate Standard record.

`MemberSubscription.UserId` has a nullable database foreign key to the Public identity table with `ON DELETE SET NULL`. Before account deletion, end any current entitlement and cancel any scheduled renewal; deleting the identity then nulls the historical subscription user link instead of deleting records. Unique partial indexes enforce at most one `Active` and one `Scheduled` subscription per non-null user. Do not store both `SubscriptionTier` and a separate plan code as independent authorities. The effective plan is derived from the currently valid `MemberSubscription` and its referenced `SubscriptionPlan`.

### 3.2.1 `SubscriptionUpgradeRequest`

Supports manual payment verification without payment-gateway automation.

| Column | Type | Description |
| --- | --- | --- |
| `Id` | `bigint` PK | Upgrade request |
| `UserId` | `text` nullable FK | References `public.AspNetUsers.Id`; `ON DELETE SET NULL` preserves request history |
| `SubscriptionPlanId` | `int` FK | Requested plan |
| `AmountDue` / `CurrencyCode` | `numeric` / `text` | Price snapshot shown when the request was created |
| `Status` | `text` | `AwaitingPayment`, `PaymentSubmitted`, `Approved`, `Rejected`, or `Withdrawn` |
| `PaymentReference` | `text` nullable | Member-provided reference for manual verification |
| `CreatedAt` / `SubmittedAt` | `timestamptz` | Request and payment-reference submission times |
| `ReviewedAt` / `ReviewedByUserId` | audit fields | Admin review audit |
| `ReviewNotes` | `text` nullable | Admin decision context |

The member selects **Upgrade to Pro**, sees the plan's Admin-configured price, currency, and payment instructions, and submits a payment reference; `PaymentReference` is required when status becomes `PaymentSubmitted`. The request snapshots the displayed amount and currency, so a later plan-price change does not alter an open request. The request does not grant Pro access. Admin verifies payment outside OMM and approves or rejects the request. Approval atomically creates a paid Pro subscription: `Active` if the member has no current Pro term, or `Scheduled` at the current Pro expiry when this is an early renewal. Approval is idempotent and cannot create duplicate subscriptions. Rejection records a reason and leaves the member's current entitlement unchanged. Only one open upgrade request is allowed per member and requested plan. Admin may separately issue a manual grant without a payment request.

### 3.3 `AiPromptVersion`

Stores versioned prompt content for each approved `AiPromptMode`.

| Column | Type | Description |
| --- | --- | --- |
| `Id` | `int` PK | Prompt version |
| `PromptModeCode` | `text` FK | References `AiPromptMode.Code`; seeded with `STANDARD` and `PRO` |
| `VersionLabel` | `text` | Human-readable label |
| `SystemInstruction` | `text` | Editable prompt text |
| `UserMessageTemplate` | `text` | Editable template with approved placeholders only |
| `SchemaVersion` | `text` | Server-owned response contract version |
| `ContentHash` | `text` | Hash of approved prompt content |
| `Status` | `text` | `Draft`, `Active`, `Inactive`, or `Archived` |
| `Notes` | `text` nullable | Change rationale |
| `CreatedAt` / `CreatedByUserId` | audit fields | Creation audit |

Only one prompt may be active for each prompt mode. Activation/deactivation must run in a transaction and handle concurrent activation requests. A unique partial index must enforce this database rule.

Add an immutable `AiPromptAuditEvent` table for creation, activation, deactivation, rollback, archive, and cache-invalidation events. Approval is not a required workflow. Do not rely on one deactivation timestamp to represent repeated activation history.

### 3.4 Emergency prompt resources

Store immutable, versioned baseline JSON resources in `OMM.Shared` as embedded resources so both applications can read the same fallback content:

```text
EmergencyPrompts/standard-emergency-v1.json
EmergencyPrompts/pro-emergency-v1.json
```

The `OMM.Shared` JSON files are last-resort emergency copies of the Standard and Pro prompt contracts. They are validated at startup and identified by a content hash. They are not ordinary C# string constants and are not the normal runtime source. Both Admin and Public can read these embedded resources, but the running applications cannot mutate them.

Emergency JSON is immutable at runtime and is not editable or publishable through Admin. Admins may inspect the embedded baseline and its hash in a read-only view. Changing an emergency baseline requires a code change and deployment. The database remains the normal source for Admin-managed prompt versions; the `OMM.Shared` embedded JSON is used only when the prompt database is unavailable.

There must be one active emergency prompt for Standard and one active emergency prompt for Pro. If an emergency resource is missing or invalid, the matching AI mode is unavailable. A Pro request may then use the safe Standard prompt as an explicitly labelled `Standard fallback — Pro analysis temporarily unavailable`, but it must never render Pro advisory fields. If the global AI switch is disabled, no AI prompt is used and the guided flow remains available.

### 3.5 `SubscriptionDowngradeEvent`

Records automatic Pro-to-Standard transitions and prevents duplicate notifications.

Required fields include `UserId`, previous plan, resulting plan, reason, detected timestamp, expiry timestamp, notification status, and a unique idempotency key derived from the source subscription transition/version. Do not deduplicate by calendar date alone.

The effective-entitlement resolver switches to a due scheduled renewal at its start time, or to Standard at the current term's expiry when there is no scheduled renewal, even if the lifecycle job has not run. The job materializes those transitions, records any downgrade event, and sends the appropriate notification.

Persist `SubscriptionLifecycleJobRun` history for Admin monitoring. Each run records its start/completion timestamps, scheduled or Admin trigger source, triggering Admin ID when applicable, status, scanned/activated/expired/downgraded/notified counts, correlation ID, and a sanitized error summary on failure.

### 3.6 Migration ownership

`OMM.Public` owns EF Core migrations for all new shared `public` schema tables, including plans, subscriptions, prompt versions, inquiry telemetry, and alert records. `OMM.Shared` may contain entities, contracts, and policies but must not create migrations. `OMM.Admin.MasterDataDbContext` may map and access shared public tables for Admin operations but must not create a competing migration history.

## 4. Prompt and response policy

### 4.1 Standard mode

Standard is OMM's non-advisory educational mode. Display the exact disclosure `Educational information only`. It may classify intent and explain general educational mechanics, but it must not be presented as OMM's licensed advisory service or produce or render:

- buy, sell, or hold recommendations;
- personalized portfolio advice;
- preferred or ranked actions;
- conviction ratings;
- allocation guidance;
- position sizing;
- entry or exit targets;
- stop-loss or take-profit levels;
- ranked securities, opportunities, or preferred actions;
- personalized portfolio action plans;
- language likely to induce a member to take a position in a specific security.

The Standard response contract must contain only educational fields. If the model returns advisory fields, the server rejects the response or renders only a deterministic safe summary; it must not rely on regex-only stripping of action words. Standard rendering uses an allowlisted schema and templates, so an AI response cannot introduce action, allocation, target-price, or ranked-opportunity content through free-form text.

The deterministic compliance policy is authoritative. The model prompt is only one input to that policy.

### 4.2 Pro mode

Pro is the paid licensed advisory mode. Display the exact label `Personalized Pro Advice`. Pro is intended to provide customized, granular advice across the full wealth-strategy scope requested for OMM, including:

- custom portfolio construction, asset allocations, security selections, macro hedging strategies, and tactical rebalancing triggers tailored to member risk parameters;
- retirement cash-flow projections, capital-preservation plans, and multi-variable stress testing;
- tax-efficiency strategies, corporate liability structures, leverage optimization, and generational estate-planning frameworks.

The server-owned Pro response contracts and validators must cover these domains explicitly. A Pro response may include actions, conviction, allocation guidance, and price boundaries where relevant to the selected analysis.

The server must independently validate:

- effective Pro entitlement;
- security identity;
- data freshness and source;
- currency and price validity;
- risk-profile/suitability inputs;
- target-price relationships and numeric ranges;
- required disclosures.

Model confidence must not be used as the authorization or suitability decision. Required disclosures are added by the server renderer and cannot be removed by prompt content or model output.

The exact label is `Personalized Pro Advice`. Full mode-specific disclosure copy is loaded from the shared localized resource catalog; do not duplicate disclosure strings in individual pages or prompts.

### 4.3 Prompt injection and admin editing

Member text is untrusted data. It must be passed in a distinct user content part with clear delimiters, maximum length, and control-character handling; it must never replace system policy. Portfolio and external-source text are also untrusted data.

The server uses member identity only to authorize the request and load the required profile/portfolio facts. The AI provider payload contains a purpose-built, minimum-necessary financial summary and must not include the member's name, email, Public `UserId`, login credentials, or account identifiers. Include only financial fields required for the requested analysis.

Admin-editable templates must:

- allow only approved placeholders, including `{memberText}`;
- reject unknown control placeholders;
- use server-owned schemas and compliance policies;
- be validated before saving;
- require authorization and audit logging;
- support activation/deactivation without an approval workflow.

Raw model output is untrusted and must pass JSON schema validation plus semantic policy validation before rendering.

## 5. Prompt provider, fallback resolution, and caching

`IPromptProvider` belongs in `OMM.Shared`, with a shared DTO and server-side persistence adapters registered by both applications. The Admin Simulator and Public runtime must not share a prompt override cache entry.

The provider must support:

```csharp
Task<PromptVersionDto?> GetActivePromptAsync(
    string promptModeCode,
    CancellationToken cancellationToken = default);

Task<PromptVersionDto?> GetVersionAsync(
    int promptVersionId,
    CancellationToken cancellationToken = default);
```

The provider may use `IMemoryCache` locally, but Admin and Public are separate processes. “Apply Now” must publish cross-process invalidation using PostgreSQL `LISTEN/NOTIFY`, a distributed cache, or a database revision token.

Prompt resolution is performed after entitlement and requested-mode resolution:

```text
Standard runtime mode  → active Standard database prompt → standard-emergency-v1.json → guided/unavailable
Pro runtime mode       → active Pro database prompt      → pro-emergency-v1.json
                         → safe Standard database prompt → standard-emergency-v1.json → guided/unavailable
```

The fallback from Pro to Standard is a runtime-mode fallback only. It is allowed when `PublicProEnabled = false` or both Pro prompt sources fail, and it must display a Standard-fallback badge and never render Pro advisory fields. It must not change the stored Pro entitlement or create a subscription downgrade event. Subscription expiry or invalid entitlement is what changes the subscription to Standard.

Prompt cache entries must include the active version ID/content hash. A cache miss, unseeded database, or failed invalidation must fail closed and never select a Pro prompt accidentally.

Cache keys must include application/environment, prompt mode, active version ID/content hash, schema version, and—only for the Admin Simulator—the override version and simulator session ID. Public requests must never accept a prompt version from the browser. Use the existing `SystemSetting` table as a monotonic prompt/settings revision source and poll it with a 30-second TTL in Phase 5A; `LISTEN/NOTIFY` may be added later.

Active-prompt lookup, activation, cache invalidation, and effective-tier resolution require structured logs and correlation IDs. API keys must never be logged. Full member messages and raw model output may be logged only through an explicitly enabled, non-production diagnostic path with redaction and retention controls.

## 6. Interpreter and deterministic engine

`GoogleAiIntentInterpreter` receives a server-resolved immutable execution context rather than an arbitrary string tier supplied by UI code. The context contains entitlement, requested mode, effective mode, simulator/public origin, prompt override, persistence permission, disclosure mode, and correlation ID.

The interpreter must:

1. reject anonymous AI requests;
2. resolve the prompt version through `IPromptProvider`;
3. use the server-owned schema for the resolved prompt mode;
4. apply request timeouts and bounded retries;
5. reject malformed or semantically invalid JSON;
6. return a safe partial/unavailable result when the provider fails, preserving any deterministic result already completed;
7. use an `IAiProvider` abstraction so Gemini can be replaced without changing CrystalBall contracts;
8. apply bounded automatic retries, per-user/session quotas, and a hard server-side call ceiling.

`DeterministicDecisionEngine` remains authoritative for calculations and rule evaluation. It must not trust an AI action, confidence value, or target price without validating the execution context and source data.

## 7. Admin workflows

### 7.1 Prompt management

`/admin/ai-prompts` provides:

- Standard and Pro tabs;
- draft creation and duplication;
- server-side validation;
- activation, deactivation, and rollback;
- read-only view of the embedded Standard and Pro emergency baseline versions and hashes;
- active-version display;
- immutable audit history;
- cache-propagation status.

Prompt editing and activation require appropriate Admin policies. No separate approval workflow is required. Every database prompt activation, deactivation, and rollback remains auditable. Emergency baseline changes are made through code and deployment.

The Admin Settings page also manages the `SystemSetting` keys:

- `PublicProEnabled`, default `true`: enables or disables Public Pro mode without changing Pro subscriptions; and
- `AiClassificationEnabled`: global Public AI kill switch. When false, both Standard and Pro use the guided wizard. The Admin Simulator ignores both settings.

Changing either setting is audited. Disabling Public Pro creates an in-app notification/banner for affected Pro members but does not create a subscription downgrade event or change their expiry.

### 7.2 Subscription management

Admin member management can review and approve/reject Pro upgrade requests, change plan price/payment instructions, assign a plan, record payment/manual-grant state, set expiry, cancel a scheduled renewal, terminate immediately, suspend temporarily, and view audit history. Price and payment-instruction changes are audited and apply only to new requests. The UI must not directly set an effective tier. Approval creates the subscription record through the entitlement service rather than setting an effective tier directly.

When a Pro period expires without a scheduled renewal:

1. effective access becomes Standard;
2. a `SubscriptionDowngradeEvent` is recorded;
3. an in-app notification is created;
4. email notification is sent when enabled;
5. duplicate notifications are suppressed.

The Admin subscription area includes a lifecycle-job monitor showing recent run history, last successful run, status, processed counts, and sanitized failure details. Authorized Admins can select **Run now** to invoke the same idempotent processor used by the scheduled background job. Scheduled and manual runs use a database-backed single-run lock; manual execution records the Admin actor and result. Access still follows the start/expiry timestamps if the job is delayed, and failed runs can be retried without duplicate records or notifications.

When an Admin changes a member's plan before expiry, any existing scheduled renewal is cancelled, the previous active row is terminated with a recorded reason, and a new active or scheduled subscription row is created in one transaction. A member-initiated early Pro termination returns the member to Standard, cancels any scheduled renewal, and records the reason. Confirmed account termination also disables Public sign-in and closes any scheduled renewal. All changes include an actor and notes in `SubscriptionChangeEvent`.

### 7.3 Membership and subscription lifecycle matrix

| Scenario | Existing record | New/changed record | Effective access |
| --- | --- | --- | --- |
| Public member registers | None | Create Standard `Active`, `StartedAt = registration time`, `SubscriptionExpiryDate = null` | Standard |
| Member submits Pro upgrade request | Standard remains `Active` | Create `SubscriptionUpgradeRequest: AwaitingPayment`, then `PaymentSubmitted` with transaction reference | Standard until Admin approval |
| Admin rejects payment | Standard remains `Active` | Request becomes `Rejected`; record Admin reason/note | Standard |
| Admin approves first Pro term | Standard becomes `Terminated` at Pro start, with reason `Upgraded to Pro` | Create Pro `Active`, `IsPaid = true`, `StartedAt = approval time`, expiry = start + 12 calendar months | Pro; Standard mode is also selectable |
| Admin grants Pro without payment | Standard becomes `Terminated` at Pro start | Create Pro `Active`, `ManuallyGranted = true`, `StartedAt` and 12-month expiry; audit grant reason | Pro; Standard mode is also selectable |
| Member renews Pro early | Current Pro remains `Active` through its expiry | After Admin verifies payment, create next Pro `Scheduled`, starting exactly at current expiry and ending 12 calendar months later | Current Pro until expiry; scheduled row grants no early access |
| Scheduled renewal is cancelled before start | Current Pro remains `Active` | Scheduled row becomes `Cancelled`, with actor/reason recorded | Current Pro until its original expiry |
| Pro term ends with no scheduled renewal | Pro becomes `Expired` at its scheduled expiry | Create one Standard lifetime `Active` row | Standard |
| Scheduled renewal reaches its start | Previous Pro becomes `Expired` at the same boundary | Scheduled Pro becomes `Active`; lifecycle job materializes transition idempotently | Pro |
| Member terminates Pro early | Pro becomes `Terminated`; set `TerminatedAt` and preserve original expiry; any future Pro `Scheduled` row becomes `Cancelled` | Create one Standard lifetime `Active` row; record actor/reason/note; no automatic refund | Standard immediately; no future Pro renewal |
| Account is temporarily suspended for suspected fraud | Current `Active` and future `Scheduled` rows become `Suspended` | Disable Public sign-in; record previous statuses, reason, and note. If cleared, restore the previous status only when its dates remain valid | No Public access while account is suspended |
| Fraud is confirmed and account is terminated | Current subscription becomes `Terminated`; scheduled subscription becomes `Cancelled` | Disable Public sign-in and retain all history/audit events | No Public access |
| Member deletes Public account | Current subscription becomes `Terminated`; scheduled subscription becomes `Cancelled`; open upgrade requests become `Withdrawn` | Delete identity; set retained subscription, upgrade-request, and inquiry user links to null. Keep message/history records until their retention or Admin deletion | No Public access |

There is at most one `Active` subscription and one future `Scheduled` subscription per member. A Standard member has one lifetime Standard record; while Pro is active, that historical Standard row is not kept active in parallel. Early termination records actual end time in `TerminatedAt` and does not rewrite the original planned expiry. A member may submit an early-termination request; an Admin records the status change and note, with no automatic refund.

## 8. Admin CrystalBall Simulator

The simulator is available at `/admin/crystal-ball-simulator` to authorized Admin users.

| Persona | AI | Persistence | Access |
| --- | --- | --- | --- |
| Non-member | None in public-style mode | Disabled | Guided wizard only; AI input may be displayed as a locked Member feature |
| Standard | Standard prompt | Disabled by default | Educational output only |
| Pro | Pro prompt by default, or Standard when selected | Disabled by default | Full internal advisory simulation |

The simulator ignores `PublicProEnabled` and `AiClassificationEnabled`. It may select any draft, active, inactive, emergency, or archived prompt version for testing. This override is never accepted by Public runtime requests and never enters the Public prompt cache.

The simulator must use a typed, immutable execution context containing persona, effective access, prompt-version override, persistence permission, disclosure mode, and correlation ID.

Switching persona or prompt version must cancel pending requests, clear all derived pathway state, clear inspector data, reset to Step 1, and create a new session ID.

CrystalBall UI should be shared through a reusable Razor Class Library or clearly separated shared UI layer. Public and Simulator adapters must provide their own data, authentication, navigation, AI, and persistence behavior. Admin authentication must never be treated as the simulated Public member. The existing `DecisionPathwayContent.razor` remains unchanged and is used only as a reference for reusable Gemini, fallback, and deterministic-engine patterns.

The inspector may show prompt text, raw response, latency, model, token usage, confidence, and engine decisions only to authorized Admin users. Define redaction, retention, and audit rules before enabling raw-response inspection.

## 9. Public CrystalBall integration

`CrystalBall.razor` must receive a server-created execution context or equivalent adapter. It must not calculate Pro access using `IsPaid` alone. The existing `DecisionPathwayContent.razor` is not rewritten as part of this phase.

### Non-member

- guided wizard only;
- no AI question box;
- no AI request;
- no member-history persistence.

### Standard

- Standard educational badge and disclosure;
- Standard prompt only;
- advisory fields hidden and rejected server-side;
- vertically stacked Pro upgrade callout;
- downgrade notice shown when the member was previously Pro.

### Pro

- Pro advisory badge and required disclosures;
- Pro prompt only while the Pro subscription is valid, Public Pro is enabled, and Pro mode is selected;
- Standard mode is always available to a Pro member as an explicit educational fallback;
- advisory output generated only through validated server-side policy;
- automatic downgrade to Standard only when the subscription entitlement expires or becomes invalid.

The Pro member mode selector defaults to Pro:

```text
Current subscription: Pro
[ Pro analysis ] [ Standard educational analysis ]
```

Selecting Standard changes only the current runtime mode. It does not change the subscription, expiry date, or plan history. Standard members see Pro as a locked/upgrade option, and non-members receive the guided wizard only.

When Pro is disabled globally, the subscription remains Pro but the Public runtime selects Standard mode and displays `Standard mode — Pro analysis temporarily unavailable`. This is a runtime fallback, not a subscription downgrade.

## 10. Inquiry interpretation and impact analysis

CrystalBall is an event-to-decision planning system, not a general-purpose chatbot. Every user inquiry follows the same reusable pipeline:

```text
Capture → classify → clarify → resolve entities → map impacts → link portfolio → present scenarios → follow through
```

### 10.1 Inquiry classification

The first structured AI response must identify:

- input type: fact claim, news claim, question, opinion, hypothesis, or advice request;
- intent: explain, assess impact, check portfolio, explore opportunity, compare scenarios, monitor, or request advice;
- scope: global, country, Malaysia, sector, stock, or portfolio;
- countries, markets, stocks, sectors, subsectors, commodities, currencies, rates, and policies;
- confidence and ambiguity;
- verification status;
- whether the user needs clarification.

The system must ask at most one focused clarification question at a time. If the user’s purpose is clear, skip clarification.

### 10.2 Multi-purpose selection

After interpretation, the UI presents a database-managed multi-select purpose selector. Users may initially select up to three purposes:

- Explain what happened;
- Impact on Malaysia;
- Impact on my portfolio;
- Affected sectors or stocks;
- Risks and opportunities;
- Compare scenarios;
- Set an alert;
- Continue with CrystalBall planning;
- **I’m not sure — guide me**.

The final option is an explicit discovery mode. OMM selects up to three high-relevance findings based on materiality, Malaysia relevance, portfolio exposure, confidence, and available evidence. It must not randomly suggest stocks.

Purpose definitions, display labels, ordering, active status, and descriptions are stored in an `InquiryPurpose` master table. AI may suggest or map to existing purpose IDs, but it must not create new purposes.

### 10.3 Four user-facing analysis stages

The existing eight internal decision fields remain available to the deterministic engine, but the user-facing experience is reduced to four adaptive stages:

1. **Understand:** question, event, entities, intent, and missing information;
2. **Analyze:** transmission chain, Malaysia relevance, positive/negative/mixed effects, and scenarios;
3. **Relate to me:** portfolio facts, sector exposure, concentration, and user-selected risk context;
4. **Follow through:** deeper analysis, opportunity research, CrystalBall planning, monitoring, alert, or save.

Simple questions should complete in two screens. Clarification adds one screen. Portfolio or Pro follow-through may add a fourth screen. The existing eight-step pathway should not open automatically for every inquiry.

### 10.4 Global-to-Malaysia impact map

The system maps events through approved relationships:

```text
Event → mechanism → commodity/rate/currency/trade flow → affected countries
      → Malaysia transmission channel → Malaysian market → sector/subsector
      → stock → user portfolio exposure
```

Every impact link includes direction (`Positive`, `Negative`, `Mixed`, or `Uncertain`), strength, time horizon, confidence, assumptions, evidence, and Malaysia relevance.

Master data for Country, Market, Sector, SubSector, Stock, exchange, currency, and portfolio holdings is used for deterministic linking. Foreign holdings, including US stocks, may be linked through their country, exchange, market, sector, and approved transmission relationships. If a holding or relationship is unsupported, the result must say that no approved material relationship was identified rather than inventing a Malaysian connection. The AI must not invent an unrelated connection.

Example: a crude-oil event may affect energy, transportation, logistics, utilities, manufacturing, and inflation-sensitive sectors. It must not automatically suggest an entertainment stock.

### 10.5 Scenario planning

For uncertain events, OMM presents multiple conditional branches:

- worst-case scenario;
- base or most plausible scenario;
- optimistic scenario;
- partial, delayed, or resolved scenario when relevant.

Each scenario includes triggers, transmission effects, Malaysia impact, portfolio sensitivity, risks, evidence that would confirm or disprove it, and what to monitor. Do not invent numerical probabilities without reliable supporting data. Use qualitative labels such as `Higher concern`, `Plausible`, `Lower concern`, or `Insufficient evidence`.

Unverified claims are not blocked when they may be financially material. They are labelled as `Unverified user claim` or `Scenario only`, sanitized to remove unnecessary explicit details, and analysed conditionally. The system must not present allegations as facts.

## 11. Risk context and personalized presentation

Risk context is collected when the user requests portfolio analysis, opportunity analysis, or Pro advisory output. It should not block an initial educational answer.

The minimum user selections are:

- approach: capital preservation, balanced, growth, or high risk;
- objective: income, growth, capital preservation, or opportunity research;
- time horizon: short term, several months, or long term.

Pro suitability workflows may collect additional risk-capacity information, including financial ability to absorb loss, liquidity needs, debt, and maximum tolerable drawdown.

For Standard mode, risk context may change presentation emphasis only. It must not rank securities, generate actions, change the underlying facts, hide adverse scenarios, or turn Standard output into investment advice. Educational questions remain available to first-time investors without requiring portfolio or risk-profile data. For personalized Pro advice, the required suitability context is validated server-side and stored as a risk-context snapshot with its capture date. Do not require the member to review and confirm the snapshot before every use; allow them to update it at any time. If required context is missing for a personalized request, ask only for the next necessary fact and do not invent it or present the result as personalized advice.

The final output must clearly state that scenarios are possibilities, not predictions, and that the user makes the final decision.

## 12. Alerts and follow-through

OMM must not create alerts automatically from an AI response. The user must explicitly choose `Monitor this` or `Create alert` and confirm the condition.

Phase 5 initially creates only user-confirmed alert/monitoring records. Evaluation and notification require a later market/news data and background-processing capability.

Alert rules may eventually monitor:

- verified event status;
- stock price or percentage movement;
- sector or portfolio exposure;
- dividend or corporate-action dates;
- material changes from approved sources.

User-entered claims may create a private, user-confirmed scenario-monitoring record, but must not be presented as verified news. Existing notification infrastructure and deduplication keys should be reused.

## 13. Admin-managed taxonomy and impact rules

Admin configuration must control the routing and impact graph, not only prompt wording. Rules must be seeded in the database, versioned, effective-dated, auditable, and tested before activation. Admins must not edit compliance policy or response schemas.

Admin-managed configuration includes:

- event taxonomy: geopolitical, tariff, interest rate, energy, M&A, earnings, dividend, currency, supply chain, regulatory, and other approved types;
- keywords, aliases, and example questions;
- entity relationships between countries, commodities, mechanisms, markets, sectors, subsectors, and stocks;
- positive, negative, mixed, and no-material-impact rules;
- Malaysia relevance and materiality thresholds;
- clarification questions;
- routing defaults and permitted next actions;
- Standard response templates for positive, negative, mixed, uncertain, and low-impact results;
- sensitive public-interest events such as leadership resignation, corruption investigation, election, governance issue, or regulatory action.

Admin changes require authorization, validation, audit logging, versioning, and rollback. Rules must be testable in the Admin Simulator. Phase 5A may start with a limited seeded rule set; the broader taxonomy CRUD and impact graph are Phase 5B.

For example:

```text
Crude oil price increase
  → transportation: potentially negative
  → airlines: potentially negative
  → logistics: potentially negative
  → upstream energy: potentially positive
  → entertainment: no default relationship
```

## 14. Safety, relevance, and sensitive-event handling

The first classification must distinguish irrelevant unsafe content from sensitive but financially material public-interest events.

Block or redirect content that is purely pornographic, sexual roleplay, exploitative, involves minors, or has no market relevance. Do not block an event merely because it contains terms such as `sexual`, `scandal`, or `corruption`.

When a sensitive allegation concerns a company executive, founder, president, policymaker, or other public figure, OMM may analyse conditional business and market consequences. It must:

- label the claim as unverified unless supported by an approved source;
- avoid repeating unnecessary explicit details;
- use `alleged`, `reported`, `if confirmed`, and `if denied` wording;
- analyse governance, leadership continuity, legal, regulatory, reputation, policy, election, and market channels;
- offer monitoring of confirmation from official or trusted sources;
- never declare guilt or treat an allegation as fact.

If a user asks whether an event is true, AI classification is not proof. OMM must show the verification status and approved source evidence when available. A historical date supplied by the user does not by itself make a claim verified; if no source is available, analyse it as a conditional scenario.

## 15. Inquiry behaviour analytics

Add append-only inquiry telemetry separate from security/audit logs. Reuse existing notification and audit infrastructure where appropriate, but do not use Admin audit logs as the sole analytics store.

Required logical records:

### `InquirySession`

Stores nullable Public user/session context, entry point, tier, anonymous status, flow version, start time, and completion time. If a Public account is deleted, set its inquiry-session `UserId` to null and retain the inquiry content through the configured retention period; account deletion does not cascade-delete inquiry records.

### `InquiryTurn`

Stores the original prompt, privacy-safe prompt hash, turn number, detected intent/event type, verification status, confidence, selected prompt version, emergency-prompt usage, requested/effective mode, fallback reason, AI-call attempt count, and timestamp. Raw messages are available to all authenticated Admin accounts through the Admin application; Public users and Public application routes cannot retrieve them. Access, exports, and Admin deletion actions are audited. Deleting a member account does not delete raw messages; the associated inquiry-session `UserId` is set to null. Admins may delete raw messages before the three-year retention limit. The deletion audit record must not retain the deleted message text. Original prompts are personal-data-sensitive and require encryption and retention handling. They must not be copied into ordinary application logs or used for model fine-tuning.

### `InquiryEntityLink`

Stores resolved entity type, entity ID, resolution method, confidence, and whether the user confirmed the entity.

### `InquirySelectionEvent`

Stores selected purposes, clarification choices, expanded sections, selected next actions, alert requests, and CrystalBall/legacy Decision Pathway launches.

### `InquiryOutcome`

Stores final route, clarification count, abandonment point, completion status, alert creation, saved analysis, and pathway completion.

Analytics should support improving UI/UX, routing, Admin rules, prompts, and test cases. Do not automatically use production prompts for model fine-tuning. Full inquiry records, including raw member messages, are retained for `3 years`, with the retention period stored in Admin configuration so it may later be changed. All authenticated Admin accounts may bulk-delete selected records; deletion is audited and must not remove the security/audit record of the deletion itself. Account deletion does not delete inquiry records; the account link is removed while message content remains until Admin deletion or the retention period ends. Define redaction and provider-data handling before enabling analytics.

## 16. AI-call budget, retries, and provider abstraction

The normal flow is bounded, but Pro does not use a permanently fixed call count. Operation budgets are controlled by a server-owned policy with Admin-configurable limits and hard upper bounds:

| Flow | AI calls |
| --- | ---: |
| Non-member wizard | 0 |
| Standard clear inquiry | 1 |
| Standard clarification | 1 additional clarification call; two total per inquiry session |
| Standard portfolio impact | 1; deterministic engine renders the impact map |
| Pro general analysis | Configurable within the server hard limit |
| Pro personalized advisory analysis | Configurable within the server hard limit |
| Alert creation and alert evaluation | 0 |
| Cached repeated classification | 0 |

Call 1 extracts intent, event, entities, ambiguity, safety/relevance status, and verification status. Deterministic services resolve entities, apply Admin rules, calculate Malaysia relevance, and link portfolio exposure. A later call is used only when the selected flow requires a narrative or advisory result after the deterministic context is validated.

Provider failure handling must preserve the completed state. Recommended defaults are two automatic provider attempts per stage, three user-triggered retries per stage, and six total provider attempts per inquiry session. These values are configurable but cannot exceed hard server-side maximums. A failed second call must not discard a valid first-call classification or deterministic impact map; the user may continue with the partial result or retry only the failed stage.

Use an `IAiProvider` abstraction so Gemini can be replaced later. Do not make one AI call per country, sector, stock, or impact branch. Use bounded retries, per-user quotas, timeouts, circuit breaking, and prevent retry loops.

## 17. Failure and cold-start behavior

| Failure | Required behavior |
| --- | --- |
| Prompt database unavailable | Preserve verified entitlement; use the matching emergency JSON resource |
| Active Standard prompt and Standard emergency resource missing | Guided flow remains available; Standard AI unavailable |
| Active Pro prompt and Pro emergency resource missing | Use the safe Standard prompt with an explicit Standard-fallback badge and log the reason; never render Pro advisory fields. If the global AI switch is off, use guided flow only |
| `PublicProEnabled = false` | Preserve Pro subscription; select Standard prompt and show `Standard mode — Pro analysis temporarily unavailable` |
| `AiClassificationEnabled = false` | Disable Public AI for all members; retain guided wizard; Admin Simulator is unaffected |
| Subscription state unavailable | Use bounded last-known entitlement; otherwise show a temporary unavailable state |
| Gemini timeout/error | Safe unavailable result; guided flow remains available |
| Invalid model JSON | Reject response; do not render partial advisory output |
| Subscription period ends | Use a due scheduled renewal if present; otherwise effective tier becomes Standard and notification is generated |
| Subscription lifecycle job fails | Effective access still follows the start/expiry timestamps; failure is recorded in Admin job history and the processor can be retried manually |
| Cache invalidation failure | Do not claim instant activation; expose stale-state telemetry and fail closed for unsafe changes |

## 18. Verification requirements

Add unit, integration, and browser coverage for:

- effective-tier calculation;
- database-driven plan-to-prompt-mode mapping and future-plan handling;
- early renewal scheduling, scheduled-record cancellation, term-end activation, immediate termination, and Standard fallback;
- expiry downgrade and notification deduplication;
- scheduled and Admin-triggered lifecycle job history, failure visibility, single-run locking, manual retry, and idempotent results;
- one-active-subscription enforcement and plan-change history;
- `PublicProEnabled` and `AiClassificationEnabled` behaviour;
- Pro user Standard-mode selection and labelled fallback behaviour;
- Admin Simulator Pro isolation;
- anonymous no-AI behavior;
- Standard advisory-field rejection;
- prompt injection attempts;
- malformed and semantically invalid Gemini JSON;
- unseeded database and Gemini outage;
- prompt activation, deactivation, rollback, and concurrent activation;
- embedded emergency JSON validation, hash matching, and fallback resolution;
- cross-process cache invalidation;
- simulator persona switching and state reset;
- simulator non-persistence;
- simulator bypass of Public feature settings;
- AI retry limits, second-call failure recovery, and partial-result continuation;
- required disclosures;
- Public and Admin authorization boundaries.
- inquiry classification and one-question clarification behavior;
- multi-purpose selection and `I’m not sure — guide me` mode;
- event-to-Malaysia transmission rules and unrelated-sector exclusion;
- verified versus unverified scenario labeling;
- worst-case, base, optimistic, and resolved scenario rendering;
- risk-context capture and snapshotting;
- alert confirmation and alert deduplication;
- sensitive public-interest event sanitization and irrelevant-content blocking;
- inquiry telemetry redaction, retention, and abandonment tracking;
- one-call Standard and bounded configurable Pro budgets;
- foreign-holding entity resolution and no-approved-relationship handling;
- bulk inquiry-log deletion with retained deletion audit events.

## 19. Implementation order

### Phase 5A — Foundation and CrystalBall vertical slice

1. Add the database-driven `AiPromptMode`, `SubscriptionPlan`, `MemberSubscription`, `SubscriptionDowngradeEvent`, `AiPromptVersion`, `AiPromptAuditEvent`, `InquiryPurpose`, and initial inquiry-log models. `OMM.Public` owns the migrations.
2. Add `PublicProEnabled` and bounded AI retry/budget settings to the existing `SystemSetting` table. Keep `AiClassificationEnabled` as the global AI kill switch.
3. Implement effective entitlement, requested-mode, effective-runtime-mode, expiry conversion, one-active-subscription enforcement, and notification idempotency.
4. Add the scheduled subscription lifecycle processor, persistent run history, Admin job monitor, and authorized **Run now** action using the same idempotent processor.
5. Implement prompt activation/deactivation/rollback, read-only emergency baseline inspection, schema validation, and 30-second revision-token cache refresh.
6. Implement the shared execution context, `IAiProvider`, prompt provider, Standard response validator, Pro response validator, and partial-result failure handling.
7. Build `CrystalBall.razor` as a shared UI component with Public and Admin Simulator adapters. Leave `DecisionPathwayContent.razor` unchanged.
8. Implement the initial UX: question capture, database-managed purpose suggestions, up to three purpose selections, `You tell me`, Standard/Pro mode selection, safe fallback badges, deterministic Malaysia/portfolio summary, and scenario display.
9. Build the Admin Simulator with Non-member, Standard, and Pro personas, prompt-version overrides, inspector, state reset, and feature-setting bypass.
10. Seed the initial Standard and Pro database prompts; include the Standard and Pro emergency baseline JSON resources in `OMM.Shared`.
11. Add foundation unit, integration, browser, failure-mode, security, and compliance-boundary tests for Public Pro behavior.

### Phase 5B — Inquiry pipeline and Admin rules

1. Expand the AI classification schema and state machine.
2. Add full entity resolution for Malaysian and supported foreign holdings.
3. Add versioned Admin taxonomy, entity relationships, transmission mechanisms, impact rules, and Malaysia-materiality rules.
4. Replace temporary seeded impact rules with the versioned deterministic impact graph.
5. Add scenario triggers, evidence/provenance, and verified-versus-unverified event handling.
6. Add the five normalized inquiry analytics tables and three-year configurable retention.
7. Add risk-context capture and Pro suitability validation.
8. Add user-confirmed alert-record creation. Defer live evaluation to the market/news monitoring phase.
9. Add broader Admin rule-testing screens and CrystalBall UX refinement.
10. Extract any remaining reusable UI pieces into a Razor Class Library if the initial shared component is not already in one.

The existing Decision Pathway remains available during Phase 5A and 5B. It may be redirected or retired only after CrystalBall supports required scenarios, preserves historical records, and passes production regression, accessibility, and mobile checks.

Out of scope for this phase: A/B testing, payment-gateway automation, automatic licensing-status retrieval, and live trading/execution.
