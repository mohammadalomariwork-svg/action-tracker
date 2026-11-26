# CLAUDE.MD — KU Action Tracker

> **Purpose**: This file is read automatically by Claude Code at session start.
> Keep it updated as features land. Last updated: 2026-11-26


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
| Background jobs | Hangfire 1.8 (SQL Server storage, `HangFire` schema) |
| UI Theme      | Fluent / Palantir aesthetic in the KUmmand Center logo palette, dark & light modes |

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

- **ActionItem** — Core tracked item (title, description, status, priority, assignee, org unit). `StartDate` / `DueDate` are the **planned** start / end (labelled "Planned start" / "Planned end"; frozen, date-change requests, working-day rule, `DashboardRules`). `ActualStartDate` / `ActualEndDate` (WO-2026-11 A-1) are the recorded actual dates — outside all of those rules. `IsStandalone` is derived by the server from `ProjectId` (A-1b, `ActionItemStandalone`) — never take it from a client
- **ActionItemSubItem** / **ActionItemSubItemAssignee** — Sub-items of an action item (WO-2026-11 A-2): title, description, optional start, required end, sort order, Done flag (`DoneAt`, `DoneByUserId`), own assignee table. One level only. **Not action items** — never counted by `DashboardRules`, stats, dashboards or reports. `DeletedWithParent` marks sub-items soft-deleted together with their parent (restoring the parent restores exactly those)
- **ActionItemUpdate** — Action Updates (WO-2026-11 A-3) on an action item or one of its sub-items (`SubItemId`), replacing comments. Mapped to the existing `ActionItemComments` table (no rename). `Status` Draft / Submitted (`ActionItemUpdateStatus`), `SubmittedAt`, `IsHighImportance`, `IsDeleted` (drafts only). A draft is visible to its author only (query filter); a submitted update is immutable for every role (409). Not action items — never counted by `DashboardRules`, stats, dashboards or reports
- **Project** — `ProjectType` (Operational / Strategic) is the **global** classification (labelled "Classification (global)"; drives the approval chain — unchanged by P-1). `InternalClassificationId` (WO-2026-11 P-1) is the optional internal classification, always one of the project's own workspace
- **ProjectMember** — Project people (WO-2026-11 P-2): additional project managers, viewers and team members of one project (`ProjectId`, `UserId`, `Role` ProjectManager / Viewer / TeamMember, unique on the three). `Project.ProjectManagerUserId` stays the **lead** PM (approval routing, notifications, PM-list rule) and sponsors stay in **ProjectSponsor**. No query filter, no soft delete (removing a person deletes the row; access ends on the next request)
- **WorkspaceProjectClassification** — Internal project classifications of one workspace (WO-2026-11 P-1): `Name` ≤ 100 (`NormalizedName` unique per workspace among live rows), `SortOrder`, `IsActive`, `IsDeleted`. Deactivated = kept on its projects, not newly chosen; delete is soft and refused while any project (also a soft-deleted one) uses it. Query filter: soft delete + private workspace
- **ActionItemWorkflowRequest** — Date change and status change approval requests (pending/approved/rejected)
- **ProjectApprovalRequest** — Project start-approval requests (pending/approved/rejected)
- **StrategicPlan** — Time-bounded wrapper for strategic objectives; one active plan per org unit; status enum (Draft / PendingApproval / Approved / Archived)
- **StrategicPlanApprovalRequest** — Approval request raised against a `StrategicPlan`. `RequestType` (`Initial` / `Revision`), `Status` (`Pending` / `Approved` / `Rejected`), with `AssignedReviewerUserId` resolved at submit time via `IApprovalAuthorityResolver`
- **StrategicObjective** — Org-unit-scoped objective with optional FK to `StrategicPlan`; auto-generated `ObjectiveCode` (SO-001)
- **Kpi** / **KpiTarget** — KPIs linked to objectives; period-aware monthly target rows. `KpiTarget` carries per-target details: `Baseline`, `DataSource`, `CalculationNotes`, and `Direction` (HigherIsBetter / LowerIsBetter / OnTarget)
- **KpiActual** — Period-aware (Quarter / SemiAnnual / Annual) measured value of a `KpiTarget`. The actual itself acts as the workflow record (Status: Draft → PendingApproval → Approved/Rejected). Reviewer is the level-3 head from `IApprovalAuthorityResolver`. Stays editable when the parent plan is locked.
- **Document** — Polymorphic file attachments (`RelatedEntityType` + `RelatedEntityId`, content in the DB). Types: `ActionItem`, `ActionItemSubItem`, `ActionItemUpdate`, `Project`, `Milestone`, `ProjectRisk`, `KpiTarget`, `KpiActual` (others refused). Every request checks the owning record (hidden → 404, also by document id — WO-2026-11 A-4); 10 per KPI target / actual, action item, sub-item and update; KPI writes gated by strategic scope
- **Tag** / **ActionItemTag** — Free-text tags on action items (WO-2026-11 A-5). A `Tag` belongs to one workspace (`WorkspaceId`, `Name` ≤ 30, `NormalizedName` unique per workspace); `ActionItemTag` links an item to a tag **of its own workspace**. Tags are never deleted. Query filters: private workspace (tags), follow the item (links)
- **PermissionArea** — Logical grouping of permissions (e.g., "Actions", "Reports", "Admin")
- **Permission** — Granular permission within an area (e.g., "Actions.Create", "Actions.Delete")
- **RolePermission** — Maps a role to one or more permissions
- **OrgUnit** — Organizational unit for scoped data access; optional `HeadUserId` (CR-001-H) — heads at level 3 act as the strategic-plan approver for their subtree
- **WorkspaceMember** — Direct user-to-workspace assignment. Members get read access to the workspace's projects/action items via `IWorkspaceAccessResolver`, independent of org-unit scoping
- **WorkspaceProjectManager** — Whitelist of users eligible to be picked as PM for projects in a workspace. The PM dropdown on the project form is restricted to this list; create/edit is rejected when the chosen PM isn't on it.
- **WorkspaceStrategicProjectApprover** — Sequential approver chain (1-based `Order`) for strategic projects in a workspace. Mirrors the strategic-plan pattern: a single `ProjectApprovalRequest` is created at level 1, each Approve advances the chain, and rejection at any level sends the project back to Draft. Operational projects bypass the chain — their submit short-circuits Draft → Active with a self-approved historical request.
- **User** — Synced from Azure AD; linked to roles and org units
- **DataSyncJob** / **DataSyncJobRun** — Admin-configurable background sync jobs (cron, time zone, enabled) and their run history. `JobKey` maps to an `IDataSyncJobHandler` in Infrastructure and doubles as the Hangfire recurring-job id
- **KuEmployeeInfo** — Keyless mirror of the HR `ku_employee_info` table (read-only from the app); refreshed by the `employee-info-sync` data-sync job
- **WorkCalendar** / **WorkCalendarDay** / **WorkCalendarException** — Working calendar: weekly pattern (always 7 day rows with working hours) plus dated exceptions (holidays, special working days, reduced-hours periods; inclusive range). Exactly one non-deleted calendar is the default (filtered unique index). `Workspace.WorkCalendarId` (nullable, `Restrict`) points a workspace at a calendar; null = use the default. Set from Workspace Settings (`PUT /api/workspaces/{id}/calendar`); start / end dates saved in a workspace must be working days of its calendar (C-3)

