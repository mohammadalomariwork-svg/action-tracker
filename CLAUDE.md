# CLAUDE.MD — KU Action Tracker

> **Purpose**: This file is read automatically by Claude Code at session start.
> Keep it updated as features land. Last updated: 2026-04-13

---

## Project Overview

**KU Action Tracker** is an internal application for Khalifa University (KU) Digital Services division. It tracks actions, decisions, and tasks across departments with role-based access control scoped to organizational units.

- **Owner**: Mohammad Khalifa Al-Omari (Manager, Enterprise Applications — Digital Services)
- **Users**: KU staff across departments; access governed by Azure AD groups and internal roles

---

## Tech Stack

| Layer         | Technology                          |
|---------------|-------------------------------------|
| Frontend      | Angular 20, Bootstrap 5, TypeScript |
| Backend API   | .NET 9 (C#), ASP.NET Core Web API  |
| Architecture  | Clean Architecture (4-layer)        |
| Database      | SQL Server (instance: `localhost`)  |
| Auth          | Azure AD SSO (MSAL)                |
| UI Theme      | Fluent / Palantir aesthetic, dark & light modes |

---

## Solution Structure

```
ACTION-TRACKER/
│
├── .claude/                            # Claude Code session config
├── .github/                            # GitHub workflows / CI
│
├── backend/ActionTracker/              # ── .NET 9 BACKEND ──
│   ├── ActionTracker.API/              #   Controllers, middleware, filters, startup
│   ├── ActionTracker.Application/      #   Use cases, interfaces, DTOs, validators (CQRS)
│   ├── ActionTracker.Domain/           #   Entities, enums, value objects, domain events
│   ├── ActionTracker.Infrastructure/   #   EF Core, Azure AD, email, external services
│   ├── ActionTracker.Tests/            #   Unit & integration tests
│   └── ActionTracker.sln               #   Solution file
│
├── frontend/action-tracker-ui/         # ── ANGULAR 20 FRONTEND ──
│   ├── public/                         #   Static assets served as-is
│   ├── src/
│   │   ├── app/
│   │   │   ├── core/                   #   Auth guards, interceptors, core services
│   │   │   ├── features/               #   Feature modules (lazy-loaded)
│   │   │   ├── layout/                 #   Shell, sidebar, header, footer
│   │   │   ├── models/                 #   TypeScript interfaces & types
│   │   │   ├── services/               #   Shared API / utility services
│   │   │   ├── shared/                 #   Directives, pipes, shared components
│   │   │   ├── app.config.ts           #   App configuration / providers
│   │   │   ├── app.html                #   Root template
│   │   │   ├── app.routes.ts           #   Route definitions
│   │   │   ├── app.scss                #   Root styles
│   │   │   ├── app.spec.ts             #   Root tests
│   │   │   └── app.ts                  #   Root component
│   │   ├── assets/                     #   Images, icons, static files
│   │   ├── environments/               #   Environment configs (dev/prod)
│   │   ├── index.html                  #   Entry HTML
│   │   ├── main.ts                     #   Bootstrap entry point
│   │   └── styles.scss                 #   Global styles & theme variables
│   ├── angular.json                    #   Angular CLI config
│   ├── package.json                    #   NPM dependencies
│   ├── proxy.conf.json                 #   Dev proxy to backend API
│   ├── tsconfig.json                   #   TypeScript base config
│   ├── tsconfig.app.json               #   App-specific TS config
│   └── tsconfig.spec.json              #   Test TS config
│
├── .gitignore
├── BACKEND_OVERVIEW.md                 # Backend architecture & API documentation
├── FRONTEND_OVERVIEW.md                # Frontend architecture & component documentation
├── status.md                           # Implementation status tracker
└── CLAUDE.MD                           # ← This file
```

---

## Key Entities (Domain Layer)

- **ActionItem** — Core tracked item (title, description, status, priority, due date, assignee, org unit)
- **ActionItemWorkflowRequest** — Date change and status change approval requests (pending/approved/rejected)
- **ProjectApprovalRequest** — Project start-approval requests (pending/approved/rejected)
- **PermissionArea** — Logical grouping of permissions (e.g., "Actions", "Reports", "Admin")
- **Permission** — Granular permission within an area (e.g., "Actions.Create", "Actions.Delete")
- **RolePermission** — Maps a role to one or more permissions
- **OrgUnit** — Organizational unit for scoped data access
- **User** — Synced from Azure AD; linked to roles and org units

---

## Implemented Features

### Authentication & Authorization
- [x] Azure AD SSO via MSAL (login/logout, token refresh)
- [x] Dynamic authorization middleware (permission-based, not just role-based)
- [x] PermissionArea → Permission → RolePermission entity model
- [x] Org-unit-scoped data filtering (users see only their unit's data)
- [x] Angular `HasPermissionDirective` for conditional UI rendering
- [x] Role management UI

### UI / UX
- [x] Fluent / Palantir design aesthetic
- [x] Dark and light theme toggle
- [x] Login page with KU branding
- [x] Footer logo

### Core Functionality
- [x] Action item CRUD (create, edit, delete via offcanvas panel)
- [x] My Actions page (scoped to current user, stats cards, show-deleted toggle)
- [x] Created by Me section in My Actions
- [x] Default assignee on action items
- [x] Workspaces with org-unit scoping and summary stats
- [x] Projects with role-based access (My Projects page)
- [x] Milestones with validation on status transitions
- [x] Dashboard with stat cards
- [x] Interactive Gantt chart with hover tooltips
- [x] Filtering & search
- [x] Export to Excel and Print to PDF (workspaces, projects, milestones, my actions)
- [x] Action item workflow (date freeze, status change approval, escalation notifications)
- [x] My Approvals page (pending reviews, my requests, approve/reject with comments)
- [x] Real-time workflow notifications via SignalR with toast popups
- [x] In-app notification system (grouped by date, filter by type/read status, mark as read, delete)
- [x] Project approval workflow (submit for approval, approve/reject, date freeze, PendingApproval status)
- [x] Project Approvals tab on My Approvals page
- [x] Date freeze enforcement (project, milestone, and action item dates locked after approval)
- [ ] Scheduled reminders (automated due-date reminders)

---

## Key Reference Files

| File                   | Purpose                                      |
|------------------------|----------------------------------------------|
| `CLAUDE.MD`            | This briefing file (Claude Code reads it)    |
| `proxy.conf.json`      | Frontend → backend API proxy settings        |

---

## Conventions & Rules

### Backend (.NET)
- **Architecture**: Strict Clean Architecture — no domain dependency on infrastructure
- **CQRS pattern**: Use MediatR for commands/queries in the Application layer
- **Validation**: FluentValidation in Application layer
- **DTOs**: Separate request/response DTOs; never expose domain entities to API
- **Naming**: PascalCase for classes/methods, camelCase for local variables
- **API routes**: RESTful, versioned (`/api/v1/action-items`)
- **Error handling**: Global exception middleware with ProblemDetails responses
- **Tests**: Place all tests in `ActionTracker.Tests`

### Frontend (Angular)
- **State**: Use Angular signals or services (no NgRx unless explicitly requested)
- **Modules**: Feature modules in `features/`, lazy-loaded via `app.routes.ts`
- **Models**: TypeScript interfaces live in `models/` (not inline in components)
- **Services**: Shared/global services in `services/`; feature-specific services inside their feature folder
- **Components**: Standalone components preferred
- **Styling**: Bootstrap 5 utilities + custom SCSS variables for theme; global styles in `styles.scss`
- **Permissions**: Always use `*hasPermission="'Area.Action'"` directive for protected UI
- **HTTP**: Centralized interceptor in `core/` for auth tokens and error handling
- **Proxy**: Dev API calls proxied via `proxy.conf.json` — do not hardcode backend URLs

### Database
- **Migrations**: EF Core code-first migrations
- **Connection**: SQL Server instance `localhost`, database `ActionTrackerDb`
- **Soft deletes**: Use `IsDeleted` flag, never hard delete user data



---

## Context for AI Sessions

When asking Claude Code to work on this project:

1. **Don't re-scaffold** — the project structure exists. Add to it.
2. **Respect Clean Architecture layers** — never put business logic in controllers or EF queries in the domain.
3. **Check existing entities** before creating new ones — the permission model is already built.
4. **Follow the theme** — new UI components must work in both dark and light modes.
5. **Test with permissions** — any new endpoint needs a permission entry and the Angular directive applied to its UI.

---

## Documentation Updates (Required)

After completing any feature, bug fix, or refactor that changes the project's functionality, **update the following files before reporting the task as done:**

| File | What to update |
|------|---------------|
| `status.md` | Add or check off the feature in the relevant section. If a new section is needed, create it. Keep the "Not Yet Implemented" list current. |
| `BACKEND_OVERVIEW.md` | Add new controllers, endpoints, entities, enums, services, or config keys. Update existing tables if fields/routes changed. |
| `FRONTEND_OVERVIEW.md` | Add new components, routes, services, models, or shared items. Update component counts, permission areas, and feature descriptions. |

**Rules:**
- Update `Last updated:` date in each file you touch.
- Only document what is actually implemented — never add aspirational or planned items as done.
- Keep the same formatting and table style already used in each file.
- If a feature spans both frontend and backend, update all three files.
- If a feature is frontend-only or backend-only, update `status.md` plus the relevant overview file.
