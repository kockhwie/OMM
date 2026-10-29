# Phase 5 Database Design and Task Breakdown

Status: implementation baseline derived from the Phase 5 architecture decisions. `OMM.Public` owns migrations for the shared `public` schema. This document makes the table boundaries, integrity rules, and delivery order concrete before the feature work expands.

## 1. Database conventions

- PostgreSQL is the source of truth. Store timestamps as `timestamptz` in UTC and use `DateTimeOffset` in .NET.
- Use generated `bigint` keys for history/transaction tables and stable `text` business codes for lookup rows.
- Persist lifecycle/status values as bounded `text` with database `CHECK` constraints. Keep the matching .NET values in shared contracts; do not create PostgreSQL enum types for values expected to evolve.
- Use `numeric(12,2)` for prices and currency amounts, uppercase ISO currency codes, and explicit `CHECK` constraints for non-negative amounts.
- New schema entities/contracts used by both applications live in `OMM.Shared`. EF mappings and migrations live in `OMM.Public`. Admin maps the same `public` tables through its existing `MasterDataDbContext`, with no Admin migration history and no relationship to `admin.AspNetUsers`.
- Public identity foreign keys are created only by the Public model/migration. Use nullable `text` user IDs with `ON DELETE SET NULL` on retained history. Never cascade-delete billing, audit, or inquiry history with an account.
- Business operations that change multiple rows (registration provisioning, payment approval, termination, renewal activation, downgrade) run in a database transaction and write an audit event in that transaction.

## 2. Phase 5A tables

### Configuration and prompts

| Table | Purpose and important fields |
| --- | --- |
| `AiPromptMode` | `Code` PK, `DisplayName`, `IsAdvisory`, `IsActive`, server-owned `SchemaVersion`. Seed `STANDARD` and `PRO`. Advisory capability and response schema are code-controlled. |
| `SubscriptionPlan` | `Id` PK, unique `Code`, display/description, `PromptModeCode` FK, nullable `DurationMonths`, nullable `PriceAmount`/`CurrencyCode`, `PaymentInstructions`, `IsActive`, `SortOrder`, timestamps. Seed Standard (lifetime, no price) and Pro (12 months, RM99/MYR, editable by Admin). |
| `AiPromptVersion` | `Id` PK, `PromptModeCode` FK, version label, system/user prompt text, server-owned schema version, content hash, status (`Draft`, `Active`, `Inactive`, `Archived`), notes and creator/timestamp. Unique partial index allows one `Active` row per prompt mode. |
| `AiPromptAuditEvent` | Append-only prompt lifecycle event: prompt ID, action, actor type/ID, timestamp, notes, and optional JSONB change metadata. Retain independently of prompt deactivation. |
| `SystemSetting` additions | Reuse the existing table for `PublicProEnabled` (seed true), `InquiryRetentionYears` (seed 3), existing AI kill switch, and bounded retry/budget settings. Do not duplicate global configuration in new tables. |

### Membership and payment

| Table | Purpose and important fields |
| --- | --- |
| `MemberSubscription` | `Id` PK; nullable Public `UserId`; `SubscriptionPlanId`; `IsPaid`; `ManuallyGranted`; `Status`; `EntitlementSlot`; `StartedAt`; nullable `SubscriptionExpiryDate`; nullable `TerminatedAt`; created/updated actor and timestamps; nullable payment reference. |
| `SubscriptionUpgradeRequest` | `Id` PK; nullable Public `UserId`; requested plan; immutable `AmountDue`/`CurrencyCode` snapshot; status (`AwaitingPayment`, `PaymentSubmitted`, `Approved`, `Rejected`, `Withdrawn`); nullable member transaction reference; created/submitted/reviewed timestamps; reviewer ID and review notes; optional resulting subscription ID. |
| `SubscriptionChangeEvent` | Append-only subscription change audit: nullable user link, subscription ID, action, actor type/ID, timestamp, reason, `Notes`, and JSONB previous/new state. Restrict deletion of the referenced subscription. |
| `SubscriptionDowngradeEvent` | Idempotent expiry/downgrade notification record keyed uniquely by source subscription transition; previous/resulting plan, reason, detected/expiry timestamps, notification state and timestamp. |
| `SubscriptionLifecycleJobRun` | Persistent scheduled/manual job run: start/completion timestamps, trigger and Admin ID, status, processed counters, correlation ID, sanitized error summary. |

