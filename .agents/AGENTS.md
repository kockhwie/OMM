# AGENTS.md – Workspace‑level agents & rules

> **NOTE**  
> This file lives in the `.agents` directory (the workspace‑level customizations root).  
> It is **not** tracked by the repository’s source code – the `.gitignore` already excludes the entire `.agents` folder if you prefer.  
> Add any custom agents, sub‑agents, or rules here following the Antigravity conventions.

## Rules
- **Rule‑001** – *No emoji icons* – Enforce usage of Tabler Icons only (already satisfied by the CDN link in `App.razor`).
- **Rule‑002** – *Bootstrap version* – Keep Bootstrap at the latest stable `5.3.x` series (checked in CI).
- **Rule‑003** – *Demo folder exclusion* – Do not commit the `demo/` directory (covered by `.gitignore`).

## Agents (optional)
You can define additional agents that the IDE can invoke, for example:
```
agent:
  name: Verify-Icons
  description: Checks that the Tabler Icons CDN points to the latest released version.
  triggers:
    - on file change: Components/App.razor
  actions:
    - run: curl https://data.jsdelivr.com/v1/packages/npm/@tabler/icons-webfont
```
*Feel free to delete or expand any of the sections above. The file exists only to give you a clean starting point for future customizations.*
