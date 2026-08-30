# Phase 3 Handoff — Admin Layout & Navigation

> **Document Status:** Complete  
> **Target Audience:** Next developer / Agent for Phase 4 (`AdminDataGrid`)

---

## 1. Executive Summary

Phase 3 established the independently hostable **`OMM.Admin`** Blazor web application, built its dedicated admin navigation shell (`AdminLayout`), configured separate database context boundaries for Identity and Master Data, and implemented folder-level authorization gates with graceful access-denied handling.

---

## 2. Solution Architecture

The solution uses a 3-tier monorepo structure in `OMMv2.slnx`:

```text
OMMv2.slnx
├── OMM.Public           # Public website and miner dashboard (Port 5025 / 7041)
├── OMM.Admin            # Admin-only website & backoffice console (Port 5026 / 7042)
└── OMM.Shared           # Shared domain entities (Master Data models) & auditable contracts
```

### Key Architectural Boundaries:
- **Zero Shared Identity / Cookies:** `OMM.Public` and `OMM.Admin` use independent Identity cookies, schemes, and user stores. Logging into the admin app does not log into the public app and vice versa.
- **Migration Ownership:**
  - `OMM.Public` owns EF Core migrations for the `public` schema (`Countries`, `Exchanges`, `Markets`, `Sectors`, `SubSectors`, `Institutions`, `Stocks`, `AspNetUsers`).
  - `OMM.Admin` owns EF Core migrations for the `admin` schema (`admin.AspNetUsers`, `admin.AspNetRoles`, etc.).
  - `OMM.Admin` reads business data using a **read-only** `MasterDataDbContext` mapped to `public` schema tables. It does **not** create or apply migrations to public master data.

---

## 3. Database & Schemas (Neon PostgreSQL)

Both applications connect to the same PostgreSQL Neon database (`DATABASE_URL`), but operate in isolated schemas:

| Entity Type | PostgreSQL Schema | DbContext in OMM.Admin | DbContext in OMM.Public |
|---|---|---|---|
| Admin Identity & Roles | `admin` (`"AspNetUsers"`, etc.) | `ApplicationDbContext` | *(No access)* |
| Public Identity & Roles | `public` (`"AspNetUsers"`, etc.) | *(No access)* | `ApplicationDbContext` |
| Master Data (Stocks, Institutions, Markets) | `public` (`"Stocks"`, etc.) | `MasterDataDbContext` (Read-only) | `ApplicationDbContext` (Owner) |

### Seeded Admin Accounts (`admin.AspNetUsers`):
1. **SuperAdmin:** `superadmin` / `kockhwie@msn.com` (Role: `SuperAdmin`)
2. **Admin:** `kockhwie` / `kockhwie@gmail.com` (Role: `Admin`)
- *Note:* Both accounts are seeded with `MustChangePassword = true` to force password reset on initial credential setup.

---

## 4. Authentication, Authorization & Security Gates

### Admin Login (`/login`)
- Route: `/login` (`OMM.Admin/Components/Pages/OmmAdminLogin.razor`)
- Render Mode: Static SSR with `@attribute [ExcludeFromInteractiveRouting]` (ensuring standard HTTP POST cookie pipeline for Identity).
- Access Gate: Checks for `Admin` or `SuperAdmin` role upon successful password authentication. Authenticated non-admins are immediately signed out and redirected to `/Account/AccessDenied`.

### Folder-Level Authorization
- Folder: `OMM.Admin/Components/Pages/Admin/`
- Guard: `_Imports.razor` applies `@layout AdminLayout` and `@attribute [Authorize(Policy = "RequireAdminRole")]` to all pages in the folder automatically.

### Graceful `NotAuthorized` Flow
- `Routes.razor` checks authentication state:
  - **Anonymous users** hitting `/admin/*` $\rightarrow$ redirected to `/login`.
  - **Authenticated non-admins** hitting `/admin/*` $\rightarrow$ redirected to `/Account/AccessDenied` (preventing login bounce loops).

---

## 5. UI/UX Styling & Visual Theme

- **Admin Palette:** Uses `--omm-ink-900` for the dark administrative sidebar, with `--omm-burden-red` (crimson) top accent borders and `ADMIN CONSOLE` badges to ensure immediate visual distinction from the gold/clay miner dashboard.
- **Icons:** Strictly Tabler Icons (`ti ti-*`), adhering to workspace rules.
- **Components:** Responsive sidebar with mobile drawer support (`ToggleMobileNav`) and user profile pill with single-click sign-out.

---

## 6. Route Map & Status

| Route | Component | Status | Notes |
|---|---|---|---|
| `/login` | `OmmAdminLogin.razor` | **Complete** | Custom admin login with role verification |
| `/admin` | `Pages/Admin/Index.razor` | **Complete** | System row counts for all 7 master tables + admin users |
| `/admin/klse-stocks` | `Pages/Admin/KlseStocks.razor` | **Stub** | Ready for Phase 4/5 (`AdminDataGrid` + Stock management) |
| `/admin/institutions` | `Pages/Admin/Institutions.razor` | **Stub** | Ready for Phase 6 (Institution management) |
| `/admin/reference-data` | `Pages/Admin/ReferenceData.razor` | **Stub** | Markets & Sectors reference browse |
| `/admin/users` | `Pages/Admin/Users.razor` | **Stub** | Ready for Phase 7 (Admin account management) |
| `/Account/AccessDenied`| `Account/Pages/AccessDenied.razor` | **Complete** | Friendly access denied page |

---

## 7. Troubleshooting & Gotchas Resolved in Phase 3

1. **Email Uniqueness Constraint:** `options.User.RequireUniqueEmail = true;` is configured in `Program.cs` for `OMM.Admin` to prevent accidental duplicate email registrations in `admin.AspNetUsers`.
2. **Interactive Routing on Login:** `/login` is decorated with `@attribute [ExcludeFromInteractiveRouting]` so `HttpContext` is available for cookie issuance and external signout.
3. **Local Redirect Normalization:** The `/Account/Logout` endpoint normalizes redirect destinations starting with `/` or `~/` to prevent `//` double-slash `InvalidOperationException` in ASP.NET Core `LocalRedirect`.

---

## 8. What's Next in Phase 4

- **Task:** Build the reusable, generic `AdminDataGrid<TItem>` Blazor component in `OMM.Admin` (or `OMM.Shared` if shared).
- **Capabilities Required:**
  - Client-side and server-side pagination.
  - Multi-column sorting (ascending/descending indicators).
  - Search / filtering text input with debouncing.
  - Loading skeleton / empty state templates.
  - Action column slot for edit/delete buttons in Phase 5 & 6.
