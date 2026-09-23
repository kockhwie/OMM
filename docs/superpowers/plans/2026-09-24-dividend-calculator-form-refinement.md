# Dividend Calculator Form Refinement Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Refine `CalculatorDividend.razor` into the first approved OMM form template with a calculation-first hierarchy, consistent stock/share terminology, shared form primitives, and documented rules for future forms.

**Architecture:** Keep the existing Blazor component and dividend engine intact. Move repeated form presentation rules into `OMM.Public/wwwroot/app.css`, keep Quick and Advanced state separate, and document the conventions in `docs/ui-form-standards.md` and `AGENTS.md`. Do not add a UI library or market-data dependency.

**Tech Stack:** Blazor, Razor components, existing Bootstrap utilities, `OMM.Public/wwwroot/app.css`, existing `StockSearchPicker`, existing `DividendEngine`.

**Spec:** `docs/superpowers/specs/2026-09-24-dividend-calculator-form-design.md`

## Global Constraints

- Do not change dividend formulas, simulator calculations, or Save-as-Mine behavior.
- Do not add a UI framework, CSS dependency, market-data provider, or live-price lookup.
- Stock search remains optional; all prices remain manually editable.
- Use `stock` for the instrument/company and `share` for a unit of ownership or its price.
- Do not add inline font-size, spacing, border, radius, or color declarations for form controls.
- Do not run `dotnet test` automatically; ask the user before running tests.
- Do not commit or push automatically.

## Review Focus

- Empty stock context: manual dividend calculations still work with no stock selected. Task 2.
- Partial yield context: entering one price shows a useful message without breaking the result. Task 2.
- Advanced state isolation: Advanced stock selection does not mutate Quick stock state. Task 3.
- Mobile layout: paired fields stack without clipping. Task 4.
- Keyboard accessibility: labels, focus rings, tabs, switches, and search remain usable. Task 4.

## File ownership map

- `OMM.Public/wwwroot/app.css`: shared form primitives and calculator presentation tokens.
- `OMM.Public/Components/Shared/Tools/CalculatorDividend.razor`: form order, labels, bindings, and sections.
- `docs/ui-form-standards.md`: reusable rules for future forms.
- `AGENTS.md`: mandatory pointer to the form standard.

## Task 1: Establish the shared form standard

**Files:**

- Modify: `OMM.Public/wwwroot/app.css`
- Modify: `AGENTS.md`
- Create: `docs/ui-form-standards.md` (already created by the planning handoff; preserve its rules)

**Produces:** `omm-form-section`, `omm-form-field`, `omm-form-label`, `omm-form-help`, and `omm-form-grid` classes for Tasks 2 and 3.

- [ ] Add this CSS block to a clearly labelled form-standard section in `OMM.Public/wwwroot/app.css`, using existing OMM variables:

```css
.omm-form-section { display: flex; flex-direction: column; gap: 1rem; }
.omm-form-field { display: flex; flex-direction: column; gap: 0.35rem; }
.omm-form-label { margin: 0; font-size: 0.75rem; font-weight: 600; line-height: 1.3; }
.omm-form-help { margin: 0; color: var(--omm-ink-400); font-size: 0.7rem; line-height: 1.4; }
.omm-form-grid { display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); gap: 1rem; }
@media (max-width: 575.98px) { .omm-form-grid { grid-template-columns: 1fr; } }
```

- [ ] Consolidate only duplicate calculator form declarations that the new primitives replace. Do not rewrite unrelated site CSS.

- [ ] Add this section to `AGENTS.md`:

```markdown
## Public form standard

For new or refactored Public forms, follow `docs/ui-form-standards.md` and reuse the shared form primitives before creating component-specific spacing, typography, borders, or radii.
```

- [ ] Run `dotnet build OMM.Public\OMM.Public.csproj --no-restore`; expected: success with no new errors.

## Task 2: Refactor Quick Mode around calculation-first flow

**Files:**

- Modify: `OMM.Public/Components/Shared/Tools/CalculatorDividend.razor` Quick Mode markup and existing Quick Mode state only.

**Preserves:** `qcShares`, `qcDividendPerShare`, `qcSpecialDividendPerShare`, `qcFrequency`, `qcPayoutType`, `qcSharePrice`, `qcPurchasePrice`, `qcStockSymbol`, `qcStockName`, `CurrentQuickCalc`, `QcHasResult`, and `YieldInputsPartiallyFilled`.

- [ ] Make the first Quick Mode input card calculation-focused: Shares Owned, Payout Type, Dividend Frequency, fixed/variable dividend fields, and Special/Bonus Dividend Per Share.

- [ ] Move stock search and prices into a second optional section titled `Stock & Yield Context` or equivalent. Include Search Stock, selected stock chip, Current Share Price, Purchase Price Per Share, and the existing partial-input message.

