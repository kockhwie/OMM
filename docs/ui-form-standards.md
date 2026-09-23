# OMM Form Standards

Use shared form classes before adding page-specific CSS. Forms should use a consistent label, control, help-text, gap, radius, border, and focus treatment.

## Required structure

- Group related controls in `omm-form-section`.
- Wrap each label/control/help combination in `omm-form-field`.
- Use `omm-form-label` for labels and `omm-form-help` for explanatory text.
- Use `omm-form-grid` for related fields.
- Use `dc-currency-group` for currency and percentage controls when the prefix or suffix is part of the value.

## Copy rules

- Use `stock` for a security or company: stock search, stock symbol, stock name.
- Use `share` for ownership units and per-unit prices: shares owned, current share price, purchase price per share, dividend per share.
- Mark optional context as optional near the section heading or control.

## Styling rules

- Do not add inline font-size, spacing, border, radius, or color styles to form controls.
- Do not mix `form-control-sm` with standard controls in the same form row unless the shared standard defines that compact variant.
- Keep focus styles visible for keyboard users.
- Preserve shared mobile stacking behavior.

## Review checklist

- Can the primary calculation be completed without optional stock selection?
- Are labels precise about total values versus per-share values?
- Are equivalent fields styled and spaced identically across modes?
- Are all controls labelled and keyboard reachable?