`EntitlementSlot` is a database integrity aid: `Current`, `Scheduled`, or null. A unique partial index on `(UserId, EntitlementSlot)` where both are non-null prevents multiple current/scheduled records even while a subscription is `Suspended`. `Active` requires `Current`; `Scheduled` requires `Scheduled`; a suspended record keeps its slot; historical states (`Expired`, `Cancelled`, `Terminated`) have a null slot. This avoids relying on application-only checks for the “one current plus one future renewal” rule.

Additional constraints/indexes:

- `MemberSubscription`: non-negative term (`Expiry > StartedAt` when expiry is present); Pro rows require finite expiry (enforced in domain service because plan mapping is cross-table); `IsPaid` and `ManuallyGranted` remain independent facts.
- Index subscriptions by `(UserId, Status, StartedAt)` and `(Status, SubscriptionExpiryDate)` for member entitlement and lifecycle scans.
- `SubscriptionUpgradeRequest`: non-negative amount, valid currency code, and payment reference required in `PaymentSubmitted`; one open request per `(UserId, SubscriptionPlanId)` using a partial unique index over `AwaitingPayment`/`PaymentSubmitted`.
- `SubscriptionUpgradeRequest` approval has a unique idempotency guarantee: one resulting subscription per approved request.
- Preserve plan price on the request; editing the plan affects only new requests.
- Create the Standard lifetime row with the Public account as part of successful registration provisioning. A successful first Pro approval terminates the Standard row and creates the Pro current row atomically.

### Initial inquiry persistence

| Table | Purpose and important fields |
| --- | --- |
| `InquiryPurpose` | Admin-managed/seeded purpose code, localized labels/descriptions, ordering and active state. AI can select existing purpose IDs but cannot create rows. |
| `InquirySession` | `Id` PK, nullable Public `UserId`, entry point, tier/mode, anonymous flag, flow version, started/completed timestamps, `ExpiresAt` set to creation + three years. User deletion sets `UserId` null and retains the record. |
| `InquiryTurn` | `Id` PK, session FK, turn number, intent/event classification, verification/confidence, prompt version/emergency usage, requested/effective mode, fallback reason, bounded AI attempt count, timestamp, prompt hash. Unique `(SessionId, TurnNumber)`. |
| `InquiryRawMessage` | One-to-one with a turn; raw member text and creation/expiry timestamps. Store separately so an Admin can delete message text while retaining non-content analytics. Public runtime has no read path; all retrieval/export/deletion goes through Admin authorization and audit logging. |

Use `ON DELETE CASCADE` only from `InquirySession` to its `InquiryTurn`/raw-message child data when an Admin explicitly deletes inquiry records. Deleting a member account only nulls the session's user link. Admin deletion audit entries contain actor, operation, row IDs/count, and timestamp, never message text.

Retention is three years from message/session creation. A background cleanup removes expired inquiry content and records according to the retention policy; Admin may delete earlier. Provider-call payloads and raw messages must not be copied to ordinary logs or training datasets. Any application-level encryption choice must be made against the deployed key-management setup before storing sensitive message bodies; database access restrictions alone do not encrypt the value.

The normalized Phase 5B analytics tables (`InquiryEntityLink`, `InquirySelectionEvent`, `InquiryOutcome`) are separate follow-up work. Do not block membership and prompt foundation migrations on them.

## 3. Lifecycle invariants

