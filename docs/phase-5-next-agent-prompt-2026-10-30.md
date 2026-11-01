# Prompt for the next OMMv2 Phase 5 agent

You are continuing Phase 5 implementation in `C:\Users\User\source\repos\OMMv2`.

Read these files before changing code:

1. `docs/phase-5-current-handoff-2026-10-30.md`
2. `docs/phase-5-production-system-prompts-and-revised-architecture.md`
3. `docs/phase-5-database-design-and-task-breakdown.md`
4. `AGENTS.md`
5. `.agents/AGENTS.md`

The current working tree is intentionally dirty and contains uncommitted Phase 5 work. Preserve all existing changes. Do not use `git reset`, `git checkout --`, or broad cleanup commands.

## Immediate task

Finish and stabilize APP-3, starting with verification:

1. Build `OMM.Integration.Tests/OMM.Integration.Tests.csproj` and fix every compile error introduced by the latest APP-3 changes.
2. Inspect `OMM.Public/Data/Migrations/20261029133000_AddPhase5AnalyticsAndRules.cs`. Generate/reconcile its EF designer and Public model snapshot through the normal EF workflow. Do not create an Admin migration.
3. Check all new entity/table mappings, indexes, foreign keys, seed data, identity columns, PostgreSQL JSONB columns, and `ON DELETE` behavior.
4. Only after the code is coherent, ask the user to run tests. Do not run `dotnet test` automatically unless the user explicitly authorizes it.

## APP-3 implementation goals

Complete the Phase 5B inquiry pipeline:

- Resolve classifier entities against approved Public master data. Unresolved AI suggestions must remain marked unresolved and must not enter the impact graph.
- Refactor `IDeterministicDecisionEngine` or add a rule-backed service that reads active/effective-dated `Phase5ImpactRule` rows.
- Enforce approved relationship traversal and prevent AI from inventing unrelated sectors, stocks, countries, commodities, currencies, policies, or rates.
- Add deterministic impact-map output containing direction, strength, time horizon, confidence, assumptions, evidence/source status, and Malaysia relevance.
- Add Standard safe rendering using an allowlisted educational schema. Reject or safely summarize advisory fields; do not use regex-only stripping.
- Define and validate a separate Pro response contract before rendering any Pro advisory content. Server validation must cover entitlement, security identity, freshness/source, currency/price validity, suitability inputs, numeric relationships, and required disclosures.
- Add sensitive public-interest event sanitization for governance, corruption, election, leadership, and regulatory events.
- Add irrelevant-content blocking and a safe guided fallback.
- Add retention/anonymization cleanup for analytics entities and telemetry. Preserve audit records while removing identity links according to policy.
- Add Admin read/reporting screens for analytics with explicit authorization and no raw message leakage.
- Keep alert creation explicit and user-confirmed. AI output must never create an alert automatically.

## Required invariants

- Public owns all EF migrations for shared `public` schema tables.
- Admin maps shared public tables but does not create migration history.
- Public prompt requests cannot accept browser-selected prompt version IDs.
- Admin Simulator may use a prompt override only in `AiExecutionOrigin.AdminSimulator`; simulator work must not persist inquiry sessions or alter Public feature settings.
- Non-members are guided-wizard only and receive no AI classification.
- `AiClassificationEnabled=false` disables Public AI classification but leaves guided flow available.
- `PublicProEnabled=false` preserves Pro entitlement but forces Standard runtime mode with exact label `Standard mode — Pro analysis temporarily unavailable`.
- Standard exact disclosure is `Educational information only`.
- Pro exact label is `Personalized Pro Advice`.
- Never send member name, email, Public user ID, account identifiers, credentials, raw portfolio identifiers, or secrets to an AI provider.
- Raw inquiry messages are encrypted and accessible through Admin-only audited paths. Never add a Public raw-message read endpoint.
- Keep `OMM.Public/Components/Shared/DecisionPathwayContent.razor` unchanged unless the architecture document is explicitly updated.

## Verification and reporting

Before reporting completion:

- Run `git diff --check`.
- Run the relevant build(s) and report exact result.
- Do not claim tests passed unless the user ran them or explicitly authorized you to run them.
- Report migration files, schema changes, authorization boundaries, and any unresolved risks.
- Do not commit or push.
