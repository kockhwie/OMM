# OMMv2 Phase 5 Handoff — 2026-10-30

## Status

This is an uncommitted working-tree handoff. Phase 5 foundation work DB-1 through DB-6 has been implemented, followed by partial APP-1, APP-2, and APP-3 work. The current agent has stopped coding here.

No tests were run in the final work session. The user explicitly requested that build/test work stop so coding could continue. Earlier in the session, a build of `OMM.Integration.Tests` succeeded with 0 warnings and 0 errors before the last APP-3 changes; the latest changes have not been compiled.

## Read first

1. `docs/phase-5-production-system-prompts-and-revised-architecture.md`
2. `docs/phase-5-database-design-and-task-breakdown.md`
3. `AGENTS.md`
4. `.agents/AGENTS.md`
5. The original external handoff at `C:\Users\User\.gemini\antigravity\brain\957646c3-3982-406f-9434-70afb11fce8e\phase5-handoff.md`
6. This document, then `docs/phase-5-next-agent-prompt-2026-10-30.md`

## Completed work

### DB-1 through DB-4

- Added shared Phase 5 entities and contracts in `OMM.Shared/Models/Phase5` and `OMM.Shared/Subscription`.
- Added Public-owned EF mappings and migrations for prompt modes, plans, subscriptions, upgrade requests, change/downgrade events, lifecycle job runs, inquiry purposes, prompt versions, and initial inquiry session/turn/raw-message tables.
- Added Standard account provisioning, entitlement resolution, Pro/Standard runtime mode evaluation, upgrade/payment review operations, lifecycle processing, scheduled/manual run history, and Admin subscription screens.
- Public owns all migrations. Admin maps shared `public` tables through `MasterDataDbContext` and has no competing migration stream.

### DB-5 prompt administration

- Added `IPromptProvider` and prompt administration services in `OMM.Shared/Prompts`.
- Added immutable draft/activation/deactivation/rollback/archive handling and audit records.
- Added embedded Standard and Pro emergency prompt resources with content-hash validation.
- Added Admin prompt page at `/admin/ai-prompts`.
- Added revision-token based cache refresh support.

### DB-6 inquiry storage and protection

- Added Public inquiry capture at `OMM.Public/Services/InquiryCaptureService.cs`.
- Raw messages are protected with shared ASP.NET Data Protection configuration. Public and Admin use the same environment-qualified application name and purpose. Production requires a shared durable key ring and private-key certificate configuration.
- Public stores ciphertext and a hash of the ciphertext. Public has no raw-message read route.
- Admin has inquiry listing, audited decrypt/read, export, delete, and retention cleanup support.
- Account deletion severs inquiry user links while retaining records.

### APP-1 Public CrystalBall slice

- Added `/crystalball` at `OMM.Public/Components/Pages/CrystalBall.razor`.
- Added four-stage UI: Understand, Analyze, Relate to me, Follow through.
- Added up-to-three database-managed purpose selection.
- Added entitlement-resolved Standard/Pro mode selection and the required labels:
  - `Educational information only`
  - `Personalized Pro Advice`
  - `Standard mode — Pro analysis temporarily unavailable`
- Added portfolio snapshot display with explicit missing/stale-data wording.
- Left the existing `DecisionPathwayContent.razor` unchanged.

### Shared AI provider and classifier

- Added `IAiProvider`, `GoogleAiProvider`, and `IPhase5InquiryClassifier` in `OMM.Shared/DecisionSupport`.
- Added server-owned structured classification schema, bounded provider attempts, prompt-provider resolution, emergency fallback, prompt-mode matching, context validation, member-text length/control-character handling, and unexpected-field rejection.
- Added `AiExecutionOrigin`, requested/effective mode, simulator origin, persistence permission, prompt override ID, disclosure mode, and call-budget fields to `AiExecutionContext`.
- Public rejects prompt-version overrides. Admin Simulator may use a scoped prompt-version override.

### APP-2 Admin Simulator slice

