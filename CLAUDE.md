# CLAUDE.md — Action Tracker

## Project Overview

Action Tracker is a full-stack project/action management application for tracking workspaces, projects, milestones, action items, budgets, baselines, and KPIs. It uses Azure AD SSO for authentication alongside local JWT auth.

## Tech Stack

- **Backend:** .NET 9, ASP.NET Core, Entity Framework Core 9, ASP.NET Identity, FluentValidation, Serilog
- **Frontend:** Angular 20, Bootstrap 5, SCSS, MSAL (Azure AD), chart.js/ng2-charts, ngx-toastr, date-fns
- **Database:** SQL Server (EF Core Code-First migrations)
- **Auth:** Azure AD SSO + local JWT issuance (dual-scheme "MultiAuth")
- **Testing:** xUnit, Moq, FluentAssertions (backend); Jasmine/Karma (frontend)

## Repository Structure

```
action-tracker/
├── backend/ActionTracker/
│   ├── ActionTracker.sln                    # .NET solution file
│   ├── ActionTracker.API/                   # ASP.NET Core Web API (entry point)
│   │   ├── Program.cs                       # App bootstrap, DI, middleware pipeline
│   │   ├── Controllers/                     # REST API controllers
│   │   ├── Extensions/ServiceCollectionExtensions.cs  # DI registration groups
│   │   ├── Middleware/                      # ExceptionMiddleware, RequestLoggingMiddleware
│   │   └── Models/ApiResponse.cs            # Generic API response wrapper
│   ├── ActionTracker.Application/           # Business logic layer
│   │   ├── Features/                        # Feature folders (DTOs, Interfaces, Services, Validators)
│   │   │   ├── ActionItems/
│   │   │   ├── Auth/
│   │   │   ├── Dashboard/
│   │   │   ├── Kpis/
│   │   │   ├── OrgChart/
│   │   │   ├── Projects/                    # Sub-features: Milestones, Budget, Baseline, Comments, Documents
│   │   │   ├── Reports/
│   │   │   ├── StrategicObjectives/
│   │   │   ├── UserManagement/
│   │   │   └── Workspaces/
│   │   └── Common/Interfaces/IAppDbContext.cs
│   ├── ActionTracker.Domain/                # Entities, enums, BaseEntity
│   │   ├── Common/BaseEntity.cs             # Guid Id, CreatedAt, UpdatedAt, IsDeleted
│   │   ├── Entities/                        # ActionItem, ApplicationUser, Kpi, OrgUnit, etc.
│   │   └── Enums/                           # ActionCategory, ActionPriority, ActionStatus
│   ├── ActionTracker.Infrastructure/        # EF Core DbContext, migrations, external services
│   │   ├── Data/AppDbContext.cs
│   │   ├── Data/Configurations/             # EF Fluent API entity configurations
│   │   ├── Data/Migrations/                 # Code-first migrations
│   │   └── Services/                        # Infrastructure service implementations
│   └── ActionTracker.Tests/                 # Unit and integration tests
│       ├── Unit/                            # Service-level tests with Moq
│       └── Integration/                     # WebApplicationFactory-based API tests
├── frontend/action-tracker-ui/
│   ├── package.json
│   ├── angular.json
│   ├── proxy.conf.json                      # Dev proxy → https://localhost:7135/api
│   ├── src/
│   │   ├── main.ts
│   │   ├── styles.scss
│   │   ├── environments/                    # environment.ts, environment.development.ts, environment.production.ts
│   │   └── app/
│   │       ├── app.routes.ts                # Lazy-loaded routes with guards
│   │       ├── app.config.ts
│   │       ├── core/                        # Auth, guards, interceptors, models, services
│   │       │   ├── auth/msal.config.ts
│   │       │   ├── guards/                  # authGuard, loginGuard, roleGuard, adminGuard
│   │       │   ├── interceptors/            # auth, error, loading, refresh-token
│   │       │   ├── models/                  # Shared TypeScript interfaces
│   │       │   └── services/                # Singleton services (auth, dashboard, toast, etc.)
│   │       ├── features/                    # Feature modules (lazy-loaded)
│   │       │   ├── actions/                 # Standalone action items
│   │       │   ├── admin/                   # Admin panel (KPIs, org chart, strategic objectives)
│   │       │   ├── auth/                    # Login, unauthorized pages
│   │       │   ├── dashboard/               # Team and management dashboards
│   │       │   ├── projects/                # Project CRUD, milestones, budget, baseline, comments, docs
│   │       │   ├── reports/
│   │       │   ├── user-management/
│   │       │   └── workspaces/
│   │       ├── layout/                      # Header, footer, loading bar, main layout
│   │       └── shared/components/           # Reusable UI: data-table, status-badge, priority-badge, etc.
│   └── assets/styles/_variables.scss
└── docs/
    ├── architecture.md
    └── db-config.md
```

## Key Conventions

### Backend