1. New Public account: create one active Standard lifetime subscription (`Current`, null expiry).
2. Upgrade request: Standard remains active until Admin approves verified payment.
3. First Pro approval: Standard becomes `Terminated` with null slot; create paid Pro `Active`/`Current`, one-year term.
4. Early Pro renewal approval: create Pro `Scheduled`/`Scheduled`, starting at current expiry; no auto-renewal and no early access.
5. Due renewal: transaction expires old term and makes scheduled term current. If none is scheduled, expire Pro and create a new Standard lifetime current row.
6. Member early termination: terminate Pro, clear its slot, cancel/clear any scheduled renewal, create Standard lifetime current row; preserve original expiry and do not issue an automatic refund.
7. Suspension: set current and any scheduled records to `Suspended` while preserving their slots. Resume only records whose date windows remain valid. Public sign-in is disabled/restored through the existing Identity account controls.
8. Confirmed fraud/account termination: terminate current and scheduled records, clear slots, disable Public sign-in, and retain history.
9. A manual Pro grant sets `ManuallyGranted=true`, records Admin actor/notes, and always has a finite Pro term. `IsPaid` remains false unless payment was actually verified.

## 4. EF Core ownership and access

- Public `ApplicationDbContext` owns model snapshots and all migrations for the new tables in schema `public`.
- Add the FK from subscription/request/session `UserId` to `public.AspNetUsers.Id` in the Public model. Retained records use `ON DELETE SET NULL`.
- Admin `MasterDataDbContext` maps only the tables it needs for Admin screens/services and uses the same database. It does not create a second migration stream. Admin actor IDs are strings because they belong to schema `admin`.
- Shared entities and business contracts go in `OMM.Shared`; project-specific pages, identity operations and Admin authorization remain in their owning apps.
- Never expose raw-message entities/repositories to Public routes. Admin raw-message access/export/delete must be policy-protected and audited.

## 5. Delivery tasks and dependencies

| Task | Scope | Depends on |
| --- | --- | --- |
| DB-1 — Shared contracts and Public schema | Add shared entity/contracts and Public EF mappings for prompt modes, plans, subscriptions, upgrade requests, change/downgrade events, job runs, purposes, prompt versions/audit, initial inquiry session/turn/raw-message tables; add constraints, indexes, seeds and migration. | None; this is the first coding slice. |
| DB-2 — Account provisioning and entitlement | Create Standard row on registration; implement authoritative subscription resolver, Pro/Standard gates, account-deletion retention behavior. | DB-1 |
| DB-3 — Upgrade, renewal and Admin subscription operations | Member request/reference flow, Admin review/approval/rejection/manual grant, transaction-safe transitions, audit notes. | DB-1, DB-2 |
| DB-4 — Lifecycle job and Admin run history | Idempotent expiry/renewal/downgrade processor, persistent run status, Admin view and Run Now. | DB-1, DB-2; DB-3 for scheduled renewals |
| DB-5 — Prompt administration foundation | Prompt version editing/activation/audit, emergency resources read-only, revision invalidation. | DB-1 |
| DB-6 — Inquiry storage and Admin access | Persist sessions/turns/raw messages, three-year retention, Admin read/export/delete and audit; no Public read endpoint. | DB-1 and Admin authorization conventions |
| APP-1 — CrystalBall domain and Public flow | Provider abstraction, classification, deterministic result orchestration, disclosure enforcement and Public UX. | DB-1, DB-2, DB-5 |
| APP-2 — Admin Simulator | Admin personas and prompt simulation using shared domain contracts, with no Public feature-setting bypass leakage. | DB-1, DB-5; may proceed alongside APP-1 after contracts stabilize |
| APP-3 — Phase 5B analytics and rules | Entity links/selections/outcomes, versioned Admin rules, impact graph and additional inquiry flow. | APP-1, DB-6 |

### First coding task boundary

DB-1 is the first implementation task. It establishes the durable schema only: shared entities/contracts, Public mappings, migration, seed data, and Admin read/write mappings for tables needed immediately. It does not implement pages, payment approval, lifecycle processing, prompt execution, or inquiry analytics behavior. After DB-1 is reviewed, DB-2 through APP-3 can be implemented as bounded follow-up tasks without overlapping migration ownership.
