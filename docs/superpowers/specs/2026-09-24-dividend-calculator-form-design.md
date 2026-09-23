# Dividend Calculator Form Design

## Goal

Refine `CalculatorDividend.razor` into the first approved OMM form template: calculation inputs lead the experience, optional stock context is progressive disclosure, terminology is consistent, and form styling is controlled by shared CSS rather than scattered inline values.

## Scope

In scope: Quick and Advanced Mode hierarchy, optional stock search and yield context, shared form primitives, documentation for future agents, accessibility, and responsive consistency.

Out of scope: live stock-price lookup, new market-data providers, dividend formulas, Save-as-Mine behavior, unrelated calculators, and new UI dependencies.

## Design decisions

Stock selection must not be the first section because it is optional and is not required to calculate dividend income.

Quick Mode order:

1. Dividend inputs: shares owned, payout type, frequency, dividend per share, and special dividend.
2. Optional yield context: current share price, purchase price per share, and optional stock search.

Advanced Mode order:

1. Position and pricing.
2. Dividend assumptions.
3. Growth and horizon.
4. Reinvestment.
5. Estimated tax.

Stock search remains an autocomplete control, not a disguised numeric-input picker. It remains optional and all prices remain manually editable.

Use `stock` for the security or company: stock search, stock symbol, stock name, selected stock. Use `share` for ownership units and per-unit values: shares owned, current share price, purchase price per share, dividend per share, and share price growth.

New and refactored forms must use shared classes for field spacing, section spacing, input sizing, labels, helper text, currency groups, optional content, and focus states. Inline styles are not allowed for reusable form primitives.

Advanced Mode must use its own stock state; it must not bind its stock picker to Quick Mode fields.

## Acceptance criteria

- The first meaningful Quick Mode fields are dividend-calculation inputs, not stock search.
- Stock selection is visibly optional and does not block dividend calculations.
- Yield context explains why prices are optional.
- Quick and Advanced Mode use the same stock/share terminology rules.
- Equivalent fields use the same shared form primitives.
- No new inline font-size, spacing, border, radius, or color styles are introduced for form controls.
- Advanced Mode stock selection does not overwrite or read Quick Mode stock state.
- The layout remains usable at mobile widths and has visible keyboard focus states.
- The app builds successfully with `dotnet build --no-restore`.
- No calculator formula or Save-as-Mine behavior changes.
