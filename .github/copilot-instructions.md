# Copilot Instructions


## Project Guidelines
- For this repository, OMM.Admin uses admin-schema identities while shared master data lives in public schema. Master-data audit user IDs must be nullable text, not AspNetUsers foreign keys. When fixing legacy audit FK errors, update both EF mappings and the database migration, using PostgreSQL DROP CONSTRAINT IF EXISTS and DROP INDEX IF EXISTS for drift-safe migrations. Record this in AGENTS.md.
- When adding repository instructions or handoff guidance, explicitly label the scope, especially when the instructions concern database/infrastructure log handling.
- For this repository, keep Phase 7 documentation limited to one final development document and one Phase 7 handoff document; avoid creating extra planning/task files unless explicitly requested.

## User Experience Preferences
- Prefer modern Blazor UI experiences for operational tasks such as viewing, searching, filtering, and downloading logs instead of PowerShell or command-line workflows.
- Ensure runtime-required directories are created automatically if missing, including recovery after accidental deletion, rather than requiring manual folder creation.
- For runtime/error-handling changes, do not stop at compilation; perform a practical end-to-end verification of the failure path, including generated folders/files, notification behavior, and user-visible fallback responses.
- Keep login and all public forms on the same shared form spacing and padding standard; do not introduce login-specific padding or margin overrides. Preserve the merged passkey/external-provider action stack while reusing common form element styles.
- Move reusable inline styles out of Blazor markup into centralized CSS classes, following DRY; reuse existing shared styles whenever possible. Use generic, reusable CSS class names for standard form and modal elements rather than page-specific prefixes such as `mines-xxx`, so future forms can reuse the same styles without duplicated CSS. Keep page-specific prefixes only for truly page-specific visuals.
- For OMM.Admin, remove inline styles from shared layout and dashboard markup where possible, reuse existing centralized CSS classes, and follow DRY principles consistently with the prior public-project cleanup.
- Prioritize restrained, intentional UI styling. Avoid generic AI-looking visual treatments such as excessive rounded cards, heavy shadows, and decorative gradients. Ensure controls do not look cheap or overly pill-shaped; focus on simple premium geometry and optical alignment.
- For OMM.Admin mobile layouts, preserve usable single-column content and avoid cramped two-column card grids with excessive right-side whitespace; mobile sign-out controls should remain compact and visually aligned rather than wrapping awkwardly.
- Admin sidebar Bootstrap tooltips should use a white bubble with dark text and a subtle border/shadow for contrast against the black sidebar; public tooltip styling can remain separate.
- For the OMMv2 solution, use Bootstrap as the UI/component styling framework for Blazor projects. Do not use daisyUI or ask about daisyUI unless the user explicitly requests it; preserve existing Bootstrap conventions and dependencies.
- For the Add Mine category selector, prioritize readable card width and clean UI over fitting four cards per desktop row: use a wider three-column desktop grid, keep icon and title together in a compact header row, place description below, and avoid title clipping or awkward character wrapping.

## OMM Color System
- Use semantic color roles consistently across dashboard cards, sidebar icons, and related UI: Net Wealth uses warm gold; Mines uses matcha/olive green; Passive Income uses blue; positive Growth uses plum/purple; Loss and Burdens use muted red; Goals may use ochre/gold; Expenses use muted terracotta.
- Do not reuse the Mines green for Growth/Loss or unrelated metrics. Related states may share a hue family, but each dashboard metric should remain visually distinguishable.
- Prefer restrained tonal gradients or subtle light/shade variation over flat fills when adding depth. Keep gradients within the assigned semantic color, avoid rainbow or decorative AI-style gradients, and preserve readable contrast in light and dark states.
- Keep the same semantic colors and interaction states across desktop and mobile Blazor layouts.

## Security Practices
- Never hardcode the Twelve Data API key. Use an environment variable in Production and .NET User Secrets for localhost/development.

- For Admin CRUD bilingual columns, prefer compact generic labels `Name (EN)` and `Name (TW)` instead of entity-specific labels such as `Market Name (EN)` or the abbreviation `ZHTW`. Keep the Traditional Chinese field visible.

## Form Consistency
- For Add Mine form consistency, show currency units in input adornments/text boxes (for example RM), never in field labels. Use semantic names for labels such as 'Total Purchase Cost' rather than 'Total Purchase Cost (MYR)' or other currency suffixes.
- For Add Mine, support a tracking-first workflow: members may add an asset without current selling price, buyback rate, dividend rate, or growth data. Optional valuation/rate fields should calculate gain/loss or income only when provided; purchase cost and placement details remain the baseline. Future backend market-price updates may populate optional current values automatically.
- For compact currency notation, use conventional abbreviated formatting such as RM1.5k, not RM1.50k; keep two decimal places for ordinary non-compact currency amounts when requested.
- When Savings Account and Current Account share the same behavior and form fields, label the mine type as "Savings / Current Account" rather than adding a separate CurrentAccount enum.