- Added `/admin/simulator` at `OMM.Admin/Components/Pages/Admin/Simulator.razor`.
- Added Non-member, Standard, and Pro personas, active/version-selected prompt execution, inspector output, reset behavior, and non-persistent Admin simulator context.
- Added Admin navigation entry.

### APP-3 analytics and rules slice

- Added shared entities and Public mappings for `InquiryEntityLink`, `InquirySelectionEvent`, `InquiryOutcome`, `InquiryTelemetryEvent`, `Phase5EventTypeRule`, and `Phase5ImpactRule`.
- Added `IInquiryAnalyticsService` and Public implementation with bounded values and object-only redacted JSON payload validation.
- CrystalBall records purpose selections, AI-suggested entity links, and completed outcomes.
- Added account-deletion anonymization for telemetry user IDs.
- Added seeded versioned rules for interest-rate, commodity, and governance/regulatory events.
- Added Public migration `OMM.Public/Data/Migrations/20261029133000_AddPhase5AnalyticsAndRules.cs`.
- Added Admin rule catalog and version-creation UI at `/admin/phase5-rules`, with validation and Admin audit logging.

## Important unverified risks

- The latest APP-3 changes were not compiled. Start with a build and fix all errors before extending the feature.
- The manually added APP-3 migration does not currently have a matching generated `.Designer.cs` file. Generate or otherwise reconcile the migration model snapshot before applying it to a database.
- The APP-3 migration and Phase 5 model seed data must be checked for PostgreSQL/EF compatibility, especially identity columns, indexes, seed values, and exact table/schema names.
- The Public CrystalBall currently records AI-suggested entity links as unresolved. Full stock/sector/country master-data resolution and approved relationship validation remain incomplete.
- The deterministic engine still contains hardcoded decision data. It has not yet been refactored to consume the new versioned impact graph.
- Sensitive-event sanitization, irrelevant-content blocking, scenario contracts, risk-context capture, alerts, and deeper follow-through are not implemented.
- Analytics retention/anonymization cleanup needs a dedicated policy review. Raw inquiry retention remains governed by the existing three-year setting; the new telemetry/entities/outcomes need explicit retention behavior.
- The latest Admin rule form currently creates standard event types; sensitive-event creation and edit/rollback/deactivation workflows need deliberate authorization and audit review.
- Existing working-tree changes are broad and uncommitted. Do not reset or discard them. Review `git status --short` before editing.

## Remaining work order

1. Compile the full solution and repair APP-3 integration errors. Do not apply migrations until the model snapshot/designer is correct.
2. Add focused tests for analytics persistence, redaction, account deletion, duplicate rules, versioning, sensitive-event handling, and simulator isolation.
3. Refactor `IDeterministicDecisionEngine` behind a rule-backed service that reads active/effective-dated `Phase5ImpactRule` rows and rejects unrelated links.
4. Implement master-data entity resolution for stocks, sectors, countries, and approved relationship checks.
5. Add sensitive public-interest event sanitization and irrelevant-content blocking.
6. Add scenario/risk-context contracts and deterministic Standard rendering; add separate validated Pro response contracts before any advisory rendering.
7. Add selection/outcome/telemetry retention and anonymization jobs, plus Admin analytics screens and policy-protected access.
8. Complete alert-intent records and explicit confirmation flow; do not create alerts automatically from AI output.
9. Review Public/Admin authorization, disclosures, Data Protection production configuration, and migration deployment behavior.
10. Run the requested integration tests only after the user asks for verification or explicitly authorizes them.

## Working-tree rules

- Do not commit or push without explicit user direction.
- Do not add a second Admin migration stream.
- Keep shared contracts/services in `OMM.Shared` when both apps may need them.
- Keep raw inquiry message retrieval restricted to Admin and audit every access/export/delete action.
- Do not pass member name, email, Public user ID, credentials, or account identifiers to an AI provider.
- Standard output must remain educational and must not render recommendations, allocations, targets, rankings, or action plans.
