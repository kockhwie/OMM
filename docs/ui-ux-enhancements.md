# OMM.Public – UI/UX Enhancement Backlog

Each task below is self-contained and can be assigned to an AI agent independently.
Work through them in order; later tasks build on tokens and classes defined by earlier ones.

---

## Task 1 – Design Token Foundation

**Goal:** Replace all hard-coded colour, spacing, and radius values in `OMM.Public/wwwroot/app.css`
with CSS custom properties so every subsequent task has a single source of truth.

**Scope:** `OMM.Public/wwwroot/app.css` only.

**Acceptance criteria:**
- `:root` block defines at minimum:
  - `--color-primary`, `--color-primary-hover`, `--color-accent`
  - `--color-bg`, `--color-surface`, `--color-text`, `--color-text-muted`
  - `--color-border`
  - `--spacing` (base unit = `0.5rem`; use `calc(var(--spacing) * N)` for multiples)
  - `--radius` (e.g., `8px`)
  - `--transition` (e.g., `all 0.2s ease`)
- All existing hard-coded hex/rgb colour strings in `app.css` are replaced with the new tokens.
- No visual regression – pages should look the same after the swap.

**Do NOT** touch Razor files or component-level `.css` files in this task.

---

## Task 2 – Typography System

**Goal:** Introduce a consistent, fluid type scale across the public app.

**Scope:** `OMM.Public/wwwroot/app.css`, `OMM.Public/Components/App.razor` (Google Fonts link only).

**Acceptance criteria:**
- Google Font "Inter" (weights 400, 500, 600) loaded via `<link>` in `App.razor` before the existing stylesheet link.
- `:root` adds:
  - `--font-family: 'Inter', system-ui, sans-serif;`
  - `--font-size-base: clamp(0.9rem, 1vw + 0.5rem, 1.1rem);`
  - `--font-size-sm: 0.875rem;`
  - `--font-size-lg: 1.25rem;`
  - `--font-size-xl: 1.75rem;`
  - `--line-height: 1.6;`
- `html` rule uses `var(--font-family)` and `var(--font-size-base)`.
- Headings (`h1`–`h4`) use tokens for size and `font-weight: 600`.
- Muted helper text (`.text-muted`, `small`) uses `var(--color-text-muted)` and `var(--font-size-sm)`.

**Do NOT** change layout or component markup in this task.

---

## Task 3 – Button & Interactive Element Polish

**Goal:** Give all buttons and clickable elements smooth hover/focus transitions and a consistent visual style.

**Scope:** `OMM.Public/wwwroot/app.css` (button rules only).

**Acceptance criteria:**
- Primary buttons (`.btn-primary`) use `var(--color-primary)` background and transition to `var(--color-primary-hover)` on `:hover`.
- `:hover` adds `transform: translateY(-2px)` and a soft `box-shadow`.
- `:active` reverses the lift (`translateY(0)`).
- `:focus-visible` shows a visible 2 px outline using `var(--color-primary)` (not the browser default blue unless that matches the palette).
- All transitions use `var(--transition)`.
- Touch targets are at least 44 px tall/wide (set `min-height: 44px` on `.btn`).

**Do NOT** touch Razor files in this task.

---

## Task 4 – Card / Surface Component

**Goal:** Define a reusable `.omm-card` CSS component with an optional glassmorphism variant.

**Scope:** `OMM.Public/wwwroot/app.css`.

**Acceptance criteria:**
- `.omm-card` class provides:
  - `background: var(--color-surface)`
  - `border: 1px solid var(--color-border)`
  - `border-radius: var(--radius)`
  - `padding: calc(var(--spacing) * 3)`
  - Smooth `box-shadow` lift on `:hover`.
  - `transition: var(--transition)`.
- `.omm-card--glass` variant adds:
  - `backdrop-filter: blur(12px)`
  - Semi-transparent background (`rgba` using the surface colour)
  - Subtle `border` with low-opacity white.
- No existing components are broken; this is additive only.

---

## Task 5 – Dark Mode Support

**Goal:** Add a dark-mode colour palette that activates automatically via `prefers-color-scheme: dark`.

**Scope:** `OMM.Public/wwwroot/app.css`.

**Pre-requisite:** Task 1 must be complete (tokens must exist).

**Acceptance criteria:**
- `@media (prefers-color-scheme: dark)` block overrides:
  - `--color-bg`, `--color-surface`, `--color-text`, `--color-text-muted`, `--color-border`
  - Optionally adjusts `--color-primary` for contrast on dark backgrounds.
- `html` element declares `color-scheme: light dark;`.
- Verify contrast ratio >= 4.5:1 for body text on dark background (use a contrast checker tool or note approximate values in a comment).
- No JS or manual toggle required for this task – OS preference only.

---

## Task 6 – Responsive Grid Layout

**Goal:** Ensure dashboard / list pages use CSS Grid for responsive multi-column layouts.

**Scope:** `OMM.Public/wwwroot/app.css` (layout utility classes).