- **IDs:** Always `Guid` (never `int`). `BaseEntity` provides `Id`, `CreatedAt`, `UpdatedAt`, `IsDeleted`.
- **Soft delete:** Use `IsDeleted` flag — never physically delete records.
- **API responses:** Wrap all responses in `ApiResponse<T>` with `Ok(data)` / `Fail(message)` factory methods.
- **Route prefix:** `api/[controller]` (resolves to `api/workspaces`, `api/projects`, etc.).
- **Async everywhere:** All service methods are async. Accept `CancellationToken` where applicable.
- **DI registration:** Group service registrations in `ServiceCollectionExtensions.cs` using extension methods (`AddApplicationServices()`, `AddAdminPanelServices()`, `AddProjectsFeatureServices()`, etc.).
- **Feature folder structure:** Each feature under `Application/Features/{FeatureName}/` contains `DTOs/`, `Interfaces/`, `Services/`, and optionally `Validators/`, `Mappers/`, `Models/`.
- **JSON serialization:** camelCase property names, string enums (camelCase), null values omitted.
- **DateTime:** Store as UTC, convert to UAE time (UTC+4) for display.
- **Validation:** FluentValidation with auto-validation enabled.
- **Logging:** Serilog (structured logging). Use `ILogger<T>` injection. Log with message templates, not string interpolation.
- **Auth policies:** `AdminOnly`, `AdminOrManager`, `LocalOrAzureAD`. Applied via `[Authorize(Policy = "...")]`.
- **Roles:** Admin, Manager, User, Viewer (seeded on startup by `RoleSeeder`).
- **Type alias pattern:** When two features share an interface name (e.g., `IStrategicObjectiveService`), use `using` aliases at the top of the file to disambiguate.

### Frontend

- **Angular 20** with standalone components (no NgModules).
- **Lazy loading:** All feature routes use `loadComponent` / `loadChildren` with dynamic imports.
- **Routing:** Defined in `app.routes.ts` with child routes nested under the `LayoutComponent`.
- **Feature structure:** Each feature under `features/{name}/` contains `components/`, `models/`, `services/`, and a `*.routes.ts` file.
- **Styling:** Bootstrap 5 + SCSS. Global variables in `assets/styles/_variables.scss`.
- **State management:** Services with RxJS (no NgRx/store). Services in `core/services/` are app-wide singletons; feature services are co-located.
- **HTTP interceptors:** Auth token injection, error handling, loading bar, token refresh.
- **Prettier config:** 100 char print width, single quotes, Angular HTML parser. Defined in `package.json`.

## Build & Run

### Backend
```bash
cd backend/ActionTracker
dotnet restore
dotnet build

# Run the API (auto-migrates database on startup)
cd ActionTracker.API
dotnet run
# API available at https://localhost:7135 (HTTPS) or http://localhost:5135 (HTTP)
# Swagger UI at /swagger
```

### Frontend
```bash
cd frontend/action-tracker-ui
npm install
npm start          # ng serve (with proxy to backend at localhost:7135)
```

### Tests
```bash
# Backend tests (xUnit)
cd backend/ActionTracker
dotnet test

# Frontend tests (Karma/Jasmine)
cd frontend/action-tracker-ui
npm test
```

## Database

- EF Core Code-First with SQL Server.
- Connection string stored in user secrets (never in committed config). See `docs/db-config.md`.
- Auto-migration runs on API startup (`db.Database.MigrateAsync()`).
- To add a migration:
  ```bash
  cd backend/ActionTracker/ActionTracker.API
  dotnet ef migrations add <MigrationName> --project ../ActionTracker.Infrastructure
  ```

## Configuration

- **Backend secrets:** Use `dotnet user-secrets` for `ConnectionStrings:DefaultConnection`, `Jwt:Key`, `AzureAd:TenantId`, `AzureAd:ClientId`.
- **`appsettings.json`** is gitignored for the API project. Template exists at `appsettings.json` (tracked version provides Serilog and non-secret config).
- **Frontend environments:** `src/environments/environment.ts` (dev), `environment.production.ts` (prod). Contains API base URL and MSAL config.

## Important Notes for AI Assistants

1. **Never commit secrets.** Connection strings, JWT keys, and Azure AD credentials go in user secrets or environment variables, not in source code.
2. **Guid IDs only.** All entity primary keys and foreign keys must be `Guid`. The codebase migrated from `int` to `Guid` — do not introduce `int` IDs.
3. **Soft delete only.** Never add physical deletes. Always set `IsDeleted = true`.
4. **Wrap API responses.** Every controller action must return `ApiResponse<T>.Ok(data)` or `ApiResponse<T>.Fail(message)`.
5. **Register new services** in the appropriate extension method in `ServiceCollectionExtensions.cs`.
6. **Use type aliases** when adding services that share interface names across features.
7. **Angular standalone components only.** Do not create NgModules. Use `loadComponent`/`loadChildren` for routing.
8. **Keep feature isolation.** Each backend feature has its own DTOs, interfaces, and services. Do not share DTOs across features — create new ones if needed.