## Application Roles (`AppRoles` constants)

Single source of truth: `ActionTracker.Domain.Constants.AppRoles`. The `RoleSeeder` deletes the deprecated **Manager** and **User** roles on every startup.

- **Admin** — full access, unrestricted
- **Workspace Admin** — workspace-scoped admin (creates workspaces, manages users in their org unit)
- **PMO Head** — cross-portfolio visibility, approves charters
- **PMO Analyst** — portfolio reporting / data entry, no approval
- **Project Sponsor** — approves charter and closure
- **Project Manager** — full project artifact and team management
- **Project Coordinator** — supports PM (data entry, scheduling, documents)
- **Team Member** — assigned action items, log progress
- **Strategy Editor** — edits strategic objectives + KPIs in user's level-2 ancestor org unit and descendants only; assignment requires user OrgUnit at level ≥ 2
- **Viewer** — legacy read-only role

---

## Implemented Features

### Authentication & Authorization
- [x] Azure AD SSO via MSAL (login/logout, token refresh)
- [x] Dynamic authorization middleware (permission-based, not just role-based)
- [x] PermissionArea → Permission → RolePermission entity model
- [x] Org-unit-scoped data filtering (users see only their unit's data)
- [x] Angular `HasPermissionDirective` for conditional UI rendering
- [x] Role management UI
- [x] Strategy Editor role with `IStrategicScopeService` for org-unit subtree scoping (Strategic Objectives, KPIs, KPI Targets, KpiTarget documents)
- [x] Strategy Editor assignment guard — rejects users with no OrgUnit or OrgUnit at level < 2

### UI / UX
- [x] Fluent / Palantir design aesthetic
- [x] Dark and light theme toggle
- [x] **Brand theme from the KUmmand Center logo** — tokens live only in `src/assets/styles/_theme-variables.scss` (`_variables.scss` is aliases). Logo palette: navy `#0B1B4A`, royal `#2A5CF0`, blue `#4D8BFF`, sky `#7AADFF`, slate `#262A33`. Filled controls use `--accent-fill` + `--on-fill`; `--accent` is for text/icons/borders. The logo PNGs are **not** transparent — the header/drawer background (`--nav` / `--bg`) must stay exactly `#262A33` (dark) / white (light).
- [x] Charts: each chart component provides `provideCharts(withDefaultRegisterables())` (ng2-charts); chart containers need a fixed height
- [x] Login page with KU branding
- [x] Footer logo

### Core Functionality
- [x] Action item CRUD (create, edit, delete via offcanvas panel)
- [x] My Actions page (scoped to current user, stats cards, show-deleted toggle)
- [x] Created by Me section in My Actions
- [x] Default assignee on action items
- [x] Workspaces with workspace-membership access control (`IWorkspaceAccessResolver`) — privileged roles (Admin / PMO Head / PMO Analyst) see everything; everyone else only sees workspaces where they are an admin or member. Projects and action items inherit the same scoping.
- [x] **Private workspaces** (`Workspace.IsPrivate`) — visible only to the workspace's admins + members; hidden from everyone else (incl. Admin / PMO) and excluded from all their stats, dashboards and reports. Enforced by EF global query filters in `AppDbContext` keyed on `ICurrentUserService` (null user = system context, unrestricted). Code using `IgnoreQueryFilters()` on projects/action items must re-apply `WhereWorkspaceVisibleTo(_db)` (takes the db context since WO-2026-11 P-2, so the project-role clause comes with it).
- [x] Workspace direct user assignment (`WorkspaceMember` table + `GET/POST/DELETE /api/workspaces/{id}/members`)
- [x] **Workspace Settings page** (`/workspaces/:id/settings`) — single home for managing Admins, Members, **Project Managers**, **Strategic Project Approvers** (ordered chain with up/down reorder), and the Linked Strategic Plan. The workspace detail page now shows a read-only access summary + a "Configure Workspace" button (gated by `Workspaces.Edit`) instead of inline editors.
- [x] PM-list gating — project create/edit rejects PMs that aren't on the workspace's `WorkspaceProjectManager` list. The PM dropdown on the project form pulls from `GET /api/workspaces/{id}/project-managers`.
- [x] Optional Workspace ↔ Strategic Plan link (`Workspace.StrategicPlanId`, `PUT /api/workspaces/{id}/strategy`) — strategic-objective dropdown on project create is restricted to objectives from that plan; workspaces with no plan cannot create strategic projects
- [x] Workspaces with summary stats
- [x] Projects with role-based access (My Projects page)
- [x] Milestones with validation on status transitions
- [x] Dashboard with stat cards
- [x] **Dashboard hub with drill-down** — `/dashboard` (Overview), `/dashboard/actions`, `/dashboard/projects`, `/dashboard/strategy`. Every stat card / chart segment / list row opens a drawer of the records behind it (`DrilldownPanelComponent`), each linking to its page. Backed by `IDashboardAnalyticsService` (`GET /api/dashboard/my-actions | team-activity | action-items | projects | strategy`), scoped via `IWorkspaceAccessResolver` / `IStrategicScopeService`. Counting rules (open, overdue, due/age windows, project health, KPI achievement) live in `Features/Dashboard/DashboardRules.cs` and are shared with the action-item list filters (`openOnly`, `overdueOnly`, `isEscalated`, `dueBucket`, `ageBucket`) so a drill-down list always matches its chart — change both sides together.
- [x] Interactive Gantt chart with hover tooltips
- [x] Filtering & search
- [x] Export to Excel and Print to PDF (workspaces, projects, milestones, my actions)
- [x] Action item workflow (date freeze, status change approval, escalation notifications)
- [x] My Approvals page (pending reviews, my requests, approve/reject with comments)
- [x] Real-time workflow notifications via SignalR with toast popups
- [x] In-app notification system (grouped by date, filter by type/read status, mark as read, delete)
- [x] Project approval workflow — strategic projects use the workspace's sequential `WorkspaceStrategicProjectApprover` chain (one Pending request at level 1, each Approve advances). Operational projects bypass approval (PM is the sole authority — submit goes Draft → Active immediately). Old "sponsors + PM's line manager" resolver remains as fallback for legacy rows. (`ProjectWorkflowService`)
- [x] Project Approvals tab on My Approvals page
- [x] Date freeze enforcement (project, milestone, and action item dates locked after approval)
- [x] Strategic Objectives + KPIs CRUD with role-based org-unit scoping
- [x] Strategic Plans (CRUD, one active per org unit, time-bounded cycle, scope-aware filtering)
- [x] Strategy page (`/strategy`) with org-unit picker and per-unit plan view (header card + objectives list, permission-gated Edit / Archive)
- [x] PMO-initiated plan creation (Admin / PMO Head only) with fan-out notification (in-app + email) to in-scope Strategy Editors
- [x] Cross-org-unit read access for PMO Head, PMO Analyst, Project Manager, and Workspace Admin (`StrategicPlans.View` / `StrategicObjectives.View` / `KPIs.View`); writes still gated by the standard `*.Create` / `*.Edit` / `*.Delete` policies
- [x] Org-unit head designation (`HeadUserId`) with `IApprovalAuthorityResolver` walking to the closest level-3 ancestor for plan-approval routing (CR-001-H)
- [x] Strategic plan approval workflow — Submit → PendingApproval → Approved/Draft, with email + in-app notifications, "Strategy Approvals" tab on My Approvals, and approval history on the Strategy page (CR-001-D)
- [x] Post-approval edit lockdown via `IStrategicPlanLockGuard` — Approved plans block mutations to plan/objective/KPI/target unless an in-flight Revision request exists; Archived plans are always read-only (CR-001-E)
- [x] Revision cycle — `POST /api/strategic-plans/workflow/request-revision` opens a Revision request that temporarily unlocks edits; review re-locks the plan
- [x] Period-aware KPI Targets (Monthly/Quarterly/Semi-Annual/Yearly) with inline editing and Save All
- [x] Per-KPI-Target details — Baseline, DataSource, CalculationNotes, Direction (Higher / Lower / OnTarget) — with null-means-no-change semantics on bulk upsert
- [x] KPI Target evidence: up to 10 documents per target, scope-gated for Strategy Editors
- [x] **KPI Actuals & evidence (CR-001-F)** — period-aware grid (Quarter / Semi / Annual), Strategy Editor → level-3 head approval workflow with submit/review/history, evidence attachments on each actual (10-doc cap, scope-gated), achievement % colouring driven by `KpiDirection`, period-end submit guard (`KpiPeriodCalendar`), and a "KPI Actuals" tab on My Approvals. Actuals bypass the plan lock — editable even on Approved plans.
- [x] Workspace admin custom multi-select picker (filter across name/email/org-unit, lazy-loaded users)
- [x] Auto-launch Swagger UI on .NET host startup in Development
- [x] **Mail Server Configuration (Admin Panel)** — Admin-managed SMTP settings stored in `MailServerConfigs` table (Host, Port, UseSsl, From, Username, EncryptedPassword). Password encrypted at rest via ASP.NET Core Data Protection (`ISecretProtector`). `EmailSender` reads the active DB row at send time and falls back to `appsettings.json` `Smtp` section when no row exists. Gated by new `MailServerConfig.View` / `MailServerConfig.Edit` permissions; Admin role only by default.
- [x] **Data Sync (Admin Panel)** — Hangfire-hosted background jobs configured from `/admin/data-sync`. `DataSyncJobs` / `DataSyncJobRuns` tables hold the schedule (cron + time zone + enabled) and run history. First job: **HR Employee Info Sync** (`employee-info-sync`) copying `ku_employee_info` from KU_BOT_PROD (KU1ICDDWV008) into Action Center — full refresh via `DELETE` + `SqlBulkCopy` in one transaction, common-column matching, row-count safety guard. Source connection string is read from `DataSync:EmployeeInfo:SourceConnectionString` in the git-ignored `appsettings.json` (placeholders `<USERNAME>` / `<PASSWORD>` must be replaced). Gated by `DataSync.View` / `DataSync.Edit`. Hangfire dashboard at `/hangfire` is local-requests only. New jobs = implement `IDataSyncJobHandler` + register it in `DataSyncServiceCollectionExtensions`; the row is seeded automatically at startup.
- [x] **Work calendars — backend (WO-2026-11 C-1)** — `IWorkCalendarService` (`Application/Features/Calendars`) with admin API `/api/calendars` (list, get, create, update, soft delete, restore, `PUT /{id}/default`, `GET /{id}/usage`), gated by the `Calendars.View / Create / Edit / Delete` permissions (Admin: all, Workspace Admin: View). Default calendar "KU Standard" (Mon–Fri 08:00–17:00, Asia/Dubai) is seeded at startup by `WorkCalendarSeeder`. For any working-day maths call `GetForWorkspaceAsync(workspaceId)` (or `GetDefaultAsync()`) and use the returned `ResolvedWorkCalendar` (`IsWorkingDay`, `NextWorkingDay`, `AddWorkingDays`, `WorkingDaysBetween`) — do not hand-roll weekend checks. Rules: one default at all times; the default and any calendar a workspace uses cannot be deleted or deactivated (409). Resolved calendars are cached in the singleton `WorkCalendarCache`; every calendar write must go through the service so the cache is cleared.
- [x] **Work calendars — admin Calendars page (WO-2026-11 C-2)** — `/admin/calendars` (Admin Panel card "Calendars", `Calendars.View`; `features/admin/calendars/`, model `models/work-calendar.model.ts`, service `services/work-calendar.service.ts`). List with week / hours / exceptions / workspaces using each calendar, drawer to create / edit / view (details, weekly pattern, exceptions) with a month preview of the unsaved values, set default, soft delete / restore. The preview applies `shared/utils/work-calendar.util.ts`, a client copy of the `ResolvedWorkCalendar` rules — **change both together** (its spec repeats `ResolvedWorkCalendarTests`). Frontend only; the C-1 API is unchanged
- [x] **Work calendars — workspace setting + working-day date rule (WO-2026-11 C-3)** — Workspace Settings → **Working Calendar** (`GET /api/workspaces/{id}/calendar` for any signed-in user, 404 for a hidden private workspace; `PUT` same route gated by `Workspaces.Edit`; dropdown list `GET /api/workspaces/calendars` gated by `Workspaces.View`). **Rule (Q7 / Q8):** a start / end date that is created or changed must be a working day of the workspace's calendar — call `IWorkCalendarService.EnsureWorkingDaysAsync(workspaceId, [WorkingDateCheck.New(...) / .Changed(field, value, current)])` after the existing guards; it throws `NonWorkingDateException` (an `ArgumentException` → 400 naming the date and the next working day). Wired into `ActionItemService` create / update (before the standalone date-request branch), `ActionItemWorkflowService.CreateDateChangeRequestAsync` (request time — approval applies the request as it stands), `MilestoneService` and `ProjectService` create / update (planned dates only). Unchanged dates are never checked; actual dates and `DashboardRules` are untouched. **Frontend:** every date form uses `injectWorkspaceCalendar(() => workspaceId)` (`services/workspace-calendar.service.ts`), `<app-working-day-hint>` under each date input (same container as the input), `<app-working-days>` under the pair, and `nonWorkingDatesMessage` / `workingDayValidator` (`shared/validators/working-day.validator.ts`) before saving — **a new date field must do the same**. Overlapping exceptions now resolve deterministically (shortest range, non-working, own hours, shorter day, earlier start time, name, start date) — same order in `ResolvedWorkCalendar` and `work-calendar.util.ts`
- [x] **Planned and actual dates on action items (WO-2026-11 A-1)** — `ActualStartDate` / `ActualEndDate` (migration `Add_ActionItem_ActualDates`, which backfills `ActualEndDate` for Done items from `UpdatedAt`, or `CreatedAt` when `UpdatedAt` is still 0001-01-01). **Rule (Q9), in `Application/Features/ActionItems/ActionItemActualDates.cs`:** first move into In Progress sets actual start when empty; move into Done sets actual end (a value sent in the same PUT wins); leaving Done clears it; neither may be after today (workspace calendar's time zone, `TodayForWorkspaceAsync`), end ≥ start, end only while Done. **Any code that changes an action item's status must call `ActionItemActualDates.Resolve` / `ApplyStatusChange`** — today: `ActionItemService` create / update / `UpdateStatusAsync` / `ApplyApprovedStatusChangeAsync`, `ActionItemWorkflowService.ReviewRequestAsync`. Actual dates are outside the planned-date freeze, the date-change requests and the working-day rule. A Done item still refuses every PUT (422) except one carrying only `actualStartDate` / `clearActualStartDate` / `actualEndDate`. Response adds `actualEndVarianceDays`. **Frontend:** `<app-actual-dates-fields>` (`shared/components/actual-dates-fields`) in every action-item edit drawer, `shared/utils/actual-dates.util.ts` is the client copy of the checks — **change both together**; send only changed fields with `actualDatesPatch`. Planned dates are labelled "Planned start" / "Planned end" everywhere (also in the working-day messages for action items)
- [x] **Action-item fix package (WO-2026-11 A-1b)** — `IsStandalone` follows the project link: set on create from `ProjectId`, changed on update only when the save changes `ProjectId`; the client's `isStandalone` is ignored (`Application/Features/ActionItems/ActionItemStandalone.cs`). Edit drawers build the PUT body with `toActionItemUpdate` (`shared/utils/action-item-payload.util.ts`), which drops `isStandalone` — **a new action-item edit form must do the same**. Policies: POST `/api/action-items` → `ActionItems.Create`, PUT `/{id}` and PATCH `/{id}/status` → `ActionItems.Edit`; every action-item Edit button is behind `*appHasPermission="'ActionItems'; action: 'Edit'"` and `/actions/:id/edit` is guarded. `DefaultRolePermissionsSeeder` grants Team Member `ActionItems.Create` (production already did). Production role grants differ from the seeder (edited in the role UI) — before putting a policy on an existing endpoint, check production read-only. The seeder never re-adds a soft-deleted grant. The document endpoints check the owning record and `ActionItems.View` / `Edit` since A-4; the action-item comment endpoints were removed in A-3
- [x] **Sub-items on action items (WO-2026-11 A-2)** — API `/api/action-items/{id}/sub-items` (`ActionItemSubItemsController`, service `Application/Features/ActionItemSubItems`): reads `ActionItems.View`, writes `ActionItems.Edit`, `PATCH /{subId}/done` also for the sub-item's assignee; 409 (`SubItemParentLockedException`) while the parent is Done / Cancelled. **Rules in `ActionItemSubItemRules`:** dates within the parent's planned dates (end ≥ start; none after planned end; none before planned start when set) — checked on create and when a date changes; working days via `EnsureWorkingDaysAsync`; new assignees from `IWorkspaceService.GetAssignableUsersAsync`. Sub-items are outside the date freeze and the approval workflows, and never block a parent date change — the GET flags `isOutsideParentDates` and the detail page names those sub-items. **Delete / restore:** any code that soft-deletes or restores action items must call `ActionItemSubItemCascade.DeleteWithParentsAsync` / `RestoreWithParentsAsync` (today: `ActionItemService`, `ProjectService`). **Private workspaces:** both tables have query filters that follow the parent; re-apply `WhereWorkspaceVisibleTo` (sub-item overload) after `IgnoreQueryFilters()`. Responses carry `subItemCount` / `subItemDoneCount`; `GET /api/action-items/assigned-via-sub-items` feeds My Actions → "Assigned through a sub-item" (kept out of the stats cards and dashboards). Notifications: `IWorkflowNotificationHelper.NotifySubItemAssignedAsync` / `NotifySubItemCompletedAsync`, templates `ActionItem.SubItemAssigned` / `ActionItem.SubItemCompleted` (`EmailTemplateSeeder` is insert-only since A-3). Migration `Add_ActionItemSubItems` — applied to production 2026-11-24 (by the API's startup `MigrateAsync`; since A-3 that is switchable with `Database:MigrateOnStartup`). **Frontend:** `SubItemsSectionComponent` (`features/actions/action-detail/sub-items-section`), `<app-sub-item-count>` chip, `services/action-item-sub-item.service.ts`, `shared/utils/sub-item-dates.util.ts` is the client copy of `ActionItemSubItemRules` — **change both together**
- [x] **Action Updates replace comments on action items (WO-2026-11 A-3)** — API `ActionItemUpdatesController`: `/api/action-items/{id}/updates` and `/api/action-items/{id}/sub-items/{subId}/updates` (list = submitted + my drafts, create with optional `submit`, edit my draft, `POST /{updateId}/submit`, delete my draft) and `GET /updates/counts`; reads `ActionItems.View`, writes `ActionItems.Edit`; 404 for a hidden parent / sub-item and for someone else's draft, **409 (`ActionItemUpdateLockedException`) for any change to a submitted update, every role incl. Admin**; allowed whatever the item's status. Service `Application/Features/ActionItemUpdates`. **Draft privacy and private workspaces live in the `ActionItemUpdate` query filter** — do not reach updates with `IgnoreQueryFilters()` in request code. **Any code that writes an update for the system (e.g. "Give direction") must create it as Submitted.** Notifications only on submit: `IWorkflowNotificationHelper.NotifyActionUpdateSubmittedAsync`, template `ActionItem.UpdateSubmitted`. **E-mail placeholders: HTML-encode every value you pass** — `EmailSender` puts them into the body as they are and HTML-decodes them only for the plain-text subject (`ResolveSubject`); see `BuildSubItemPlaceholders` / `BuildActionUpdatePlaceholders`. The old `/api/action-items/{id}/comments` routes, their `IActionItemService` methods and the `comments` array of action-item responses are gone; `/api/comments` keeps reading old action-item comments but refuses writes for them, and checks parent visibility for `ActionItem` / `Project` / `Milestone` / `ProjectRisk` on every call. Migration `Add_ActionItemUpdates` (adds `SubItemId`, `Status` — database default Submitted —, `SubmittedAt`, `IsDeleted`; backfill: existing rows → Submitted, action-item comments copied from `Comments`) — applied to production 2026-11-25 (8 comments copied); the same copy INSERT with a read-only preview is `backend/ActionTracker/ActionTracker.Infrastructure/Data/Scripts/A3-copy-action-item-comments.sql` (re-runnable; a test keeps it equal to the migration). The direction e-mail now uses the seeded key `ActionItem.DirectionGiven`. **Frontend:** `ActionUpdatesSectionComponent` (`features/actions/action-detail/action-updates-section`) on the detail page and, compact, under each sub-item ("Updates (n)" toggle in `SubItemsSectionComponent`); `services/action-update.service.ts`, `models/action-update.model.ts` (named `ActionUpdate…`: `ActionItemUpdate` is the action-item PUT payload). `CommentsSectionComponent` is unchanged and still used for projects, milestones and risks (Q12)
- [x] **Startup migration switch (WO-2026-11 A-3, fix 0)** — `Database:MigrateOnStartup` (default true when missing). `Program.cs` runs migration + seeders through `Infrastructure/Data/Startup/DatabaseStartup.cs`: off → pending migrations are named in a warning and not applied, the API still starts, and a seeder whose tables / columns are missing is skipped with a warning (SQL 207 / 208 also skip). **A new startup seeder must be registered through `RunSeederAsync` with the entity types it needs.** The local git-ignored `appsettings.json` sets it to false. `EmailTemplateSeeder` is insert-only since A-3 (never changes an existing template); the integration-test host replaces `IEmailSender` with `RecordingEmailSender`
- [x] **Attachments on action items, sub-items and action updates (WO-2026-11 A-4)** — `DocumentService` (`Application/Features/Documents`) resolves the owning record on **every** list / upload / download / delete and answers 404 when the caller cannot see it — the record's own rule: query filters for the action-item and project types, strategic read scope for KPI types (draft targets count). **A new document type must be added to `DocumentEntityTypes` with its visibility rule** (anything else is 400). Action-item types: `ActionItems.View` / `Edit` (evaluated in `DocumentsController`, passed as `DocumentCaller`), 409 (`DocumentLockedException`) while the item is Done / Cancelled (item and sub-item files) or once an update is submitted, a draft update's files only through its author, delete right = uploader / item creator / item assignees / sub-item assignees / Admin (`IUserRoleService`, from the DB), `canDelete` on each file. Caps 10 (`MaxDocumentsPerEntity`). No migration. **Frontend:** `DocumentsSectionComponent` inputs `canUpload`, `lockedNote`, `maxFiles`, `compact`, output `countChange`; "Files (n)" toggles in `SubItemsSectionComponent` and `ActionUpdatesSectionComponent`; "Attach files" in the update form uploads after creating the draft and before submitting (files cannot be added after submit)
- [x] **Custom tags on action items (WO-2026-11 A-5)** — `Features/Tags` (`ActionItemTagService`, `TagRules`), `TagsController`: `GET /api/workspaces/{id}/tags` (suggestions, 404 for a hidden workspace) and `GET /api/action-items/my-actions/tags`, both `ActionItems.View`. Tags are saved with the item (`tags` on POST / PUT `/api/action-items`, names; PUT: `[]` clears, missing = keep); lists filter with `tagIds` (any of, **by id**, needs `ActionItems.View`). **Rules:** 10 per item, 30 characters, trimmed / collapsed, de-duplicated without regard to case, no commas or control characters — server `TagRules` and client `shared/utils/tag-rules.util.ts`, **change both together**. **Tags never cross workspaces:** names are only resolved in the item's workspace (`ResolveAsync`), a move carries the tags over by name (D2), responses drop any tag of another workspace. Done item: tags refused like any field (422); Cancelled: editable (D1). **Migration `Add_ActionItemTags` — applied to production 2026-11-25 (~14:40 UTC+4).** While a database lacks the tables, `ITagSchemaState` (`TagSchemaState`, re-checked once a minute) switches tags off: responses carry `tags: []`, tag saves and the tag filter get 503, everything else works — **action-item queries must never `Include` or join the tag tables; load tags through `IActionItemTagService.FillTagsAsync`.** Frontend: `<app-tag-input>` on the action form, the action detail edit drawer and the My Actions panel (D4 — other drawers leave tags alone, so they must not send `tags`), `<app-tag-chips>` on the lists, `<app-tag-filter>` on My Actions and workspace detail, Tags column in the three Excel exports; edit forms send `tags` only when changed (`tagsPatch`). Team Actions (`/management`) and the Reports CSV unchanged; dashboard tag widget = D-2
- [x] **Internal project classification (WO-2026-11 P-1)** — `Features/ProjectClassifications` (`ProjectClassificationService`, `ProjectClassificationRules`), `WorkspaceProjectClassificationsController` at `/api/workspaces/{id}/project-classifications`: GET needs `Projects.View` **or** `Workspaces.View` (checked in the controller) and a workspace the caller sees — or whose Project Managers list they are on; POST / PUT `/{cid}` / PUT `/reorder` / DELETE `/{cid}` need `Workspaces.Edit` and a workspace the caller sees (404 otherwise); 409 for a duplicate name (any case) or a delete while any project — also a soft-deleted one — uses it. Projects: `internalClassificationId` on POST / PUT (PUT: missing = keep, `clearInternalClassification: true` removes; an active one of the project's own workspace, the one it already has accepted even when inactive; changeable in every status the edit accepts — outside the date freeze and approvals), the three `internalClassification*` fields on every response (only a live one of the project's own workspace), list filters `internalClassificationId` / `unclassified`. Name rules: server `ProjectClassificationRules`, client `shared/utils/project-classification-rules.util.ts` — **change both together**. **Migration `Add_WorkspaceProjectClassifications` adds a column to `Projects`: the API with this code fails on every project-loading query (SQL 207, incl. all action-item lists) until it is applied — migrate first, then restart (older code ignores the new column).** Applied to production 2026-11-26 (~08:30 UTC+4). Frontend: Workspace Settings → `ProjectClassificationsSectionComponent`; `<app-project-classification-select>` in all four project edit screens (project form, project detail drawer, My Projects drawers, workspace detail drawer) — **a new project edit form must add it and send `internalClassificationPatch`**; "Classification (global)" label for `ProjectType` there and on the detail page; column / chip on My Projects, workspace detail and the strategy page list; filter on the workspace project list; Excel column / row. Dashboard grouping by classification = D-2
- [x] **Error messages (WO-2026-11 P-1 step 2)** — `errorInterceptor` shows the server's reason for 403 / 409 / 422 / 503 and other uncommon statuses (400 / 404 / 500 unchanged), one task later; `showErrorHere` in 81 on-page error handlers (see Conventions → Error messages); the workspace detail project drawer's errors show in the drawer; the 503 tag-filter toasts are gone. Frontend only
- [x] **Project people and access — backend (WO-2026-11 P-2 step 1; the UI is step 2, not built yet)** — the lead PM, sponsors and `ProjectMember` people see their project and everything under it (milestones, risks, action items, sub-items, updates, files, comments, the item's own tags, the project's own classification), **also in a private workspace**, in lists, My Projects (`GET /api/projects?mine=true`), dashboards and reports — and **nothing else of the workspace**. **How:** `AppDbContext.ProjectRoleProjectIds` / `ProjectRoleWorkspaceIds` (worked out per context and user, reset after a save touching projects / sponsors / project members) are parameters of the private-workspace filters; the Workspace row is let through only for joins. **Never write a filter that navigates Workspace → Project** (EF Core does not detect the cycle: stack overflow). **Reads of a workspace's own data must use the old rule**: `WhereWorkspaceLevelVisibleTo(userId)` in queries and `[WorkspaceLevelAccess("…")]` on every controller whose routes take a workspace id — `WorkspaceRouteAccessTests` fails for a workspace route without it. `WhereWorkspaceVisibleTo` now takes the db context (`.WhereWorkspaceVisibleTo(_db)`; overloads ActionItem, sub-item, tag link, Project, Milestone, ProjectRisk) — re-apply it after every `IgnoreQueryFilters()`. **Rights:** permission policies still apply (D1); callers who see the workspace at workspace level keep exactly their rights (D2); through a role, `IProjectAccessService` (`Application/Features/ProjectPeople`, `new ProjectAccessService(db)`) is the ceiling — PMs `EnsureCanManageAsync`, team members `EnsureCanWorkOnItemAsync` on items / sub-items assigned to them, viewers and sponsors read only, no moves, read-only tags (D5); **a new write path under a project must call one of them** (403 `ProjectAccessDeniedException`). An additional PM may submit (D3); routing, reviewers and notifications stay with the lead PM. People API `ProjectPeopleController` (`/api/projects/{id}/people`, `/people-lookup`, `/{id}/assignable-users`, `/{id}/calendar`); project POST / PUT take `additionalManagerUserIds` / `viewerUserIds` / `teamMemberUserIds` (PUT: missing keeps); additional PMs must be on the workspace PM list; project-only PMs cannot add PMs or change the lead (D4); notification `Project.PersonAdded` (HTML-encoded, never the actor; also a new lead PM — D10). Sub-item assignee pool = workspace pool + the project's team members. Policies added (D11): `Projects.View` on project GETs and the workflow GETs, `Projects.Create` on POST, `Projects.Edit` on PUT; `Milestones.View` / `Create` / `Delete` / `Edit` (baseline) — **PUT milestone deliberately has none** (the milestone page offers Edit to everyone; Team Members lack `Milestones.Edit` in production). Gaps closed (D12): workflow requests, reviewer lists, risk restore, workspace people lists, item / project creation into hidden places. **Migration `Add_ProjectMembers` (one new table, no backfill) — applied to production 2026-11-26 (~13:54 UTC+4). While pending (another database), every signed-in request on workspaces / projects / items fails (SQL 208); migrate first, then restart (older code ignores the table)**
- [ ] Project people UI (WO-2026-11 P-2 step 2)
- [ ] Gantt shading of non-working days from the workspace calendar (WO-2026-11 P-3)
- [ ] Scheduled reminders (automated due-date reminders) — Hangfire is now available for this
- [ ] Audit trail / activity log
- [ ] Advanced search (cross-entity, full-text)

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
- **Permissions**: Always use the permission directive for protected UI — the actual syntax is `*appHasPermission="'Area'; action: 'Action'"` (optionally `; else tpl`); in TypeScript use `PermissionStateService.hasPermission('Area', 'Action')`
- **Focus after a UI change**: the app runs `provideZoneChangeDetection({ eventCoalescing: true })`, so the view updates on the next animation frame — move focus to an element that appears in response to an event with `afterNextRender(..., { injector })`, not `setTimeout`
- **Confirm dialogs**: use the shared `ConfirmDialogComponent` (`open()` + `(confirmed)`); it moves its modal to `<body>` so it is not trapped under Bootstrap's backdrop. It does not restore focus on close — the caller should
- **HTTP**: Centralized interceptor in `core/` for auth tokens and error handling
- **Error messages**: `errorInterceptor` toasts every failed request once, with the server's reason for 403 / 409 / 422 / 503 (`serverReason`: `message`, else `errors`; `detail` only on a 400), one task after the error. **A handler that shows the reason on the page (alert, drawer, dialog, field) must call `showErrorHere(err)` (`core/http/server-reason.ts`) in the handler itself** — it returns the reason (null → own text) and keeps the toast away; after a timer or a wait for other requests is too late (claim each failure in its own `catchError`). A handler that toasts the reason itself uses `err?.error?.message` so ngx-toastr drops the identical interceptor toast. Where the page alert sits behind an open drawer, do not call it (the toast is the visible message)
- **Proxy**: Dev API calls proxied via `proxy.conf.json` — do not hardcode backend URLs

### Database
- **Migrations**: EF Core code-first migrations
- **Connection**: SQL Server instance `localhost`, database `ActionTrackerDb`
- **Soft deletes**: Use `IsDeleted` flag, never hard delete user data
- **migrations**: do not do migrations that make data loose



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