**Acceptance criteria:**
- Add utility classes:
  - `.omm-grid` – `display: grid; gap: calc(var(--spacing) * 3); grid-template-columns: repeat(auto-fit, minmax(260px, 1fr));`
  - `.omm-grid--2col` – fixed two-column at >= 640 px, stacks below.
  - `.omm-grid--3col` – fixed three-column at >= 960 px, stacks below.
- Add spacing helpers: `.mt-1` through `.mt-4`, `.mb-1` through `.mb-4` using `var(--spacing)` multiples.
- All grid-using pages reflow gracefully at 375 px viewport width (mobile).

**Do NOT** refactor existing page Razor markup in this task – classes are additive.

---

## Task 7 – Accessibility Hardening

**Goal:** Audit and fix WCAG AA accessibility gaps in the public UI.

**Scope:** `OMM.Public/wwwroot/app.css` + any inline `style=""` attributes in Razor pages under `OMM.Public/Components/Pages/`.

**Acceptance criteria:**
- Remove any `outline: none` or `outline: 0` rules not paired with a visible `:focus-visible` alternative.
- All form `<input>`, `<select>`, `<textarea>` elements have a paired `<label>` (check existing pages; do not break working labels).
- Add `.sr-only` utility class (visually hidden but announced by screen readers):
  ```css
  .sr-only {
    position: absolute; width: 1px; height: 1px;
    padding: 0; margin: -1px; overflow: hidden;
    clip: rect(0,0,0,0); white-space: nowrap; border: 0;
  }
  ```
- Minimum touch target rule from Task 3 is enforced (`min-height: 44px`).
- Run a Lighthouse accessibility audit after changes; note the before/after score in a short comment at the top of any modified files.

---

## Task 8 – Micro-Animations & Motion Polish

**Goal:** Add tasteful entrance and interaction animations that make the UI feel alive without being distracting.

**Scope:** `OMM.Public/wwwroot/app.css`.

**Pre-requisite:** Tasks 1, 3, and 4 must be complete.

**Acceptance criteria:**
- Define a `@keyframes fadeSlideUp` animation (fade in + 8 px upward translate over 0.3 s).
- Apply it to `.omm-card` on initial render via `.omm-card { animation: fadeSlideUp 0.3s ease both; }`.
- Stagger multiple cards using `animation-delay` on `:nth-child(n)` selectors (up to 5 children, 50 ms apart).
- Wrap all new animation rules in `@media (prefers-reduced-motion: no-preference)` so they do not fire for users who prefer less motion.
- Skeleton loader shimmer class `.omm-skeleton` (`background: linear-gradient(90deg, ...)`) for use on loading states.

---

## Task 9 – Form & Input Visual Upgrade

**Goal:** Align all public form controls with the design tokens and `ui-form-standards.md`.

**Scope:** `OMM.Public/wwwroot/app.css` (form rules) + any `omm-form-*` classes. Also cross-check against `docs/ui-form-standards.md`.

**Acceptance criteria:**
- `input`, `select`, `textarea` share:
  - `border: 1px solid var(--color-border)`
  - `border-radius: var(--radius)`
  - `padding: calc(var(--spacing) * 1.5) calc(var(--spacing) * 2)`
  - `transition: var(--transition)` on border-color / box-shadow.
  - `:focus` state: border becomes `var(--color-primary)` + subtle glow shadow.
- Invalid state (`.is-invalid` / `:invalid`) uses a red token (`--color-danger`), not a hard-coded `#dc3545`.
- All tokens are defined in Task 1; no new magic numbers introduced.
- Mobile: inputs stack full-width below 640 px.

---

## Task 10 – Navigation & Header Polish

**Goal:** Give the top navigation / sidebar a premium finish that coheres with the token system.

**Scope:** `OMM.Public/wwwroot/app.css` + `OMM.Public/Components/Layout/` Razor files (minimal markup changes only).

**Acceptance criteria:**
- Nav background uses `var(--color-surface)` with a subtle `box-shadow` to separate it from page content.
- Active nav link has a clear visual indicator (left border strip or background highlight using `var(--color-primary)`).
- Nav links use `var(--transition)` on colour/background.
- On mobile (< 640 px) the nav collapses correctly without horizontal scroll.
- Brand / logo area has correct spacing using `var(--spacing)` tokens.

---

## Notes for AI Agents

- **Check `docs/ui-form-standards.md`** before touching any form-related CSS to stay consistent with existing standards.
- **Prefer additive changes** – extend existing rules with tokens rather than rewriting selectors wholesale.
- **No magic numbers** – every colour, spacing value, or radius must reference a CSS custom property defined in Task 1.
- **No Tailwind** – this project uses Vanilla CSS. Do not add utility-class frameworks.
- **Bootstrap 5.3.x is in use** – work alongside Bootstrap; do not duplicate or override Bootstrap's core reset unless necessary.
- **Tabler Icons only** – do not add icon fonts or emoji for decorative icons; use Tabler Icon classes already loaded in `App.razor`.
- After each task, do a quick sanity check: run the public app and visually verify no regressions before marking the task done.