- [ ] Keep stock selection optional and keep both price inputs manually editable. Use disclosure copy such as `Add prices to calculate yield`.

- [ ] Replace touched repeated `mb-2 mb-md-3` wrappers with `omm-form-field`; use `omm-form-label`, `omm-form-help`, and `omm-form-grid` for equivalent fields.

- [ ] Delete the commented-out Code/Name block rather than carrying dead markup forward.

- [ ] Normalize touched labels to `Shares Owned`, `Current Share Price`, `Purchase Price Per Share`, `Dividend Per Share`, and `Special / Bonus Dividend Per Share`.

- [ ] Run `dotnet build OMM.Public\OMM.Public.csproj --no-restore`; expected: success and no changes to formula/service files.

## Task 3: Refactor Advanced Mode and isolate its stock state

**Files:**

- Modify: `OMM.Public/Components/Shared/Tools/CalculatorDividend.razor` Advanced Mode markup and existing simulator state only.

**Consumes:** Task 1 classes. **Produces:** Advanced Mode using `simStockSymbol` and `simStockName` for stock context.

- [ ] Change Advanced Mode `StockSearchPicker` bindings from `qcStockSymbol`/`qcStockName` to `simStockSymbol`/`simStockName`. Do not add live price lookup.

- [ ] Verify `CarryToSimulator` still copies Quick Mode values into simulator state after the binding correction.

- [ ] Use this section order: `Position & Pricing`; `Dividend Assumptions`; `Growth & Horizon`; `Reinvestment (DRIP)`; `Estimated Tax (Optional)`.

- [ ] Put stock search, selected stock, initial shares, current share price, purchase price per share, recurring contribution, and contribution frequency in Position & Pricing.

- [ ] Use `Current Share Price` and `Purchase Price Per Share` wherever values are per-share. Do not use ambiguous `Current Market Share Price` or `Purchase Price` in those contexts.

- [ ] Apply `omm-form-section`, `omm-form-field`, `omm-form-label`, `omm-form-help`, and `omm-form-grid` to touched fields. Remove inline form font sizes and use shared prefix/suffix classes for `%`, `yr`, and currency values.

- [ ] Run `dotnet build OMM.Public\OMM.Public.csproj --no-restore`; expected: success and unchanged simulator calculations.

## Task 4: Final consistency, accessibility, and responsive pass

**Files:**

- Modify: `OMM.Public/Components/Shared/Tools/CalculatorDividend.razor`
- Modify: `OMM.Public/wwwroot/app.css`

- [ ] Audit the component with:

```powershell
rg -n 'style=|form-control-sm|mb-[0-9]|p-[0-9]|Stock Price|Purchase Cost|Current Market' OMM.Public\Components\Shared\Tools\CalculatorDividend.razor
```

Replace remaining form-related drift with shared classes. Leave chart/table presentation styles out of scope unless they directly affect form consistency.

- [ ] Confirm every input/select has a matching label `for`/`id` pair or an explicit accessible name. Confirm mode tabs, payout tabs, switches, and stock search retain visible keyboard focus.

- [ ] Inspect narrow and desktop layouts. Confirm price pairs, contribution pairs, variable payout terms, tax fields, and action buttons do not overflow. Adjust shared CSS rather than adding one-off widths in Razor.

- [ ] Run `dotnet build OMMv2.slnx --no-restore`; expected: solution succeeds with no new errors.

## Task 5: Independent review and handoff verification

**Review files:** `docs/superpowers/specs/2026-09-24-dividend-calculator-form-design.md`, `docs/superpowers/plans/2026-09-24-dividend-calculator-form-refinement.md`, `docs/ui-form-standards.md`, `AGENTS.md`, `OMM.Public/Components/Shared/Tools/CalculatorDividend.razor`, and `OMM.Public/wwwroot/app.css`.

- [ ] Run the terminology audit:

```powershell
rg -n -i 'stock price|purchase cost|current market|stock information|search stock|share price|price per share|dividend per share' OMM.Public\Components\Shared\Tools\CalculatorDividend.razor docs\ui-form-standards.md
```

Review every match against the terminology rules.

- [ ] Confirm no unrelated calculator, formula, service, or data files changed.

- [ ] Run `git diff --check`, `git diff --stat`, and `git status --short`; expected: no whitespace errors and only planned files changed.

- [ ] Report evidence for every acceptance criterion in the spec: calculation-first order, optional stock context, terminology, shared primitives, no new inline form styling, Advanced state isolation, mobile layout, keyboard focus, successful build, and unchanged formulas/Save-as-Mine behavior.

- [ ] Stop before commit or push and present the final diff for user review.
