# KU Action Tracker - Backend Overview

> Last updated: 2026-11-24 (Sub-items on action items — WO-2026-11 A-2: `ActionItemSubItem` / `ActionItemSubItemAssignee`, `ActionItemSubItemsController` at `/api/action-items/{actionItemId}/sub-items` (reads `ActionItems.View`, writes `ActionItems.Edit`, ticking also allowed for the sub-item's assignee), `GET /api/action-items/assigned-via-sub-items`, `subItemCount` / `subItemDoneCount` on action-item responses, private-workspace query filters on both new tables, delete / restore together with the parent (and its project), two `IWorkflowNotificationHelper` methods + e-mail templates `ActionItem.SubItemAssigned` / `ActionItem.SubItemCompleted`, migration `Add_ActionItemSubItems` (applied to production 2026-11-24 at API startup). Earlier the same day: action-item fix package WO-2026-11 A-1b: `IsStandalone` follows the project link (`ActionItemStandalone`; the client's `isStandalone` is ignored on POST and PUT), `ActionItems.Create` on POST `/api/action-items`, `ActionItems.Edit` on PUT `/{id}` and PATCH `/{id}/status`, `DefaultRolePermissionsSeeder` grants Team Member `ActionItems.Create` (as production already does). Earlier the same day: planned and actual dates on action items — WO-2026-11 A-1: `ActionItem.ActualStartDate` / `ActualEndDate`, `ActionItemActualDates` rule shared by every status path, actual-date fields on PUT `/api/action-items/{id}`, `actualEndVarianceDays` on the response, PUT on a Done item accepted when it carries only actual dates, migration `Add_ActionItem_ActualDates` with the Done backfill. Previous: 2026-11-23 workspace calendar setting + working-day date rule — WO-2026-11 C-3: `GET/PUT /api/workspaces/{id}/calendar`, `GET /api/workspaces/calendars`, `IWorkCalendarService.EnsureWorkingDaysAsync` called by the action-item, milestone, project and date-change-request services, `NonWorkingDateException`, deterministic tie-break between overlapping exceptions. Earlier the same day: work calendars — WO-2026-11 C-1: `WorkCalendar` entities, `IWorkCalendarService` + calculation API, `/api/calendars` admin API, `Calendars` permission area, migration `Add_WorkCalendars`; C-2 note: the admin Calendars page now consumes this API, no backend change)

## Tech Stack

- **Framework:** .NET 9 / ASP.NET Core
- **ORM:** Entity Framework Core (SQL Server)
- **Identity:** ASP.NET Core Identity
- **Authentication:** Dual JWT (Local HS256 + Azure AD OIDC)
- **Real-time:** SignalR
- **Validation:** FluentValidation
- **Logging:** Serilog (structured logging)
- **API Docs:** Swagger / Swashbuckle

---

## Architecture

Four-layer clean architecture:

```
ActionTracker.API/            -> Controllers, Middleware, Configuration, SignalR Hubs
ActionTracker.Application/    -> Business Logic, DTOs, Service Interfaces, Validators
ActionTracker.Infrastructure/ -> Data Access, EF Core, Auth, Authorization
ActionTracker.Domain/         -> Entities, Enums, Constants
```

---

## Authentication & Authorization

### Dual JWT Authentication

| Scheme | Description |
|--------|-------------|
| `LocalBearer` | HS256 symmetric key signing for local email/password users |
| `AzureAD` | OpenID Connect discovery against Microsoft Entra ID v2.0 |
| `MultiAuth` | Policy scheme that routes to LocalBearer or AzureAD based on `tid` claim presence |

### Authentication Flows
- **Local Login:** `POST /auth/login` -> validates credentials -> returns JWT access + refresh tokens
- **Azure AD Login:** `POST /auth/azure-login` -> validates MSAL token -> auto-provisions user if first login -> returns JWT tokens
- **Token Refresh:** `POST /auth/refresh-token` -> rotates refresh token -> returns new token pair
- **Logout:** `POST /auth/logout` -> revokes all refresh tokens for user

### Authorization Model

**Role-Based Access Control (RBAC)**
- Centralized in `ActionTracker.Domain.Constants.AppRoles` (single source of truth)
- Pre-seeded roles (10): **Admin**, **Workspace Admin**, **PMO Head**, **PMO Analyst**, **Project Sponsor**, **Project Manager**, **Project Coordinator**, **Team Member**, **Strategy Editor**, **Viewer**
- Deprecated roles **Manager** and **User** are auto-deleted by `RoleSeeder` on every startup (idempotent)
- ASP.NET Identity roles with role claims in JWT
- `DefaultRolePermissionsSeeder` seeds a per-role permission matrix on startup, idempotent (skips rows that already exist). A (role, area, action) key counts as existing in any state — in effect, inactive or soft-deleted — so the seeder never duplicates a grant, never changes an existing row and never restores a grant removed through the role UI (which soft-deletes). Team Member gets View + Create + Edit on ActionItems (Create added in WO-2026-11 A-1b, when POST `/api/action-items` started requiring it; production already had it)
- **Strategy Editor** is granted View/Create/Edit/Delete on Strategic Objectives and KPIs, plus View on Dashboard / OrgChart / Notifications. Effective scope is enforced at the service layer via `IStrategicScopeService`

**Fine-Grained Permissions (RBAC + ABAC Hybrid)**
- Permissions defined as Area.Action pairs (e.g., `Projects.View`, `ActionItems.Delete`)
- **20 Areas:** Dashboard, Workspaces, Projects, Milestones, ActionItems, StrategicObjectives, KPIs, Reports, OrgChart, UserManagement, PermissionsManagement, Roles, EmailTemplates, Notifications, Risks, **StrategicPlans**, **KpiActuals**, **MailServerConfig**, **DataSync**, **Calendars** (View / Create / Edit / Delete — Admin: all, Workspace Admin: View)
- **7 Actions:** View, Create, Edit, Delete, Approve, Export, Assign
- Stored in `RolePermission` (role-level) and `UserPermissionOverride` (user-level) tables
- **Effective Permissions** = Role Permissions + User Overrides (denials take precedence)
- Checked at runtime via `PermissionRequirement` + `PermissionAuthorizationHandler`

**Authorization Policies**
| Policy | Requirement |
|--------|------------|
| `LocalOrAzureAD` | Authenticated via either scheme |
| `AdminOnly` | Admin role |
| `AdminOrManager` | Admin or Manager role |
| `{Area}.{Action}` | Dynamic policies auto-generated from PermissionPolicies constants |

**Org Unit Scoping**
- Users assigned to org unit via `ApplicationUser.OrgUnitId`
- Workspaces linked to org unit via `Workspace.OrgUnitId`
- `IOrgUnitScopeResolver` resolves visible org units (assigned unit + all descendants)
- Controllers filter queries by `VisibleOrgUnitIds`

**Strategic Scope Service (`IStrategicScopeService`)**
- Implementation: `Infrastructure.Services.StrategicScopeService`
- Adds a second, role-driven layer of scoping for **Strategic Objectives**, **KPIs**, **KPI Targets**, and **`KpiTarget` documents**
- `GetVisibleOrgUnitIdsAsync(userId)`:
  - **Admin** → returns `null` (unrestricted)
  - **Strategy Editor** → returns user's org-unit IDs from `IOrgUnitScopeResolver` (level-2 ancestor + descendants); empty list when user has no `OrgUnitId`
  - Any other role → returns `null` (unrestricted; existing permission policies decide)
- `EnsureCanWriteAsync(userId, orgUnitId)` throws `UnauthorizedAccessException` if `orgUnitId` is outside the user's visible set
- Wired into `StrategicObjectiveService`, `KpiService`, and `DocumentService` (the latter only for `KpiTarget` documents)
- **Strategy Editor role assignment** (`UserManagementService.UpdateRoleAsync`) is rejected unless the target user has an active org unit at level 2 or deeper

---

## API Controllers & Endpoints (24 Controllers)

### AuthController (`/api/auth`)
| Method | Endpoint | Description |
|--------|----------|-------------|
| POST | `/login` | Local email/password authentication |
| POST | `/azure-login` | Azure AD federated login (validates MSAL token, auto-provisions) |
| POST | `/refresh-token` | Refresh JWT pair with token rotation |
| POST | `/logout` | Revoke all refresh tokens for current user |

### ActionItemsController (`/api/action-items`)
| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/` | Paginated action items (filtered, org-unit scoped). Dashboard drill-down filters: `openOnly`, `overdueOnly`, `isEscalated`, `dueBucket`, `ageBucket` (definitions in `ActionItemRules`; an unknown bucket key is ignored) |
| GET | `/my-actions` | Items assigned to current user |
| GET | `/assigned-via-sub-items` | WO-2026-11 A-2. Items where the caller is assigned to a live sub-item but **not** to the item itself (My Actions → "Assigned through a sub-item"). User taken from the token; status / priority / search filters, paging and sorting apply; assignee / creator / include-deleted / scope fields of the filter are ignored. Each row adds `mySubItemCount`. Not part of `/my-stats`, `/my-actions` or any dashboard count. Requires `ActionItems.View` |
| GET | `/created-by-me` | Items created by current user |
| GET | `/my-stats` | Stats for user's assigned items (total, critical, in-progress, completed, overdue) |
| GET | `/{id}` | Single action item by GUID |
| POST | `/` | Create action item (auto-generates ActionId ACT-001). Requires `ActionItems.Create` (WO-2026-11 A-1b). `isStandalone` in the body is ignored — the flag is true exactly when no `projectId` is given (see "Standalone flag" below). Refused with 400 when a created or changed date is not a working day in the workspace calendar (WO-2026-11 C-3, see "Working-day rule" below); the reason comes back as `ApiResponse.message` |
| PUT | `/{id}` | Update action item. Requires `ActionItems.Edit` (WO-2026-11 A-1b). `isStandalone` in the body is ignored — the flag changes only when the save changes `projectId` (see "Standalone flag" below). `startDate` / `dueDate` are the planned start / planned end. Refused with 400 when a created or changed date is not a working day in the workspace calendar (WO-2026-11 C-3, see "Working-day rule" below) — for a standalone item the check runs before the date edit is turned into a date-change request. Actual dates (WO-2026-11 A-1, see "Actual dates" below): `actualStartDate`, `clearActualStartDate`, `actualEndDate` — 400 with the reason as `ApiResponse.message` when one is refused (every `ArgumentException` is now returned that way). A Done item accepts a payload that carries only actual dates; anything else is 422 "Completed action items cannot be edited." |
| PATCH | `/{id}/status` | Update status (Done sets Progress=100%; records / clears actual dates — WO-2026-11 A-1). Requires `ActionItems.Edit` (A-1b) |
| DELETE | `/{id}` | Soft delete (requires ActionItems.Delete). Its live sub-items are soft-deleted with it and marked `DeletedWithParent` (WO-2026-11 A-2) |
| PATCH | `/{id}/restore` | Restore soft-deleted item (requires ActionItems.Edit). Restores exactly the sub-items deleted with it; a sub-item deleted on its own stays deleted (A-2) |
| POST | `/process-overdue` | Mark past-due items as Overdue (`Roles = "Admin"` — a role check, not a permission policy) |
| GET | `/{id}/comments` | Get comments for action item |
| POST | `/{id}/comments` | Add comment (no permission policy; to be covered by A-3) |
| PUT | `/{actionItemId}/comments/{commentId}` | Update comment (no permission policy; author only, checked in the service) |
| DELETE | `/{actionItemId}/comments/{commentId}` | Delete comment (no permission policy; author only, checked in the service) |

Every action-item response (list, single, create, update) carries `subItemCount` and
`subItemDoneCount` (live sub-items, through the sub-item query filters; one grouped query per
page). They are display only: no count, stat or dashboard figure includes sub-items (A-2).

Every endpoint needs a signed-in user (controller-level `[Authorize]`). A user without the policy
named above gets 403. Since A-1b the create / edit / status writes carry a policy; the comment
endpoints, and `POST` / `DELETE /api/documents` for action items, still do not (left for A-3 / A-4).

### WorkspacesController (`/api/workspaces`)
| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/` | All active workspaces scoped via `IWorkspaceAccessResolver` (admin/member-driven, with Admin / PMO Head / PMO Analyst unrestricted) |
| GET | `/summary` | Aggregate stats scoped to the caller's visible workspaces. Optional `workspaceId` query narrows to a single workspace. |
| GET | `/{id}` | Workspace details (includes `members`, `strategicPlanId`, `strategicPlanName`) |
| GET | `/{id}/stats` | Workspace statistics (projects, action items, completion rates) |
| GET | `/by-admin/{adminUserId}` | Workspaces for specific admin |
| POST | `/` | Create workspace (accepts optional `strategicPlanId`) |
| PUT | `/{id}` | Update workspace (accepts optional `strategicPlanId`) |
| GET | `/org-units` | Org units for dropdown |
| GET | `/active-users` | Active users for the workspace admin picker — returns `{Id, DisplayName, Email, OrgUnitName}` (searchable across name, email, and org unit) |
| GET | `/strategic-plans` | Strategic plans for the workspace strategy dropdown |
| GET | `/{id}/assignable-users` | Active users eligible to be picked as action-item assignees in this workspace — union of admins, members, and project managers. Used by every workspace-context form that selects users (action-item, project, milestone, risk). Returns `AssignableUserDto[]` (`{ userId, userName, email, orgUnitName, sources[] }`) where `sources` is any of `"Admin" | "Member" | "ProjectManager"`. Sorted server-side by `userName`. |
| GET | `/{id}/members` | Regular (non-admin) members of a workspace |
| POST | `/{id}/members` | Add a regular member to a workspace (gated by `Workspaces.Edit`) |
| DELETE | `/{id}/members/{userId}` | Remove a regular member from a workspace |
| POST | `/{id}/members/from-org-unit` | Bulk-add every active user assigned to the given org unit (body `{ orgUnitId }`) as workspace members. Already-existing members are silently skipped. Returns `{ added, skippedAlreadyMembers, totalUsersInOrgUnit }`. Permission: `Workspaces.Edit` |
| GET | `/{id}/project-managers` | Users eligible to be picked as Project Manager in this workspace |
| POST | `/{id}/project-managers` | Add a user to the workspace's PM list (gated by `Workspaces.Edit`). Side effect: auto-grants the user the `Project Manager` role if they don't already have it |
| DELETE | `/{id}/project-managers/{userId}` | Remove a user from the workspace's PM list |
| GET | `/{id}/strategic-project-approvers` | Ordered approval chain for strategic projects in this workspace |
| POST | `/{id}/strategic-project-approvers` | Append a user to the chain (gated by `Workspaces.Edit`) |
| DELETE | `/{id}/strategic-project-approvers/{userId}` | Remove an approver; remaining entries re-densify their `Order` |
| PUT | `/{id}/strategic-project-approvers/reorder` | Replace the chain in the given UserId order (must list every existing approver) |
| PUT | `/{id}/strategy` | Link or unlink a strategic plan (`{ strategicPlanId: Guid? }`, gated by `Workspaces.Edit`) |
| GET | `/calendars` | Active work calendars for the Workspace Settings "Working Calendar" dropdown — `{ id, name, description, timeZoneId, isDefault, days[7] }`, default first then by name (requires `Workspaces.View`; WO-2026-11 C-3) |
| GET | `/{id}/calendar?from=&to=` | The calendar that applies to the workspace (its own, or the default) for date forms: `{ workspaceId, workCalendarId (own setting, null = default), calendarId, calendarName, timeZoneId, isDefault, isInherited, from, to, days[7], exceptions overlapping from..to }`. Default window one year back to three years ahead; `to < from` or more than 3660 days = 400. Any signed-in user (`LocalOrAzureAD`, like `assignable-users`); 404 for an unknown workspace or a private one the caller is not part of (C-3) |
| PUT | `/{id}/calendar` | Point the workspace at a calendar or back at the default — body `{ workCalendarId: Guid? }` (null = default). 404 when the caller cannot see the workspace (private filter + `IWorkspaceAccessResolver`); 400 for an unknown, deleted or inactive calendar. Returns the same shape as the GET. Dates saved afterwards follow the new calendar; existing records are not re-checked (requires `Workspaces.Edit`; C-3) |
| DELETE | `/{id}` | Soft delete (sets IsActive=false) |
| PATCH | `/{id}/restore` | Restore deleted workspace |

### ProjectsController (`/api/projects`)
| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/` | Paginated, filtered projects (org-unit scoped). Optional `strategicObjectiveIds` query param (repeated) returns only projects linked to one of those objectives — used by the strategy page's in-page Strategic Projects section. |

### StrategicObjectivesController (`/api/strategicobjectives`) — by-plan addition
| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/by-plan/{planId}` | Returns all non-deleted strategic objectives that belong to the specified `StrategicPlan`. Used by the strategy page to scope objectives strictly to the currently-viewed plan; refuses to return data when the plan's owning org unit is outside the caller's role-based scope. |
| POST | `/` | Create objective. The DTO now accepts an optional `StrategicPlanId`: when supplied, the service validates the plan exists, isn't archived, isn't locked, and the request's `OrgUnitId` is inside the plan's subtree (via `IOrgUnitScopeResolver.GetDescendantOrgUnitIdsAsync`) — then attaches the objective to that plan directly. Lets the Strategy page create objectives in sub-units that don't have their own plan while keeping them linked to the parent plan. When `StrategicPlanId` is omitted (admin / legacy flow) the service falls back to auto-linking the new row to the org unit's newest non-archived plan, or `null` if none exists. |
| GET | `/{id}` | Single project |
| POST | `/` | Create project (status=Draft). Planned start / end: Refused with 400 when a created or changed date is not a working day in the workspace calendar (WO-2026-11 C-3, see "Working-day rule" below) |
| PUT | `/{id}` | Update project. Changed planned start / end: Refused with 400 when a created or changed date is not a working day in the workspace calendar (WO-2026-11 C-3, see "Working-day rule" below); the actual start date is not checked |
| GET | `/{id}/stats` | Project statistics (milestones, action items, rates) |
| GET | `/strategic-objectives-for-workspace/{workspaceId}` | Objectives for workspace |
| DELETE | `/{id}` | Soft delete (cascades to milestones/items, and to the items' sub-items — WO-2026-11 A-2) |
| PATCH | `/{id}/restore` | Restore project (its items' sub-items deleted with them come back too — A-2) |

### MilestonesController (`/api/projects/{projectId}/milestones`)
| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/` | All milestones for project (ordered by sequence) |
| GET | `/{milestoneId}` | Single milestone |
| POST | `/` | Create milestone (auto-generates MS-YYYY-001). Planned start / due: Refused with 400 when a created or changed date is not a working day in the workspace calendar (WO-2026-11 C-3, see "Working-day rule" below) |
| PUT | `/{milestoneId}` | Update milestone. Changed planned start / due: Refused with 400 when a created or changed date is not a working day in the workspace calendar (WO-2026-11 C-3, see "Working-day rule" below); the actual completion date is not checked |
| DELETE | `/{milestoneId}` | Soft delete |
| GET | `/{milestoneId}/stats` | Action-item stats for milestone |
| POST | `/baseline` | Lock milestone dates (save baseline values) |

### ProjectRisksController (`/api/projects/{projectId}/risks`)
| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/` | Paginated risks for project |
| GET | `/{riskId}` | Single risk |
| POST | `/` | Create risk (auto-generates RISK-001 per project) |
| PUT | `/{riskId}` | Update risk |
| DELETE | `/{riskId}` | Soft delete |
| PATCH | `/{riskId}/restore` | Restore soft-deleted risk |
| GET | `/stats` | Risk stats summary for project |

### DashboardController (`/api/dashboard`)
| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/kpis` | High-level KPI metrics (Admin/Manager) |
| GET | `/management` | Full management dashboard (KPIs, status, workload, at-risk, activity) |
| GET | `/team-workload` | Per-user workload stats |
| GET | `/status-breakdown` | Action items grouped by status |
| GET | `/my-actions` | Caller's own assignments for the Overview charts: totals plus slices by status, by priority (open only) and by due window (open only). Policy `Dashboard.View` |
| GET | `/team-activity` | Per-person action-item counts (total, open, in progress, completed, overdue) across visible workspaces, busiest first. Policy `Dashboard.View` |
| GET | `/action-items` | Action-item performance: totals, completion / on-time rates, slices by status / priority / due window / age, 8-week created-vs-completed trend, top workspaces, team. Policy `Dashboard.View` |
| GET | `/projects` | Every visible project as a row with progress, health, action-item and milestone counts, plus portfolio totals and late / upcoming milestones. Policy `Dashboard.View` |
| GET | `/strategy?year=` | Plans, objectives and KPIs in strategic scope with KPI achievement for the year (default: current), rolled up to objectives and plans, plus linked strategic projects. Policy `StrategicPlans.View` |

The first four endpoints rely only on the global query filters. The five analytics endpoints (`IDashboardAnalyticsService`) are additionally scoped to the caller: action items and projects by `IWorkspaceAccessResolver`, strategy by `IStrategicScopeService`; `/my-actions` is everything assigned to the caller regardless of workspace. Archived plans are excluded from `/strategy`. Each query is a plain filter + column projection, aggregated in memory with the rules in `Features/Dashboard/DashboardRules.cs`:

- `ActionItemRules` — *open* (To Do / In Progress / In Review / Overdue), *overdue* (open and flagged Overdue **or** past its due date), due windows (`overdue` / `next7` / `next30` / `later`) and age windows (`0-7` / `8-30` / `31-90` / `90+`). Each rule exists as an in-memory function and as an `Expression` used by the list filter — `DashboardAnalyticsServiceTests` asserts they agree.
- `ProjectRules` — progress (share of action items done; falls back to mean milestone completion; Completed = 100) and health (Delayed = active past planned end; At risk = active with overdue action items or late milestones; else On track).
- `KpiAchievement` — direction-aware achievement % (now also used by `KpiActualService`) and health bands (≥ 90 on track, ≥ 60 at risk, below off track, none = no data).

### NotificationsController (`/api/notifications`)
| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/` | Paginated notifications for current user |
| GET | `/summary` | Notification summary |
| GET | `/unread-count` | Count of unread notifications |
| PATCH | `/{id}/read` | Mark single notification as read |
| POST | `/mark-all-read` | Mark all notifications as read |
| DELETE | `/{id}` | Delete single notification |
| DELETE | `/read` | Delete all read notifications |

### EmailTemplatesController (`/api/email-templates`)
| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/` | All email templates (requires EmailTemplates.View) |
| GET | `/{id}` | Single template by ID |
| GET | `/logs` | Paginated email delivery logs |
| PUT | `/{id}` | Update template (requires EmailTemplates.Edit) |

### MailServerConfigController (`/api/mail-server-config`)
| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/` | Active mail server configuration (password masked, requires `MailServerConfig.View`) |
| PUT | `/` | Upsert the active configuration (requires `MailServerConfig.Edit`) — null/empty password keeps the stored value |

The password is encrypted via `ISecretProtector` (ASP.NET Core Data Protection)
before being persisted in the `MailServerConfigs` table. `EmailSender` reads the
active configuration through `IMailServerConfigService.GetActiveSmtpSettingsAsync()`
at send time and falls back to the bound `Smtp` section of `appsettings.json` when
no row exists.

### DataSyncController (`/api/data-sync`)
| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/jobs` | All sync jobs with schedule, next run (UTC), running flag and last-run summary (requires `DataSync.View`) |
| GET | `/jobs/{id}` | Single job |
| GET | `/jobs/{id}/runs?take=25` | Run history, newest first (max 200) |
| PUT | `/jobs/{id}` | Update cron / time zone / enabled; validates the cron (Cronos) and re-registers the Hangfire recurring job (requires `DataSync.Edit`) |
| POST | `/jobs/{id}/run` | Enqueue an immediate run; `409` if a run is already in progress (requires `DataSync.Edit`) |
| POST | `/jobs/{id}/probe` | Read-only connectivity + schema check of source and target (requires `DataSync.Edit`) |

**Background scheduler:** Hangfire 1.8 with SQL Server storage (`HangFire` schema in
the application database, created automatically), hosted in the API process with
2 workers. The dashboard at `/hangfire` is restricted to local requests because the
API is JWT-only. Each job is an `IDataSyncJobHandler` registered in DI; at startup
`IDataSyncService.EnsureJobsRegisteredAsync()` seeds a `DataSyncJobs` row per
handler and pushes every enabled row's cron to Hangfire. Hangfire invokes
`DataSyncHangfireJob.Run` (`[DisableConcurrentExecution]`, no automatic retries),
which delegates to `IDataSyncJobRunner` to record a `DataSyncJobRuns` row and
execute the handler. In the `Testing` environment Hangfire is not registered and a
`NoOpDataSyncScheduler` is used instead.

**Employee-info handler** (`employee-info-sync`): copies `ku_employee_info` from the
HR database (`DataSync:EmployeeInfo:SourceConnectionString`) into the app database.
Full refresh: discovers writable target columns and source columns via `sys.columns`,
selects only the intersection, loads the source into memory, then `DELETE` +
`SqlBulkCopy` inside one transaction so readers never see an empty table. Aborts
(target untouched) when the source is empty or below `MinRowRatio` (default 0.9) of
the current target row count, or when the post-copy count differs.

### CalendarsController (`/api/calendars`)
| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/?includeDeleted=false` | All calendars with their weekly pattern and exceptions, default first then by name (requires `Calendars.View`) |
| GET | `/{id}` | Single calendar (requires `Calendars.View`) |
| GET | `/{id}/usage` | Workspaces using the calendar. Names only the workspaces the caller may see (private-workspace filter + `IWorkspaceAccessResolver`); the rest are reported as `hiddenWorkspaceCount`. For the default calendar also returns `inheritingWorkspaceCount` — visible active workspaces with no calendar of their own (requires `Calendars.View`) |
| POST | `/` | Create a calendar. Body: name, description, timeZoneId, isActive, `days` (exactly 7), `exceptions` (requires `Calendars.Create`) |
| PUT | `/{id}` | Update a calendar, its 7 day rows and its **full** exception list — rows with an `id` are updated, rows without are added, rows left out are removed (requires `Calendars.Edit`) |
| PUT | `/{id}/default` | Make this calendar the default; the previous default loses the flag in the same transaction (requires `Calendars.Edit`) |
| PATCH | `/{id}/restore` | Restore a soft-deleted calendar (requires `Calendars.Edit`) |
| DELETE | `/{id}` | Soft delete (requires `Calendars.Delete`) |

**Status codes:** `400` invalid input (FluentValidation or `ArgumentException`), `404` unknown
calendar, `409 Conflict` when a rule forbids the change — duplicate name, deleting or
deactivating the default calendar, deleting or deactivating a calendar a workspace uses,
making an inactive calendar the default, restoring a calendar whose name is now taken.

**Wire format:** `dayOfWeek` is a string enum (`sunday` … `saturday`), times are
`HH:mm:ss` (`TimeOnly`), dates are `yyyy-MM-dd` (`DateOnly`). Times are wall-clock in
the calendar's `timeZoneId`.

**Rules** (enforced in `WorkCalendarService`, with the database as a second line of
defence):
- Exactly one default calendar at all times — filtered unique index
  `IX_WorkCalendars_IsDefault`; `WorkCalendarSeeder` inserts "KU Standard" at startup
  when no calendar exists. `SetDefaultAsync` releases the old flag and claims the new one
  as two ordered saves inside one transaction (EF does not order a one-save swap reliably
  under the filtered index).
- The default calendar, and a calendar any workspace points at, cannot be deleted or
  deactivated. The in-use count ignores query filters on purpose so private and archived
  workspaces also protect their calendar; only the number leaves the service.
  `FK_Workspaces_WorkCalendars_WorkCalendarId` is `Restrict`.
- A working day needs `StartTime < EndTime`; the week needs all 7 days and at least one
  working day; an exception needs `EndDate ≥ StartDate`; `TimeZoneId` must resolve via
  `TimeZoneInfo` (IANA or Windows id).
- Name is unique among non-deleted calendars (case-insensitive; filtered unique index
  `IX_WorkCalendars_Name`).

The admin UI for this API is the Calendars page (`/admin/calendars`, WO-2026-11 C-2 —
frontend only, no API change). Its month preview repeats the `ResolvedWorkCalendar` rules in
`frontend/.../shared/utils/work-calendar.util.ts`; change both together. The workspace
setting (`/api/workspaces/{id}/calendar`) and the working-day rule are WO-2026-11 C-3 (below).

**Working-day rule (WO-2026-11 C-3, Q7 / Q8).** `IWorkCalendarService.EnsureWorkingDaysAsync(workspaceId, checks)`
takes `WorkingDateCheck(field, value, current)` entries and throws `NonWorkingDateException`
(an `ArgumentException`, so 400) listing every date that is created or changed and is not a
working day in the workspace's calendar. Dates are compared by calendar day
(`DateOnly.FromDateTime`), so a date the record already holds is never checked — legacy
records on weekends keep saving. Message, one sentence per refused date:
`Planned end 2026-11-28 (Saturday) is not a working day in the workspace calendar "KU Standard". Next working day: 2026-11-30 (Monday).`
(field names for action items since A-1: "Planned start" / "Planned end", and "Requested planned start" / "Requested planned end" on a date-change request)
Called after the existing guards (Draft / freeze / ordering) and before any change, from:

| Caller | Dates checked | Calendar of |
|--------|---------------|-------------|
| `ActionItemService.CreateAsync` | Start, Due | `dto.WorkspaceId` |
| `ActionItemService.UpdateAsync` | changed Start, Due — before the standalone branch, which turns a date edit into a request and only logs a failure | standalone item: its current workspace; otherwise `dto.WorkspaceId ?? item.WorkspaceId` |
| `ActionItemWorkflowService.CreateDateChangeRequestAsync` | requested Start / Due that differ from the item's | the item's workspace |
| `MilestoneService.CreateAsync` / `UpdateAsync` | planned Start, planned Due (changed only on update) | the project's workspace |
| `ProjectService.CreateAsync` / `UpdateAsync` | planned Start, planned End (changed only on update) | `dto.WorkspaceId` / the project's workspace |

Not checked: `ReviewRequestAsync` (approval applies the stored request), the unused
`ApplyApprovedDateChangeAsync`, actual dates, and unchanged dates of an item moved to
another workspace. `DashboardRules` (overdue and due windows) is unchanged. If no calendar
is configured at all the rule logs a warning and lets the save through. `ActionItemsController`
(POST / PUT) and `ActionItemWorkflowController` (`date-change-request`) catch the exception and
return `ApiResponse.Fail(message)`; `MilestonesController` and `ProjectsController` already
return `ArgumentException` messages that way.

**Actual dates (WO-2026-11 A-1, Q9).** `ActionItem.ActualStartDate` / `ActualEndDate`, maintained by
`ActionItemActualDates` on every path that changes an action item's status:

| Path | What it does with actual dates |
|------|-------------------------------|
| `ActionItemService.CreateAsync` | Created as In Progress → actual start = today; created as Done → actual end = today |
| `ActionItemService.UpdateAsync` (PUT) | Applies a status that is not sent for approval, plus any `actualStartDate` / `clearActualStartDate` / `actualEndDate` in the payload; worked out and validated before the standalone branch, so a refused value creates no request |
| `ActionItemService.UpdateStatusAsync` (PATCH `/status`) | The asked-for status counts for the actual start (In Progress stored as Overdue on a past-due item still records it); the only path that can take an item out of Done, which clears the actual end |
| `ActionItemWorkflowService.ReviewRequestAsync` | An approved status change applies the rule (approved Done → actual end = the approval day) |
| `ActionItemService.ApplyApprovedStatusChangeAsync` | Same rule (no callers today) |
| `ActionItemService.ProcessOverdueItemsAsync` | Never moves into In Progress or Done — actual dates untouched |

Rules: actual start is set on a move into In Progress when empty and never cleared automatically;
actual end is set on the move into Done (a value sent in the same PUT wins) and cleared when the
item leaves Done; To Do → Done leaves actual start empty. "Today" is the calendar day in the
workspace calendar's time zone (`TodayForWorkspaceAsync`), stored at midnight like the planned dates.
Validation (`ArgumentException` → 400): neither date after today; actual end not before actual start;
actual end only when the item is or becomes Done in that save; actual start cannot be set and cleared
together. Actual end cannot be cleared while Done (there is no clear flag). Automatic values never
block a status change (an automatic end before the start is moved up to the start).
Actual dates are outside the planned-date freeze, the date-change requests and the working-day rule.
A Done item refuses every PUT (422) except one whose only fields are actual dates
(`ActionItemUpdateDto.ChangesOnlyActualDates()` and `HasActualDates()`). The response carries
`actualEndVarianceDays` = `ActualEndDate.Date − DueDate.Date` in calendar days (null without an actual end).
`DashboardRules`, `GetMyStatsAsync` (on-time still compares `UpdatedAt`) and the approval workflows are unchanged.

Migration `Add_ActionItem_ActualDates`: two nullable `datetime2` columns plus
`UPDATE ActionItems SET ActualEndDate = CAST(CAST(CASE WHEN UpdatedAt >= CreatedAt THEN UpdatedAt ELSE CreatedAt END AS date) AS datetime2) WHERE Status = 4`
— `UpdatedAt` is 0001-01-01 on items created as Done and never modified, so those use `CreatedAt`;
soft-deleted Done rows are included. `Down()` drops the two columns.

**Standalone flag (WO-2026-11 A-1b).** `ActionItem.IsStandalone` follows the project link, through
`ActionItemStandalone` (`Application/Features/ActionItems`). The `isStandalone` field of the create and
update DTOs is ignored (kept so older clients keep working):

| Path | Flag |
|------|------|
| `ActionItemService.CreateAsync` | `ForProject(dto.ProjectId)` — true exactly when no project is given (null or `Guid.Empty`) |
| `ActionItemService.UpdateAsync` | `AfterUpdate(stored, previousProjectId, newProjectId)` — re-derived only when the save changes `ProjectId` (linking → false, `projectId: Guid.Empty` → true); otherwise the stored flag is kept, so an edit never repairs an existing row on the side |

Before A-1b `UpdateAsync` copied `dto.IsStandalone`, and three edit screens sent `true` on every save,
which turned a project-linked item into a standalone one (proved by `ActionItemStandaloneFlagTests`
against the earlier code). The approval branch of the same save still reads the stored flag, so the
workflows, the date freeze and `DashboardRules` are unchanged. `ChangesOnlyActualDates()` no longer
counts `IsStandalone`, since the server ignores it. No migration; production had 0 items with a
`ProjectId` and `IsStandalone = 1` when this was written (read-only check, 2026-11-24).

### UsersController (`/api/users`)
| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/roles` | All role names |
| GET | `/` | All users (paginated, searchable, sortable) |
| GET | `/{id}` | Single user |
| POST | `/register-external` | Register local user with username/password |
| POST | `/register-ad` | Register Azure AD user |
| GET | `/search-employees` | Search KU employee directory |
| PUT | `/update-role` | Replace user roles |
| PUT | `/{id}/deactivate` | Deactivate user account |
| PUT | `/{id}/reactivate` | Reactivate user |
| PUT | `/{id}/assign-org-unit` | Assign/unassign org unit to user |
| GET | `/me/org-units` | Org units visible to current user |

### RoleManagementController (`/api/roles`)
| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/` | All roles with user counts |
| GET | `/{roleName}` | Single role |
| GET | `/{roleName}/users` | Users in role (paginated) |
| GET | `/{roleName}/permissions` | Permission matrix for role |
| POST | `/` | Create new role |
| DELETE | `/{roleName}` | Delete role (fails if users assigned) |
| POST | `/{roleName}/permissions` | Assign permissions (full replace) |
| POST | `/{roleName}/users/assign` | Assign users to role |
| POST | `/{roleName}/users/remove` | Remove users from role |

### OrgUnitsController (`/api/orgunits`)
| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/tree` | Full org chart tree |
| GET | `/` | Paged flat list |
| GET | `/{id}` | Single org unit |
| GET | `/{id}/children` | Direct children |
| POST | `/` | Create org unit |
| PUT | `/{id}` | Update org unit |
| DELETE | `/{id}` | Soft delete (cascades to descendants) |
| POST | `/{id}/restore` | Restore deleted org unit |
| GET | `/{id}/head` | Returns the org unit head (CR-001-H) or null |
| PUT | `/{id}/head` | Assign or replace the org unit head — body `{ userId }`. Permission: `OrgChart.AssignHead` |
| DELETE | `/{id}/head` | Clear the org unit head |
| GET | `/{id}/plan-approver` | Resolves the level-3 ancestor's head (`IApprovalAuthorityResolver`) — used by the frontend to preview who will receive the approval request |
| GET | `/{id}/users` | List users currently assigned to this org unit (id, full name, email, roles, isActive). Permission: `OrgChart.View` |



### PermissionCatalogController (`/api/permission-catalog`)
| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/areas` | All permission areas |
| GET | `/areas/{id}` | Single area |
| POST | `/areas` | Create area |
| PUT | `/areas/{id}` | Update area |
| DELETE | `/areas/{id}` | Soft delete area |
| GET | `/actions` | All permission actions |
| GET | `/actions/{id}` | Single action |
| POST | `/actions` | Create action |
| PUT | `/actions/{id}` | Update action |
| DELETE | `/actions/{id}` | Soft delete action |
| GET | `/mappings` | All area-action mappings |
| GET | `/mappings/by-area/{areaId}` | Mappings for area |
| POST | `/mappings` | Create mapping |
| DELETE | `/mappings/{id}` | Soft delete mapping |

### RolePermissionsController (`/api/role-permissions`)
| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/roles` | All role names |
| GET | `/{roleName}` | Permissions for role |
| GET | `/{id}/detail` | Single role permission |
| GET | `/matrix/{roleName}` | Permission matrix for role |
| POST | `/` | Create role permission |
| PUT | `/{id}` | Update role permission |
| DELETE | `/{id}` | Soft delete role permission |

### UserPermissionsController (`/api/user-permissions`)
| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/{userId}/overrides` | Permission overrides for user |
| GET | `/overrides/{id}` | Single override |
| POST | `/overrides` | Create user permission override |
| PUT | `/overrides/{id}` | Update override |
| DELETE | `/overrides/{id}` | Delete override |
| GET | `/{userId}/effective` | Merged effective permissions for user |
| GET | `/me/effective` | Effective permissions for current user |

### StrategicObjectivesController (`/api/strategicobjectives`)
All endpoints scoped through `IStrategicScopeService` — Strategy Editors see only their org-unit subtree. PMO Head, PMO Analyst, Project Manager, and Workspace Admin are explicitly read-unrestricted across org units (CR-001-I); writes still gated by their permission set.
| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/` | Paged list (filterable by org unit, scope-filtered) |
| GET | `/{id}` | Single objective (returns 404 when out of scope). Response is enriched with parent strategic-plan context (`strategicPlanId`, `strategicPlanName`, `strategicPlanStatus`, `planStartYear`, `planEndYear`, `isPlanLocked`) so the UI can gate edits and constrain the year picker without a second round-trip; the list endpoints leave these fields null to avoid an N+1 lock-guard probe per row |
| GET | `/by-orgunit/{orgUnitId}` | Objectives for org unit (empty list when out of scope) |
| POST | `/` | Create (auto-generates ObjectiveCode `SO-n` from the `dbo.ObjectiveCodeSeq` SQL sequence — atomic, collision-proof; write scope enforced) |
| PUT | `/{id}` | Update (scope enforced on both source and target org unit) |
| DELETE | `/{id}` | Soft delete (write scope enforced). Returns **409 Conflict** with the list of project codes when one or more non-deleted projects still link to the objective (`Project.StrategicObjectiveId`) — caller must unlink/reassign those projects before retrying |
| POST | `/{id}/restore` | Restore (write scope enforced) |

### StrategicPlansController (`/api/strategic-plans`)
All endpoints scoped through `IStrategicScopeService`. Permission policies: `StrategicPlans.{View|Create|Edit|Delete|Approve}`. Create is restricted to **Admin** and **PMO Head** only (CR-001-C); Strategy Editor holds only View + Edit.
| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/?orgUnitId&status&year&page&pageSize&includeDeleted` | Paged list, scope-filtered |
| GET | `/{id}` | Single plan |
| GET | `/by-orgunit/{orgUnitId}` | All plans for an org unit |
| GET | `/by-orgunit/{orgUnitId}/active` | The single Approved or PendingApproval plan, or 404 |
| POST | `/` | Create — returns 409 if an active plan already exists for the org unit; on success fires in-app + `StrategicPlan.Initiated` email to every Strategy Editor whose `OrgUnitId` is in the plan's subtree |
| PUT | `/{id}` | Update name, start year, duration. `OrgUnitId` is now mutable on **Draft** plans only — pass it to re-assign the plan; the service rejects targets that already host another non-archived plan and verifies write scope on the destination. Linked `StrategicObjective.OrgUnitId` rows are cascaded to the new unit in the same `SaveChangesAsync` so plan + objective references stay aligned. Non-Draft statuses (PendingApproval / Approved / Archived) reject the change with a 400 |
| GET | `/{id}/approvers` | Returns the plan's configured ordered approval chain (`StrategicPlanApproverDto[]`). Same scope rules as the rest of the strategy reads. Empty list = legacy single-level fallback (CR-001-H level-3 head). |
| POST | `/{id}/drafts` | **CR-001-G**. Open a new revision draft against an Approved plan. Returns the draft plan id (`ApiResponse<Guid>`). Clones plan + objectives + KPIs + targets into new rows tagged `IsDraft=true` with `Original*Id` back-pointers. Returns 409 if a draft already exists, 404 if the plan doesn't exist, 403 if the caller lacks write scope. |
| GET | `/{id}/drafts/current` | **CR-001-G**. Returns the open draft's id if one exists, else 204. |
| DELETE | `/drafts/{draftId}` | **CR-001-G**. Hard-delete the draft graph. Auto-rejects any pending revision approval requests pointing at it. Only the draft's creator can cancel. 404 if missing, 403 if not the creator. |
| PUT | `/{id}/approvers` | Replace-the-chain upsert. Body: `{ approvers: [{ userId, order }, ...] }`. Server normalises gaps + duplicates, validates every user id maps to an active user, then wipes existing rows and re-inserts the new chain in one transaction. **Rejected** on any non-Draft status (chain freezes at submit time so in-flight requests' levels stay consistent). |
| DELETE | `/{id}` | Soft delete — returns 409 if any non-deleted objective references the plan |
| PATCH | `/{id}/restore` | Restore a soft-deleted plan |

### KpisController (`/api/kpis`)
All endpoints scoped through `IStrategicScopeService` — applied via the parent strategic objective's org unit.
| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/` | Paged KPIs (filterable by objective, scope-filtered) |
| GET | `/{id}` | Single KPI with targets (returns 404 when out of scope) |
| GET | `/by-objective/{objectiveId}` | KPIs for objective (empty when out of scope) |
| POST | `/` | Create KPI (auto-assigns KpiNumber per objective; write scope enforced) |
| PUT | `/{id}` | Update KPI (write scope enforced) |
| DELETE | `/{id}` | Soft delete (write scope enforced) |
| POST | `/{id}/restore` | Restore (write scope enforced) |
| POST | `/targets/upsert` | Upsert single target for month (write scope enforced) |
| POST | `/targets/bulk-upsert` | Upsert all monthly targets for KPI/year (write scope enforced) |
| GET | `/{id}/targets` | All targets for KPI/year (empty when out of scope) |

Controller error mapping for KPIs and Strategic Objectives: `UnauthorizedAccessException` → 403 Forbidden, `KeyNotFoundException` → 404, `ArgumentException` → 400 Bad Request.

### CommentsController (`/api/comments`)
| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/` | Comments by entity (entityType + entityId query params) |
| POST | `/` | Add comment to entity |
| PUT | `/{id}` | Update comment (authorization check) |
| DELETE | `/{id}` | Delete comment (authorization check) |

### DocumentsController (`/api/documents`)
| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/` | Documents by entity (entityType + entityId query params) |
| POST | `/` | Upload document (multipart, max 10 MB; whitelist: PDF/DOC/DOCX/XLS/XLSX/PPT/PPTX) |
| GET | `/{id}/download` | Download document |
| DELETE | `/{id}` | Delete document |

**Per-entity-type document caps:** `KpiTarget` is limited to 10 documents per record (`MaxDocumentsPerEntity` lookup in `DocumentService`).
**Scope enforcement:** uploads/deletes for `entityType=KpiTarget` traverse `KpiTarget → Kpi → StrategicObjective → OrgUnit` and call `IStrategicScopeService.EnsureCanWriteAsync` — Strategy Editors cannot manipulate evidence outside their scope.

### ProfileController (`/api/profile`)
| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/me` | Current user's employee profile from KU directory |

### ActionItemSubItemsController (`/api/action-items/{actionItemId}/sub-items`) — WO-2026-11 A-2
| Method | Endpoint | Policy | Description |
|--------|----------|--------|-------------|
| GET | `/` | `ActionItems.View` | `{ actionItemId, parentStartDate, parentDueDate, parentAcceptsChanges, totalCount, doneCount, items[] }`, items by `sortOrder`. Each item: title, description, start / end, sortOrder, isDone, doneAt, doneBy*, assignees, `isOutsideParentDates` |
| POST | `/` | `ActionItems.Edit` | Add at the end. Body `{ title, description?, startDate?, endDate, assigneeIds[] }` (dates `yyyy-MM-dd`). 201 with the sub-item |
| PUT | `/{subItemId}` | `ActionItems.Edit` | Replace title, description, dates and the whole assignee list |
| DELETE | `/{subItemId}` | `ActionItems.Edit` | Soft delete; the remaining order is re-densified. It stays deleted when the parent is later deleted and restored |
| PATCH | `/{subItemId}/done` | signed in, then `ActionItems.Edit` **or** assigned to that sub-item (else 403) | Body `{ isDone }`. Sets / clears `DoneAt` + `DoneByUserId`. The parent's `Progress` is not recalculated (Q10) |
| PUT | `/reorder` | `ActionItems.Edit` | Body `{ subItemIds[] }` — every live sub-item once, in the new order |

**Status codes:** 404 when the caller cannot open the parent (deleted, or a private workspace the caller
is not part of) or the sub-item is not the parent's; 409 (`SubItemParentLockedException`) for any
write while the parent is Done or Cancelled; 400 with the reason in `message` for a rule failure
(controller-level catch, so the text survives the Production exception middleware); FluentValidation
400 for field rules (title required ≤ 255, description ≤ 5000, end required, assignee ids non-empty,
reorder ids unique). Visibility is the same as `GET /api/action-items/{id}` (query filters only; no
`IWorkspaceAccessResolver` check, because project managers are in the assignee pool but not in that resolver).

**Rules** (`ActionItemSubItemService`, pure parts in `ActionItemSubItemRules`; the client copy is
`shared/utils/sub-item-dates.util.ts` — change both together):
- One level only (a sub-item has no sub-items). `StartDate` optional, `EndDate` required, both stored as calendar days.
- Dates within the parent's planned dates: end ≥ start; neither date after the parent's planned end; neither before the
  parent's planned start when it has one. Checked on create, and on update only when a date changes (both dates then) —
  a sub-item left outside by a later change of the parent's dates still accepts other edits and ticking.
- Working-day rule (C-3): `EnsureWorkingDaysAsync(parent.WorkspaceId, …)` with fields "Sub-item start" / "Sub-item end";
  only created or changed dates.
- Assignees: users **being added** must be in `IWorkspaceService.GetAssignableUsersAsync(parent.WorkspaceId)` (active
  admins, members and PMs — the parent's picker pool); someone already assigned who left that pool may stay. An empty list is allowed.
- No add / change / tick / reorder / delete while the parent is Done or Cancelled (409). Sub-items are not frozen by the
  project date freeze and never go through the approval workflows; a parent date change is never blocked by its sub-items.
- Notifications (fire-and-forget in a new scope, never failing the save) through `IWorkflowNotificationHelper`:
  `NotifySubItemAssignedAsync` to users newly added, `NotifySubItemCompletedAsync` when ticked done (not when unticked)
  to the parent's creator, the parent's assignees and the sub-item's other assignees. The actor is never notified;
  inactive users are skipped. In-app type `ActionItem`, action types `SubItemAssigned` / `SubItemCompleted`, link
  `/actions/{parentId}/view`; e-mail templates `ActionItem.SubItemAssigned` / `ActionItem.SubItemCompleted`.

**Delete / restore with the parent** (`ActionItemSubItemCascade`, decision D2): `ActionItemService.DeleteAsync` and
`ProjectService.DeleteAsync` soft-delete the live sub-items of the deleted items with `DeletedWithParent = true`; the two
restore paths restore exactly those and clear the flag.

**Private workspaces:** `ActionItemSubItem` and `ActionItemSubItemAssignee` have query filters that repeat the
`ActionItem` rule through the parent (not deleted; parent not deleted; parent's workspace not private, or the user is one
of its admins / members). The cascade queries use `IgnoreQueryFilters()` and re-apply the rule with
`WhereWorkspaceVisibleTo` (new `IQueryable<ActionItemSubItem>` overload). The notification helper reads one sub-item by id
with `IgnoreQueryFilters()` because it runs after the request.

### ActionItemWorkflowController (`/api/action-items/workflow`)
| Method | Endpoint | Description |
|--------|----------|-------------|
| POST | `/date-change-request` | Create date change request (dates frozen on standalone items). Requested dates that differ from the item's current ones: Refused with 400 when a created or changed date is not a working day in the workspace calendar (WO-2026-11 C-3, see "Working-day rule" below). Checked here, at request time; approving (`PUT /requests/{requestId}/review`) applies the request as it stands |
| POST | `/status-change-request` | Create status change request (terminal transitions require approval) |
| PUT | `/requests/{requestId}/review` | Approve or reject a workflow request. Approving a status change also records the actual dates (approved Done → actual end = the approval day; WO-2026-11 A-1) |
| GET | `/pending-reviews` | Get pending requests for current user to review |
| GET | `/my-requests` | Get requests created by current user |
| GET | `/action-item/{actionItemId}` | Get all workflow requests for an action item |
| GET | `/pending-summary` | Get pending request counts for badge |
| POST | `/escalate` | Trigger escalation workflow with notifications |
| POST | `/give-direction` | Give direction on escalated item (high-importance comment) |
| GET | `/can-review/{actionItemId}` | Check if current user can review requests |

### ProjectWorkflowController (`/api/projects/workflow`)
| Method | Endpoint | Description |
|--------|----------|-------------|
| POST | `/submit` | Submit project for start-approval (Draft → PendingApproval) |
| PUT | `/requests/{requestId}/review` | Approve or reject a project approval request |
| GET | `/project/{projectId}` | Get all approval requests for a project |
| GET | `/pending-reviews` | Get pending approval requests for current user to review |
| GET | `/my-requests` | Get approval requests submitted by current user |
| GET | `/pending-summary` | Get pending project approval count for badge |
| GET | `/can-review/{projectId}` | Check if current user can review a project |

### StrategicPlanWorkflowController (`/api/strategic-plans/workflow`)
Mirrors the project workflow controller for strategic plans (CR-001-D + CR-001-E + CR-001-G). **Reviewer routing**: when the plan has a configured approval chain (`PUT /api/strategic-plans/{id}/approvers`), the workflow uses sequential multi-level approval — only the level-1 reviewer gets a Pending request; on each Approve a new Pending row is created for level N+1 and the previous row is marked Approved; the plan only flips to `Approved` on the **final-level** approval. Rejection at any level rolls the plan back to `Draft`. With **no chain configured**, falls back to the legacy single-level resolver (`IApprovalAuthorityResolver` → level-3 ancestor's head). **Submitter cannot be an approver**: `Submit` and `RequestRevision` reject (400) when the submitter appears anywhere in the chain (listing the offending levels) or when the level-3 fallback head is the submitter — the chain must be fixed before the request can be routed. The mid-flight advance (next-level approval) re-runs the same check as a safety net in case the chain is edited during an in-flight workflow. **CR-001-G (draft model)**: a Revision request now points at a *draft* plan row (created via `POST /api/strategic-plans/{id}/drafts`), not the published plan. The published plan is **permanently locked** once Approved — edits live exclusively on the draft. `RequestRevisionAsync` requires the caller to be the draft's `CreatedBy` and flips the draft's `Status` to `PendingApproval`. `ReviewAsync` distinguishes Initial (request → published plan) vs Revision (request → draft); on final-level approval of a Revision it calls `IStrategicPlanDraftService.PromoteDraftAsync` which merges the draft graph back into the published row (field-level update on each entity with `OriginalXxxId`, new rows promoted by re-parenting, removed rows soft-deleted on published — with the linked-project guard re-running on each soft-delete) and hard-deletes the draft graph. Rejection sends the draft back to `Status = Draft` so the submitter can edit + resubmit (chain resets to level 1 on next submit). Chain config lives on the published plan only — drafts inherit it.
| Method | Endpoint | Description |
|--------|----------|-------------|
| POST | `/submit` | Submit a Draft plan for initial approval. Validates: at least one objective + one KPI per objective; if no chain is configured, the level-3 head must be configured. Stamps `Level=1` and `TotalLevels=chain.Count` (or `1` for the fallback path) on the resulting request row |
| POST | `/request-revision` | Open a Revision request against an Approved plan (CR-001-E). Same multi-level routing as `/submit` — single in-flight revision at a time |
| PUT | `/requests/{requestId}/review` | Approve / reject an Initial or Revision request (reviewer or Admin). Comment required on rejection. Multi-level: a non-final-level Approve creates the next-level Pending row + sends a Submit-style notification to the next approver, leaving plan-level state alone |
| GET | `/plan/{planId}` | All approval requests for a plan (history) |
| GET | `/pending-reviews` | Pending requests where current user is the assigned reviewer (or Admin sees all) |
| GET | `/my-requests` | Requests submitted by the current user |
| GET | `/pending-summary` | Pending count for the header badge aggregator |
| GET | `/can-review/{planId}` | Whether the current user can review the plan's latest pending request |
| GET | `/lock-state/{planId}` | `{ planId, status, isLocked, pendingRevisionId }` — drives the lock banners on the Strategy page |

### KpiActualsController (`/api/kpis/actuals`)
CR-001-F. Period-aware CRUD for `KpiActual` plus evidence document operations. All endpoints are scope-filtered through `IStrategicScopeService` based on the parent KPI's org unit.
| Method | Endpoint | Permission | Description |
|--------|----------|------------|-------------|
| GET | `/by-kpi/{kpiId}?year=` | `KpiActuals.View` | All actuals for a KPI in the given year (history, all targets) |
| GET | `/grid?kpiTargetId=&year=` | `KpiActuals.View` | One row per period slot for a target (Q1..Q4 / H1..H2 / Annual) with `current` actual + period-end metadata |
| GET | `/history?kpiTargetId=&periodType=&periodIndex=&year=` | `KpiActuals.View` | Full history (including rejected) for a single (target, period, year) slot |
| GET | `/{id}` | `KpiActuals.View` | Single actual with target/KPI context |
| POST | `/` | `KpiActuals.Create` | Create a Draft. 409 if an active (non-rejected) actual exists for that slot |
| PUT | `/{id}` | `KpiActuals.Edit` | Update Draft (rejected once `Status != Draft`) |
| DELETE | `/{id}` | `KpiActuals.Edit` | Soft-delete Draft |
| GET | `/{id}/documents` | `KpiActuals.View` | List evidence (delegates to `DocumentService`) |
| POST | `/{id}/documents` | `KpiActuals.Edit` | Upload evidence; 10-doc cap (409 on overflow), scope-gated for Strategy Editors |
| DELETE | `/{id}/documents/{documentId}` | `KpiActuals.Edit` | Remove evidence |

### KpiActualsWorkflowController (`/api/kpis/actuals/workflow`)
CR-001-F. The actual itself is the workflow record — no separate request entity. Reviewer is resolved at submit time via `IApprovalAuthorityResolver` (level-3 head). Mirrors the strategic-plan workflow shape.
| Method | Endpoint | Description |
|--------|----------|-------------|
| POST | `/submit` | Submit a Draft. 400 if the period has not yet ended (`KpiPeriodCalendar`); 409 if no level-3 head configured |
| PUT | `/requests/{actualId}/review` | Approve / reject. Comment required on reject. Rejected is final (submitter must create a new draft) |
| GET | `/pending-reviews` | Pending actuals where current user is the assigned reviewer (or Admin sees all) |
| GET | `/my-requests` | Actuals the current user submitted or created |
| GET | `/pending-summary` | `{ pendingKpiActuals }` for the header badge aggregator |
| GET | `/can-review/{actualId}` | Whether the current user can review the actual |

### ReportsController (`/api/reports`)
| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/export-csv` | Export filtered action items to CSV |
| GET | `/summary` | KPI summary statistics |

---

## Database Entities (28 Entities)

### Core Entities

#### ActionItem
| Field | Type | Description |
|-------|------|-------------|
| Id | Guid (PK) | Primary key |
| ActionId | string | Human-readable code (ACT-001), auto-generated |
| Title | string | Action item title |
| Description | string | Detailed description |
| WorkspaceId | Guid (FK) | Parent workspace (required) |
| ProjectId | Guid? (FK) | Linked project (nullable) |
| MilestoneId | Guid? (FK) | Linked milestone (nullable) |
| IsStandalone | bool | True when not linked to a project. Set by the server from `ProjectId` (WO-2026-11 A-1b, `ActionItemStandalone`); never taken from the client |
| Priority | enum | Low, Medium, High, Critical |
| Status | enum | ToDo, InProgress, InReview, Done, Overdue, Deferred, Cancelled |
| StartDate | DateTime? | Planned start (shown as "Planned start") |
| DueDate | DateTime | Planned end (required; shown as "Planned end") |
| ActualStartDate | DateTime? | Actual start (WO-2026-11 A-1): set on the first move into In Progress, editable. Calendar day at midnight |
| ActualEndDate | DateTime? | Actual end (A-1): set on the move into Done, cleared when the item leaves Done, editable while Done. Calendar day at midnight. Backfilled for Done items by `Add_ActionItem_ActualDates` |
| Progress | int | 0-100 (auto-clamped) |
| IsEscalated | bool | Escalation flag |
| CreatedByUserId | string? | User who created the item |
| CreatedAt | DateTime | UTC creation timestamp |
| UpdatedAt | DateTime | UTC last update timestamp |
| IsDeleted | bool | Soft delete flag |
| **Nav:** | | Workspace, Project, Milestone, Assignees, Escalations, Comments, SubItems (A-2) |

#### ActionItemSubItem (`ActionItemSubItems`) — WO-2026-11 A-2
| Field | Type | Description |
|-------|------|-------------|
| Id | Guid (PK) | `NEWID()` |
| ActionItemId | Guid (FK → ActionItems, cascade) | Parent action item |
| Title | string(255) | Required |
| Description | string? (≤ 5000, `nvarchar(max)`) | Optional |
| StartDate | DateTime? | Optional calendar day |
| EndDate | DateTime | Required calendar day |
| SortOrder | int | 1-based, dense within the parent |
| IsDone | bool | Default false |
| DoneAt | DateTime? | UTC time it was ticked; cleared when unticked |
| DoneByUserId | string(450)? (FK → AspNetUsers, Restrict) | Who ticked it |
| CreatedAt / CreatedByUserId / UpdatedAt / UpdatedByUserId | audit | |
| IsDeleted | bool | Soft delete (query filter, incl. the private-workspace rule through the parent) |
| DeletedWithParent | bool | True when deleted because the parent (or its project) was deleted; restoring the parent restores exactly these |
| **Index:** | | `(ActionItemId, SortOrder)`, `DoneByUserId` |

#### ActionItemSubItemAssignee (`ActionItemSubItemAssignees`) — A-2
| Field | Type | Description |
|-------|------|-------------|
| SubItemId | Guid (PK, FK → ActionItemSubItems, cascade) | |
| UserId | string(450) (PK, FK → AspNetUsers, Restrict) | Indexed (My Actions query) |

Migration `Add_ActionItemSubItems` (2026-11-24): creates the two tables, their FKs and indexes; no existing column
touched, no backfill; `Down()` drops the two tables. Verified on throwaway LocalDB databases (empty, and one holding
production-like rows migrated from `Add_ActionItem_ActualDates`: schema, indexes, FKs, row counts and checksums of all 47
existing tables identical before and after; `Down()` back to the previous migration leaves them identical too).
**Production:** applied to production on 2026-11-24 at 14:22 (UTC+4) by the API's startup `MigrateAsync` when the API was restarted from Visual Studio; verified read-only afterwards — 26 migrations applied, 0 pending, both tables with their 5 indexes and 4 foreign keys, `ActionItems` row count and status mix unchanged, the 29 existing e-mail templates byte-identical (the seeder added the 2 new ones and overwrote nothing).

#### Project
| Field | Type | Description |
|-------|------|-------------|
| Id | Guid (PK) | Primary key |
| ProjectCode | string | Auto-generated (PRJ-YYYY-001) |
| Name | string | Project name |
| Description | string | Description |
| WorkspaceId | Guid (FK) | Parent workspace |
| ProjectType | enum | Operational, Strategic |
| ProjectStatus | enum | Draft, Active, OnHold, Completed, Cancelled, PendingApproval |
| StrategicObjectiveId | Guid? (FK) | Required when Strategic type |
| Priority | enum | Low, Medium, High, Critical |
| ProjectManagerUserId | string (FK) | Project manager |
| OwnerOrgUnitId | Guid? (FK) | Owning department/org unit |
| PlannedStartDate | DateTime | Planned start |
| PlannedEndDate | DateTime | Planned end |
| ActualStartDate | DateTime? | Set when status changes to Active |
| ApprovedBudget | decimal? | Budget amount (AED) |
| Currency | string | Always "AED" |
| IsBaselined | bool | Locks dates when true |
| IsDeleted | bool | Soft delete (cascades to milestones/items) |
| **Nav:** | | Workspace, StrategicObjective, ProjectManager, OwnerOrgUnit, Sponsors, Risks |

#### Milestone
| Field | Type | Description |
|-------|------|-------------|
| Id | Guid (PK) | Primary key |
| MilestoneCode | string | Auto-generated (MS-YYYY-001) |
| Name | string | Milestone name |
| ProjectId | Guid (FK) | Parent project |
| SequenceOrder | int | Display order |
| PlannedStartDate | DateTime | Planned start |
| PlannedDueDate | DateTime | Planned due date |
| ActualCompletionDate | DateTime? | Actual completion |
| IsDeadlineFixed | bool | Hard deadline flag |
| Status | enum | NotStarted, InProgress, AtRisk, Completed |
| CompletionPercentage | decimal | 0-100% |
| ApproverUserId | string? | Formal sign-off approver |
| BaselinePlannedStartDate | DateTime? | Locked baseline start |
| BaselinePlannedDueDate | DateTime? | Locked baseline due |
| IsDeleted | bool | Soft delete flag |
| **Nav:** | | Project, Approver |

#### ProjectRisk
| Field | Type | Description |
|-------|------|-------------|
| Id | Guid (PK) | Primary key |
| RiskCode | string | Auto-generated (RISK-001 per project) |
| ProjectId | Guid (FK) | Parent project |
| Title | string | Risk title |
| Description | string | Risk description |
| Category | string | Risk category |
| ProbabilityScore | int | 1-5 scale |
| ImpactScore | int | 1-5 scale |
| RiskScore | int | Computed: Probability x Impact (1-25) |
| RiskRating | enum | Critical (20-25), High (12-19), Medium (5-11), Low (1-4) |
| Status | enum | Open, Mitigated, Closed |
| MitigationPlan | string | Mitigation strategy |
| ContingencyPlan | string | Contingency strategy |
| Notes | string | Additional notes |
| RiskOwnerUserId | string | Risk owner |
| IdentifiedDate | DateTime | When risk was identified |
| DueDate | DateTime? | Target resolution date |
| ClosedDate | DateTime? | When risk was closed |
| IsDeleted | bool | Soft delete |
| **Nav:** | | Project |

#### Workspace
| Field | Type | Description |
|-------|------|-------------|
| Id | Guid (PK) | Primary key (NEWID()) |
| Title | string(200) | Workspace title |
| OrganizationUnit | string(200) | Organization unit name |
| OrgUnitId | Guid? (FK) | Org unit for access scoping |
| StrategicPlanId | Guid? (FK) | Optional linked strategic plan — drives the strategic objectives available to projects in this workspace |
| IsActive | bool | Active flag (default true) |
| IsPrivate | bool | Private flag (default false). When true, only the workspace's admins + members can see the workspace and its projects / milestones / action items / risks — enforced by global query filters (see `ICurrentUserService`). Create/update DTOs carry `IsPrivate`; on update `null` keeps the current value |
| WorkCalendarId | Guid? (FK → WorkCalendars, Restrict) | Working calendar for this workspace's date calculations. `null` = use the default calendar. Column added by `Add_WorkCalendars`; set through `PUT /api/workspaces/{id}/calendar` (WO-2026-11 C-3) |
| **Nav:** | | Admins (WorkspaceAdmin), Members (WorkspaceMember), ProjectManagers (WorkspaceProjectManager), StrategicProjectApprovers (WorkspaceStrategicProjectApprover), Projects, ActionItems |

#### WorkspaceMember (join table for direct user assignment)
| Field | Type | Description |
|-------|------|-------------|
| Id | int (PK) | Identity |
| WorkspaceId | Guid (FK) | Parent workspace (cascade delete) |
| UserId | string(450) | AspNetUsers.Id of the member |
| UserName | string(256) | Denormalized display name |
| AddedAt | DateTime | When the user was added (UTC) |
| AddedByUserId | string(450)? | The actor who added the member |
| **Unique index:** | | (WorkspaceId, UserId) — a user can be a member at most once per workspace |

#### WorkspaceProjectManager (workspace-scoped whitelist of eligible PMs)
| Field | Type | Description |
|-------|------|-------------|
| Id | int (PK) | Identity |
| WorkspaceId | Guid (FK) | Parent workspace (cascade delete) |
| UserId | string(450) | AspNetUsers.Id of the eligible PM |
| UserName | string(256) | Denormalized display name |
| AddedAt | DateTime | When the user was added (UTC) |
| AddedByUserId | string(450)? | The actor who added the PM |
| **Unique index:** | | (WorkspaceId, UserId) |

#### WorkspaceStrategicProjectApprover (sequential approval chain for strategic projects)
| Field | Type | Description |
|-------|------|-------------|
| Id | int (PK) | Identity |
| WorkspaceId | Guid (FK) | Parent workspace (cascade delete) |
| UserId | string(450) | AspNetUsers.Id of the approver |
| UserName | string(256) | Denormalized display name |
| Order | int | 1-based position in the chain (dense — gaps are re-densified on remove) |
| AddedAt | DateTime | When the approver was added (UTC) |
| AddedByUserId | string(450)? | The actor who added the approver |
| **Unique index:** | | (WorkspaceId, UserId) |
| **Index:** | | (WorkspaceId, Order) |

#### OrgUnit
| Field | Type | Description |
|-------|------|-------------|
| Id | Guid (PK) | Primary key |
| Name | string | Org unit name |
| Code | string? | Optional code |
| Level | int | Hierarchy depth |
| ParentId | Guid? (FK) | Parent org unit (tree structure) |
| HeadUserId | string? (FK → AspNetUsers, OnDelete: SetNull) | Designated head user (CR-001-H). Heads at Level 3 act as the strategic-plan approver for their subtree. |
| IsDeleted | bool | Soft delete (cascades to descendants) |
| **Nav:** | | Parent, Children, StrategicObjectives, StrategicPlans, HeadUser |

#### StrategicPlan
| Field | Type | Description |
|-------|------|-------------|
| Id | Guid (PK) | Primary key |
| OrgUnitId | Guid (FK) | Scoped to org unit |
| Name | string (≤ 200) | Plan name |
| StartYear | int (2000–2100) | Cycle start year |
| DurationYears | int (1–10) | Cycle duration |
| EndYear | int (computed) | `StartYear + DurationYears - 1` |
| Status | enum (StrategicPlanStatus) | Draft / PendingApproval / Approved / Archived |
| ApprovedAtUtc | DateTime? | Set when transitioned to Approved |
| ApprovedByUserId | string? (FK) | Approving user |
| IsDeleted | bool | Soft delete (delete blocked while non-deleted objectives exist) |
| **Index:** | | Filtered unique on `OrgUnitId` where `IsDeleted=0 AND Status IN (1,2)` — at most one active plan per org unit |
| **Nav:** | | OrgUnit, Objectives (StrategicObjective) |

#### StrategicPlanApprovalRequest (CR-001-D + CR-001-E)
| Field | Type | Description |
|-------|------|-------------|
| Id | Guid (PK) | Primary key |
| StrategicPlanId | Guid (FK, cascade) | Owning plan |
| RequestType | enum (StrategicPlanApprovalRequestType) | Initial / Revision |
| Status | enum (StrategicPlanApprovalStatus) | Pending / Approved / Rejected |
| RequestedByUserId / RequestedByDisplayName | string | Submitter |
| AssignedReviewerUserId / AssignedReviewerDisplayName | string? | Reviewer expected to action — resolved at submit via `IApprovalAuthorityResolver` |
| ReviewedByUserId / ReviewedByDisplayName | string? | Actual reviewer (captured on review) |
| Justification | string (≤ 2000) | Required reason from submitter |
| ReviewerComment | string? (≤ 2000) | Required on rejection |
| CreatedAt / ReviewedAt | DateTime / DateTime? | Audit timestamps |
| IsDeleted | bool | Soft delete |
| **Indexes:** | | `(StrategicPlanId, Status)` filtered on `IsDeleted=0`; `(AssignedReviewerUserId, Status)`; `RequestedByUserId` |

#### StrategicObjective
| Field | Type | Description |
|-------|------|-------------|
| Id | Guid (PK) | Primary key |
| ObjectiveCode | string | Auto-generated `SO-n` from `dbo.ObjectiveCodeSeq` (SQL SEQUENCE, seeded from highest existing code by the `Add_ObjectiveCodeSequence` migration) |
| Statement | string | Objective statement |
| OrgUnitId | Guid (FK) | Scoped to org unit |
| StrategicPlanId | Guid? (FK) | Optional FK to parent plan (nullable during the migration window — backfilled to a "Legacy Strategy" plan) |
| IsDeleted | bool | Soft delete |
| **Nav:** | | OrgUnit, StrategicPlan, Kpis |

#### Kpi
| Field | Type | Description |
|-------|------|-------------|
| Id | Guid (PK) | Primary key |
| KpiNumber | int | Auto-assigned per objective |
| Name | string | KPI name |
| CalculationMethod | string | How the KPI is measured |
| Period | enum | Monthly, Quarterly, SemiAnnual, Yearly |
| Unit | string? | Measurement unit |
| StrategicObjectiveId | Guid (FK) | Parent objective |
| IsDeleted | bool | Soft delete |
| **Nav:** | | StrategicObjective, Targets (KpiTarget) |

#### KpiTarget
| Field | Type | Description |
|-------|------|-------------|
| Id | Guid (PK) | Primary key |
| KpiId | Guid (FK) | Parent KPI |
| Year | int | Target year |
| Month | int | Target month (1-12) |
| Target | decimal? | Target value for this month |
| Actual | decimal? | Actual value recorded |
| Notes | string? (≤ 500) | Free-form notes |
| Baseline | decimal? | Reference value used to compute progress (CR-001-G) |
| DataSource | string? (≤ 500) | Source of the metric (CR-001-G) |
| CalculationNotes | string? (≤ 2000) | Target-specific calculation notes (CR-001-G) |
| Direction | enum (KpiDirection) | HigherIsBetter (default) / LowerIsBetter / OnTarget (CR-001-G) |

#### KpiActual (CR-001-F)
Period-aware measured value. Acts as its own workflow record (Status / Submitted* / Reviewed* fields live on the entity).
| Field | Type | Description |
|-------|------|-------------|
| Id | Guid (PK) | Primary key |
| KpiTargetId | Guid (FK, cascade) | Parent target |
| PeriodType | enum (KpiActualPeriodType) | Quarter / SemiAnnual / Annual — must match parent KPI's `MeasurementPeriod` |
| PeriodIndex | int | Q1=1..Q4=4 / H1=1, H2=2 / Annual=1 (CHECK constraint enforces ranges) |
| Year | int | Reporting year |
| ActualValue | decimal(18,4) | Measured value |
| Notes | string? (≤ 2000) | Optional notes |
| Status | enum (KpiActualStatus) | Draft / PendingApproval / Approved / Rejected |
| SubmittedByUserId / DisplayName | string? | Set on submit |
| SubmittedAtUtc | DateTime? | Set on submit |
| AssignedReviewerUserId / DisplayName | string? | Resolved at submit time via `IApprovalAuthorityResolver` |
| ReviewedByUserId / DisplayName | string? | Set on approve/reject |
| ReviewedAtUtc | DateTime? | Set on approve/reject |
| ReviewerComment | string? (≤ 2000) | Required on reject |
| IsDeleted | bool | Soft delete flag |
| CreatedAtUtc / ModifiedAtUtc | DateTime / DateTime? | Audit timestamps |
| **Indexes:** | | `(KpiTargetId, Year)`; `(AssignedReviewerUserId, Status)`; filtered unique `(KpiTargetId, PeriodType, PeriodIndex, Year)` where `IsDeleted=0 AND Status<>Rejected` |
| **Nav:** | | KpiTarget |

#### ApplicationUser (extends IdentityUser)
| Field | Type | Description |
|-------|------|-------------|
| FirstName | string | First name |
| LastName | string | Last name |
| FullName | computed | FirstName + LastName |
| DisplayName | string? | Preferred display name |
| Role | string | Denormalized role |
| Department | string | Department |
| AzureObjectId | string? | Azure AD oid claim |
| LoginProvider | string | "Local" or "AzureAD" |
| IsActive | bool | Controls login ability |
| OrgUnitId | Guid? (FK) | Assigned org unit |
| LastLoginAt | DateTime? | Last login timestamp |

### Supporting Entities

#### AppNotification
| Field | Type | Description |
|-------|------|-------------|
| Id | Guid (PK) | Primary key |
| UserId | string | Target user |
| Title | string | Notification title |
| Message | string | Notification body |
| Type | string | Entity type (ActionItem, Project, Milestone, etc.) |
| ActionType | string | Action that triggered notification |
| RelatedEntityType | string | Entity type discriminator |
| RelatedEntityId | Guid? | Related entity ID |
| RelatedEntityCode | string? | Related entity code |
| Url | string? | Optional deep link |
| IsRead | bool | Read status |
| ReadAt | DateTime? | When read |
| CreatedAt | DateTime | Created timestamp |

#### EmailTemplate
| Field | Type | Description |
|-------|------|-------------|
| Id | Guid (PK) | Primary key |
| TemplateKey | string | Lookup key |
| Name | string | Template name |
| Subject | string | Email subject line |
| HtmlBody | string | Email HTML body |
| Description | string? | Optional description |
| IsActive | bool | Active toggle |
| IsDeleted | bool | Soft delete |

#### EmailLog
| Field | Type | Description |
|-------|------|-------------|
| Id | Guid (PK) | Primary key |
| Tracks email delivery attempts for auditing |

#### MailServerConfig
| Field | Type | Description |
|-------|------|-------------|
| Id | Guid (PK) | Primary key |
| Host | string(256) | SMTP host |
| Port | int | SMTP port (default 587) |
| UseSsl | bool | Use SSL/TLS |
| FromEmail | string(256) | From email address |
| FromName | string(256) | From display name |
| Username | string(256) | SMTP auth username (nullable) |
| EncryptedPassword | string(4000) | DataProtection-encrypted password |
| IsActive | bool | Only one row with IsActive=true (filtered unique index) |
| CreatedAt / CreatedBy / UpdatedAt / UpdatedBy | audit | Standard audit columns |

#### DataSyncJob (`DataSyncJobs`)
| Field | Type | Description |
|-------|------|-------------|
| Id | Guid (PK) | Primary key |
| JobKey | string(100) | Unique machine key (e.g. `employee-info-sync`); matches the DI handler and the Hangfire recurring-job id |
| Name / Description | string(200) / string(1000) | Display text seeded from the handler |
| CronExpression | string(100) | 5-field cron (6 with seconds) |
| TimeZoneId | string(100)? | Windows/IANA id used to evaluate the cron; null = UTC |
| IsEnabled | bool | When false the recurring job is removed from Hangfire (manual runs still allowed) |
| LastRunAt / LastRunStatus / LastRunMessage / LastRunRowCount / LastRunDurationMs | denormalised | Last-run summary for the list view |
| CreatedAt / UpdatedAt / UpdatedBy | audit | |

#### DataSyncJobRun (`DataSyncJobRuns`)
| Field | Type | Description |
|-------|------|-------------|
| Id | Guid (PK) | Primary key |
| JobId | Guid (FK → DataSyncJobs, cascade) | |
| StartedAt / FinishedAt | DateTime | UTC |
| Status | `DataSyncRunStatus` | Running / Succeeded / Failed (Running rows older than 30 min are marked Failed "Interrupted" on the next run) |
| Trigger | `DataSyncTrigger` | Scheduled / Manual |
| TriggeredBy | string(256)? | User id for manual runs |
| RowsRead / RowsWritten | int? | |
| Message | string(4000)? | Success summary or error text |

#### WorkCalendar (`WorkCalendars`)
| Field | Type | Description |
|-------|------|-------------|
| Id | Guid (PK) | Primary key |
| Name | string(100) | Unique among non-deleted calendars (filtered unique index) |
| Description | string(500)? | |
| TimeZoneId | string(100) | IANA or Windows time-zone id; default `Asia/Dubai` |
| IsDefault | bool | Exactly one non-deleted row is the default (filtered unique index `[IsDefault] = 1 AND [IsDeleted] = 0`) |
| IsActive | bool | Inactive calendars cannot be the default |
| IsDeleted | bool | Soft delete (global query filter) |
| CreatedAt / CreatedBy / UpdatedAt / UpdatedBy | audit | Standard audit columns |
| **Nav:** | | Days (WorkCalendarDay, cascade), Exceptions (WorkCalendarException, cascade) |

#### WorkCalendarDay (`WorkCalendarDays`)
| Field | Type | Description |
|-------|------|-------------|
| Id | Guid (PK) | Primary key |
| WorkCalendarId | Guid (FK → WorkCalendars, cascade) | |
| DayOfWeek | int (`System.DayOfWeek`) | 0 = Sunday … 6 = Saturday; unique per calendar — always exactly 7 rows |
| IsWorkingDay | bool | |
| StartTime / EndTime | `time`? (`TimeOnly`) | Working hours; null on a non-working day |

#### WorkCalendarException (`WorkCalendarExceptions`)
| Field | Type | Description |
|-------|------|-------------|
| Id | Guid (PK) | Primary key |
| WorkCalendarId | Guid (FK → WorkCalendars, cascade) | |
| Name | string(150) | e.g. "National Day", "Ramadan hours" |
| StartDate / EndDate | `date` (`DateOnly`) | Inclusive range |
| IsWorkingDay | bool | `false` = holiday; `true` = special working day or reduced-hours period |
| StartTime / EndTime | `time`? (`TimeOnly`) | Optional hours for a working exception; null keeps the weekly pattern's hours |

Exception rows have no `IsDeleted` — removing one from a calendar deletes the row; the calendar itself is soft-deleted.

#### Document
| Field | Type | Description |
|-------|------|-------------|
| Id | Guid (PK) | Primary key |
| Name | string | Display name |
| FileName | string | Original filename |
| ContentType | string | MIME type |
| FileSize | long | Max 10 MB |
| Content | byte[] | Binary stored in DB |
| RelatedEntityType | string | Polymorphic: "ActionItem", "Project", "Kpi" |
| RelatedEntityId | Guid | Owning entity ID |
| UploadedByUserId | string (FK) | Uploader |

#### Comment (Generic)
| Field | Type | Description |
|-------|------|-------------|
| Id | Guid (PK) | Primary key |
| RelatedEntityType | string | Polymorphic entity type |
| RelatedEntityId | Guid | Owning entity ID |
| Content | string | Comment text |
| AuthorUserId | string | Author |
| IsHighImportance | bool | High-importance flag |

#### Other Entities
| Entity | Purpose |
|--------|---------|
| ActionItemAssignee | Junction table: ActionItem <-> User (multi-assignee) |
| ActionItemComment | Action-item-specific comments |
| ActionItemSubItem / ActionItemSubItemAssignee | Sub-items of an action item and their assignees (WO-2026-11 A-2) — see above |
| ActionItemEscalation | Escalation tracking for overdue items |
| ActionItemWorkflowRequest | Date change and status change approval requests (pending/approved/rejected) |
| ProjectApprovalRequest | Project start-approval requests — `Status` (Pending/Approved/Rejected) plus `AssignedReviewerUserId`/`Level`/`TotalLevels` for the workspace-chain sequential workflow. Strategic projects: one Pending row at level 1, advances on each Approve. Operational projects: one self-approved row stamped "Auto-approved: operational project (PM authority)" with `Level=TotalLevels=1`. |
| StrategicPlanApprovalRequest | Strategic plan approval requests — `RequestType` (Initial/Revision), `Status` (Pending/Approved/Rejected), `AssignedReviewerUserId`, `ReviewedByUserId`, `Justification`, `ReviewerComment`. Cascade delete from `StrategicPlan` (CR-001-D / CR-001-E) |
| ProjectSponsor | Sponsors linked to projects |
| WorkspaceAdmin | Admin users per workspace |
| WorkspaceMember | Regular (non-admin) members assigned to a workspace; drives `IWorkspaceAccessResolver` for read access to projects/action items |
| WorkspaceProjectManager | Workspace-scoped whitelist of users eligible to be picked as Project Manager for projects in that workspace. Project create/edit is rejected when the chosen PM isn't on this list. |
| WorkspaceStrategicProjectApprover | Sequential approval chain for strategic projects in a workspace. 1-based `Order`, dense (re-densified on remove). Mirrors `StrategicPlanApprover`. |
| RefreshToken | JWT refresh token storage with rotation |
| KuEmployeeInfo | KU employee directory integration |

### Permission Entities

#### RolePermission
| Field | Type | Description |
|-------|------|-------------|
| Id | Guid (PK) | Primary key |
| RoleName | string | ASP.NET Identity role name |
| AreaId | Guid | Permission area (denormalized) |
| AreaName | string | Denormalized for fast reads |
| ActionId | Guid | Permission action (denormalized) |
| ActionName | string | Denormalized for fast reads |
| IsActive | bool | Active flag |
| IsDeleted | bool | Soft delete |

#### UserPermissionOverride
| Field | Type | Description |
|-------|------|-------------|
| Id | Guid (PK) | Primary key |
| UserId | string | Application user |
| AreaId/AreaName | Guid/string | Permission area |
| ActionId/ActionName | Guid/string | Permission action |
| IsAllowed | bool | Grant (true) or Deny (false) |
| IsActive | bool | Active flag |
| IsDeleted | bool | Soft delete |

#### AppPermissionArea / AppPermissionAction / AreaPermissionMapping
- Catalog tables defining which areas and actions exist
- Mappings define valid area-action combinations
- Seeded on startup via `PermissionCatalogSeeder`

---

## Enums

| Enum | Values |
|------|--------|
| ActionPriority | Low, Medium, High, Critical |
| ActionStatus | ToDo, InProgress, InReview, Done, Overdue, Deferred, Cancelled |
| ProjectStatus | Draft, Active, OnHold, Completed, Cancelled, PendingApproval |
| ProjectType | Operational, Strategic |
| MilestoneStatus | NotStarted, InProgress, AtRisk, Completed |
| ProjectPhase | Initiation, Planning, Execution, MonitoringAndControlling, Closing |
| RiskStatus | Open, Mitigated, Closed |
| RiskRating | Critical (20-25), High (12-19), Medium (5-11), Low (1-4) |
| WorkflowRequestType | DateChangeRequest, StatusChangeRequest |
| WorkflowRequestStatus | Pending, Approved, Rejected |
| ProjectApprovalStatus | Pending, Approved, Rejected |
| StrategicPlanStatus | Draft, PendingApproval, Approved, Archived |
| StrategicPlanApprovalStatus | Pending, Approved, Rejected |
| StrategicPlanApprovalRequestType | Initial, Revision |
| KpiDirection | HigherIsBetter, LowerIsBetter, OnTarget |
| KpiActualStatus | Draft, PendingApproval, Approved, Rejected |
| KpiActualPeriodType | Quarter, SemiAnnual, Annual |

---

## Services (Application Layer)

| Service Interface | Responsibilities |
|-------------------|------------------|
| `IActionItemService` | CRUD, filtering, stats, comments, overdue processing, approved date/status change bypass. Applies the actual-date rule (`ActionItemActualDates`) on create, update, status change and the approved-status bypass; `RequiresStatusApproval` decides (for both PUT and PATCH `/status`) when a standalone item's move needs a request |
| `ActionItemActualDates` (static, `Application/Features/ActionItems`) | WO-2026-11 A-1 rule: `Resolve(previous, requested, resulting, current…, supplied…, today)` returns the actual start / end an item keeps after a save and throws `ArgumentException` for a refused value; `ApplyStatusChange(item, previous, requested, today)`; `Today(timeZoneId, utcNow)` / `TodayForWorkspaceAsync(calendars, workspaceId)` — the calendar day in the workspace calendar's time zone, UTC without a calendar |
| `ActionItemStandalone` (static, `Application/Features/ActionItems`) | WO-2026-11 A-1b rule: `ForProject(projectId)` (create) and `AfterUpdate(stored, previousProjectId, newProjectId)` (update) — `IsStandalone` follows the project link and changes only when a save changes it; the client's value is ignored |
| `IActionItemWorkflowService` | Date change requests, status change requests, review/approve/reject, escalation handling, direction giving, pending summary. An approved status change applies the actual-date rule (A-1); the workflow itself is unchanged |
| `IWorkflowNotificationHelper` | Workflow-specific notification creation (in-app + SignalR + email) with manager resolution and deduplication. WO-2026-11 A-2 added `NotifySubItemAssignedAsync(subItemId, newAssigneeIds, actorUserId)` and `NotifySubItemCompletedAsync(subItemId, actorUserId)` — their first callers (the action-item workflow service sends its own notifications); they load by id, skip the actor and inactive users, and use the seeded `ActionItem.SubItemAssigned` / `ActionItem.SubItemCompleted` templates. (The older helper methods use e-mail keys such as `WorkflowDateChangeRequested` that the seeder never created, so those e-mails are skipped as "template not found" — unchanged.) |
| `IActionItemSubItemService` | WO-2026-11 A-2. List / create / update / delete / tick / reorder sub-items with the rules above. Implementation `Application/Features/ActionItemSubItems/Services/ActionItemSubItemService.cs` |
| `ActionItemSubItemRules` (static) | A-2 pure rules: `DateRangeProblem`, `IsOutsideParentDates`, `ParentAcceptsChanges` (not Done / Cancelled), `SameDay`, `ToStoredDay`. Client copy: `sub-item-dates.util.ts` |
| `ActionItemSubItemCascade` (static) | A-2: `DeleteWithParentsAsync` / `RestoreWithParentsAsync` — used by `ActionItemService` and `ProjectService`; tracked changes only, the caller saves |
| `IProjectService` | CRUD, strategic objectives lookup, stats, cascade delete |
| `IMilestoneService` | CRUD per project, stats, baselining |
| `IProjectRiskService` | CRUD per project, risk stats, scoring |
| `IWorkspaceService` | CRUD, summary stats, admin assignment, **member CRUD (add/remove/list)**, **strategic-plan link/unlink**, enriched active-users + strategic-plans dropdowns |
| `IWorkspaceAccessResolver` | Returns the set of workspace IDs visible to a user; `null` means unrestricted (Admin / PMO Head / PMO Analyst). Drives workspace + project + action-item list filtering. Private workspaces are additionally hidden from non-members by the global query filters, even for unrestricted roles. |
| `ICurrentUserService` | Request user id from `ClaimTypes.NameIdentifier` (`null` outside HTTP requests). Injected into `AppDbContext` and exposed as `IAppDbContext.CurrentUserId`; drives the private-workspace query filters on `Workspace`, `Project`, `Milestone`, `ActionItem`, `ProjectRisk`, `ActionItemSubItem` and `ActionItemSubItemAssignee` (combined with their soft-delete filters). `null` = system context, unrestricted. Queries that call `IgnoreQueryFilters()` (include-deleted lists, restores) re-apply the rule via `PrivateWorkspaceQueryExtensions.WhereWorkspaceVisibleTo(userId)` |
| `IDashboardService` | KPI metrics, management dashboard, team workload |
| `IDashboardAnalyticsService` | Scoped aggregations for the Overview, Action Items, Projects and Strategy dashboards (`GetMyActionsAsync`, `GetTeamActivityAsync`, `GetActionItemsAsync`, `GetProjectsAsync`, `GetStrategyAsync`) |
| `INotificationService` | CRUD notifications, read/unread tracking, summary |
| `IEmailTemplateService` | CRUD templates, delivery logging |
| `IMailServerConfigService` | Read / upsert the active mail-server configuration; encrypts the password on save (via `ISecretProtector`) and decrypts at send time. `EmailSender` calls `GetActiveSmtpSettingsAsync()` and falls back to bound `SmtpSettings` from `appsettings.json` when no row exists. |
| `ISecretProtector` | Symmetric-encryption wrapper over `IDataProtectionProvider` (purpose `ActionTracker.MailServerConfig.Password.v1`) used to protect SMTP passwords (and any future secrets at rest). |
| `IDataSyncService` | Admin Data Sync operations: list jobs (with Hangfire next-run + running flag), update schedule (validates cron/time zone, re-registers), trigger immediate run (409 if already running), probe, run history, and `EnsureJobsRegisteredAsync()` at startup. |
| `IDataSyncJobHandler` | One per sync job (`JobKey`, defaults, `ExecuteAsync`, `ProbeAsync`). Current implementation: `EmployeeInfoSyncJobHandler` (`employee-info-sync`). |
| `IDataSyncJobRunner` | Invoked by Hangfire via `DataSyncHangfireJob`; writes the `DataSyncJobRuns` row, resolves the handler, records outcome and updates the job's last-run summary. |
| `IDataSyncScheduler` | Scheduler abstraction (`AddOrUpdate` / `Remove` / `TriggerNow` / `GetNextExecutionUtc` / validation). `HangfireDataSyncScheduler` in normal environments, `NoOpDataSyncScheduler` under `Testing`. |
| `IWorkCalendarService` | Work calendars (WO-2026-11 C-1). Admin CRUD, `SetDefaultAsync`, `RestoreAsync`, `GetUsageAsync`, and the calculation API for other features: `GetDefaultAsync()` / `GetForWorkspaceAsync(workspaceId)` return a `ResolvedWorkCalendar` (the workspace's own calendar, or the default when it has none or the caller cannot see the workspace). Implementation `WorkCalendarService` lives in `Application/Features/Calendars/Services`. Throws `KeyNotFoundException` (404), `ArgumentException` (400), `InvalidOperationException` (409). C-3 adds the working-day rule `EnsureWorkingDaysAsync(workspaceId, checks)` (throws `NonWorkingDateException`) and the workspace setting: `GetWorkspaceCalendarAsync(workspaceId, from, to)`, `SetWorkspaceCalendarAsync(workspaceId, dto, userId)` (validated by `SetWorkspaceCalendarDtoValidator`), `GetWorkspaceCalendarOptionsAsync()`. Changing a workspace's calendar needs no cache clear — the cache is keyed by calendar and the workspace → calendar link is read from the database each time. |
| `WorkingDateCheck` / `NonWorkingDateException` | `Application/Features/Calendars`. One date being saved with the value it replaces (`New(field, value)` on create, `Changed(field, value, current)` on update; `IsChanged` compares calendar days), and the 400 raised when a checked date is not a working day (`CalendarName`, `Dates` = field, date, next working day). |
| `ResolvedWorkCalendar` | Immutable in-memory snapshot of one calendar — no database access. `IsWorkingDay(date)`, `NextWorkingDay(date)` (the date itself when it is already a working day), `PreviousWorkingDay(date)`, `AddWorkingDays(date, n)` (start date not counted; negative moves back; 0 = unchanged), `WorkingDaysBetween(start, end)` (both ends counted; 0 when end < start), `GetWorkingHours(date)`. An exception overrides the weekly pattern; when several cover a date the most specific wins, in this order (C-3 — the result no longer depends on database order): shortest range; non-working before working; a working exception with hours of its own before one without; shorter working day; earlier start time; name (ordinal); earlier start date. The client copy in `work-calendar.util.ts` uses the same order. |
| `WorkCalendarCache` | Singleton cache of resolved calendars. Cleared after every calendar write; a version stamp stops a calendar read before a concurrent write from being cached after it; entries also expire after 10 minutes so a second app instance cannot serve a stale calendar for long. |
| `IAuthService` | Login, Azure AD validation, token refresh, logout |
| `IUserManagementService` | CRUD users, role assignment (with Strategy Editor org-unit-level guard), KU employee search, activation |
| `ICommentService` | CRUD polymorphic comments (any entity type) |
| `IDocumentService` | Upload/download/delete documents (polymorphic); per-entity caps (`KpiTarget` and `KpiActual` each capped at 10); strategic-scope-aware writes for both target and actual evidence |
| `IStrategicObjectiveService` | CRUD by org unit, soft delete/restore — all reads/writes go through `IStrategicScopeService` |
| `IStrategicPlanService` | CRUD strategic plans, active-plan lookup, soft delete with delete-while-children block, uniqueness enforcement, in-app + email fan-out to in-scope Strategy Editors on creation. Update/Delete now lock-guarded (CR-001-E). |
| `IStrategicPlanWorkflowService` | Submit / Request Revision / Review for strategic plans. Resolves reviewer via `IApprovalAuthorityResolver`, fires submit/review notifications, exposes `GetLockStateAsync` (CR-001-D + CR-001-E). **CR-001-G**: revision requests now point at a draft plan (not the published row). On final-level approval of a Revision the service calls `IStrategicPlanDraftService.PromoteDraftAsync` to merge the draft back into the published row. |
| `IStrategicPlanDraftService` | **CR-001-G**. `OpenDraftAsync` (clone published graph), `GetCurrentDraftIdAsync` (peek), `CancelDraftAsync` (hard-delete), `PromoteDraftAsync` (atomic merge into published — field-level update on rows with `Original*Id`, new rows rebranded by re-parenting FKs and clearing the draft flag, removed published rows soft-deleted with the linked-project guard rerun, draft graph then hard-deleted). |
| `IStrategicPlanLockGuard` | `EnsureNotLockedAsync(planId)` / `EnsureNotLockedForOrgUnitAsync(orgUnitId)` (throwing) and `IsLockedAsync(planId)` / `IsLockedForOrgUnitAsync(orgUnitId)` (non-throwing) — sprinkled across plan/objective/KPI/target mutation handlers. **Post CR-001-G semantics**: published plans in Approved / PendingApproval / Archived are *permanently locked* (no temporary unlock during revision — drafts are the working copy). Draft plans are editable while `Status = Draft` and locked once submitted (`Status = PendingApproval`). The guard reads with `IgnoreQueryFilters()` so it can answer for draft rows. The non-throwing variants enable field-level lock decisions: `KpiService.BulkUpsertTargetsAsync` calls `IsLockedForOrgUnitAsync` and, when locked, pins `Target` / `Baseline` / `DataSource` / `CalculationNotes` / `Direction` to their stored values while accepting incoming `Actual` + `Notes` (per CR-001-F §4.5: actuals stay editable on locked plans). New month rows still throw, and any Target-value change still throws. |
| `IApprovalAuthorityResolver` | Walks org-unit chain to closest level-3 ancestor and returns its head as the configured plan approver, or null when unconfigured (CR-001-H). |
| `IKpiService` | CRUD, monthly targets, bulk upsert (extended with Baseline/DataSource/CalculationNotes/Direction; null-means-no-change) — all reads/writes scope-checked via parent objective's org unit, mutations lock-guarded (CR-001-E). |
| `IKpiActualService` | CR-001-F. CRUD for KPI actuals, period-aware grid (`GetGridAsync`), per-slot history, cross-target list by KPI. Computes direction-aware achievement %. Scope-checked via parent KPI's org unit. **Does NOT call the plan lock guard** — actuals stay editable on Approved plans. |
| `IKpiActualWorkflowService` | CR-001-F. Submit / Review for KPI actuals. Period-end gate via `KpiPeriodCalendar`. Reviewer resolved via `IApprovalAuthorityResolver` (level-3 head). Fires `KpiActual.Submitted` / `KpiActual.Reviewed` notifications + email + PMO Head cc. |
| `IStrategicScopeService` | Resolves role-based org-unit visibility — Admin / PMO Head / PMO Analyst / Project Manager / Workspace Admin → unrestricted; Strategy Editor → user's level-2 ancestor + descendants; enforces write scope for strategic items |
| `IOrgUnitService` | Tree, CRUD, soft delete hierarchy |
| `IReportService` | CSV export, summary statistics |
| `IRoleManagementService` | CRUD roles, user assignment, permission matrix |
| `IPermissionCatalogService` | CRUD areas, actions, mappings |
| `IRolePermissionService` | CRUD role permissions, matrix queries |
| `IUserPermissionOverrideService` | CRUD user overrides |
| `IEffectivePermissionService` | Calculate merged effective permissions |
| `IOrgUnitScopeResolver` | Resolve visible org units for user |
| `IUserLookupService` | User lookup and KU employee directory integration |

---

## Middleware

| Middleware | Purpose |
|-----------|---------|
| `RequestLoggingMiddleware` | Logs every HTTP request: `[Method] Path responded StatusCode in {N}ms` |
| `ExceptionMiddleware` | Catches unhandled exceptions, returns RFC 7807 ProblemDetails JSON, suppresses stack traces in production |
| `PermissionEnforcement` | Extension point for permission-audit middleware |

---

## Middleware Pipeline Order

1. RequestLoggingMiddleware
2. ExceptionMiddleware
3. HTTPS Redirection (non-dev only)
4. CORS
5. Authentication
6. Authorization
7. PermissionEnforcement
8. Swagger
9. Controllers & SignalR Hubs

---

## Database Seeding (Startup)

1. **Auto-migrate** database on startup (`MigrateAsync` on SQL Server; `EnsureCreatedAsync` when the integration-test host swaps in the EF InMemory provider)
2. **RoleSeeder** — seeds the 10 application roles (Admin, Workspace Admin, PMO Head, PMO Analyst, Project Sponsor, Project Manager, Project Coordinator, Team Member, Strategy Editor, Viewer); idempotent. Also deletes deprecated **Manager** and **User** roles when they are still present
3. **PermissionCatalogSeeder** — seeds permission areas, actions, area-action mappings
4. **DefaultRolePermissionsSeeder** — seeds default permissions for each role (idempotent — only inserts keys with no row at all, soft-deleted rows included, so grants removed in the role UI stay removed; covered by `DefaultRolePermissionsSeederTests`). Team Member → ActionItems View / Create / Edit (Create since A-1b). Includes the Strategy Editor permission set (View/Create/Edit/Delete on Strategic Objectives + KPIs, View on Dashboard / OrgChart / Notifications)
5. **EmailTemplateSeeder** — seeds / refreshes the email templates (31 since A-2: `ActionItem.SubItemAssigned` and `ActionItem.SubItemCompleted` added). Behaviour (unchanged, pinned by `EmailTemplateSeederTests`): when every seeded key already exists (any state, soft-deleted included) it writes nothing. When at least one key is missing it inserts the missing templates **and**, in the same save, overwrites `Subject` and `HtmlBody` of every existing template (soft-deleted included) whose `HtmlBody` differs from the seed — an edit made in the Email Templates screen is lost at that startup; an edit to the subject alone is kept. So the first start after A-2 runs the overwrite pass. Production's 29 templates were byte-identical to the seed on 2026-11-24 (read-only hash comparison), so that start inserts the two new rows and changes nothing else — unless a template is edited before it
6. **WorkCalendarSeeder** — inserts the default calendar "KU Standard" (Monday–Friday 08:00–17:00, Saturday–Sunday off, `Asia/Dubai`) when no calendar exists, so it runs once on a clean database and once on an existing database after `Add_WorkCalendars`; promotes the oldest active calendar if calendars exist but none is the default. Idempotent
7. **AdminBootstrapSeeder** — seeds the bootstrap admin user

### Developer Experience (Startup)
- In **Development** environment, the host registers an `ApplicationStarted` callback that opens the system browser to `{baseUrl}/swagger` automatically. Works regardless of `dotnet run` / Visual Studio / VS Code launch path. `launchSettings.json` keeps `launchBrowser=false` to prevent duplicate tabs.

---

## Key Architectural Decisions

| Decision | Rationale |
|----------|-----------|
| Soft Deletes | Preserves audit trail, recoverable via restore endpoints |
| Denormalized Permissions | Stores AreaName/ActionName for fast reads without FK joins |
| Polymorphic Relationships | Documents/Comments use string type discriminators |
| Org Unit Scoping | Multi-tenant data isolation via org unit hierarchy |
| Auto-Seeding | Permission catalog seeded on startup for consistency |
| UTC Everywhere | All datetimes internally UTC with EF value converters |
| Hybrid Auth | Dual JWT schemes (local + Azure AD) routed via tid claim |
| FluentValidation | Auto-validated DTOs before handlers run |
| Async All the Way | All I/O operations use async/await |
| Auto-Generated Codes | ActionId (ACT-001), ProjectCode (PRJ-YYYY-001), MilestoneCode (MS-YYYY-001), RiskCode (RISK-001), ObjectiveCode (SO-001) |
| Progress Clamping | ActionItem.Progress auto-clamped to 0-100 |
| Token Rotation | Refresh tokens invalidated on use for security |
| Cascade Deletes | Soft-deleting projects cascades to milestones and action items |
| SignalR | Real-time communication hub for notifications |
| Centralized Role Constants | All role names live in `AppRoles` — no magic strings |
| Two-Layer Org Scoping | `IOrgUnitScopeResolver` (data-tier visibility) + `IStrategicScopeService` (role-driven Strategy/KPI scope) |
| Per-Entity Document Caps | Document attachments throttled per entity type (e.g. KpiTarget → 10 files) |
| Working Calendars | Date maths goes through `IWorkCalendarService` → `ResolvedWorkCalendar` (pure, cached) rather than ad-hoc weekend checks; one default calendar is guaranteed by a filtered unique index plus the startup seeder |
| Planned vs actual dates | `StartDate` / `DueDate` stay the planned dates (freeze, date requests, working-day rule, filters and `DashboardRules` depend on them); `ActualStartDate` / `ActualEndDate` are recorded facts outside all three date rules, maintained by one shared rule (`ActionItemActualDates`) on every status path |
| Standalone flag is derived | `ActionItem.IsStandalone` is set by the server from `ProjectId` (`ActionItemStandalone`), never from the client, so a client cannot detach an item from its project by accident (WO-2026-11 A-1b) |
| Sub-items are not action items | `ActionItemSubItem` lives in its own table with its own assignee table, so `DashboardRules`, `/my-stats`, `/my-actions`, reports and every assignee-based count stay as they were; a user assigned only to a sub-item gets a separate My Actions list (WO-2026-11 A-2) |

---

## Configuration (appsettings.json)

| Key | Description |
|-----|-------------|
| `ConnectionStrings:DefaultConnection` | SQL Server connection string (`ActionTrackerDb`) |
| `Jwt:Key` | HS256 symmetric signing key |
| `Jwt:Issuer` | JWT issuer |
| `Jwt:Audience` | JWT audience |
| `Jwt:ExpiryMinutes` | Access token expiry |
| `Jwt:RefreshTokenExpiryDays` | Refresh token expiry |
| `AzureAd:TenantId` | Microsoft Entra tenant ID |
| `AzureAd:ClientId` | Azure AD application client ID |
| `AzureAd:ClientSecret` | Azure AD client secret |
| `AzureAd:Instance` | Azure AD instance URL |
| `AllowedOrigins` | CORS allowed origins |
| `App:FrontendBaseUrl` | Frontend base URL for deep links |
| `Smtp:Host/Port/UseSsl/FromEmail/FromName/Username/Password` | SMTP email configuration |
| `FileStorage:Path/MaxFileSizeMB/AllowedExtensions` | File storage settings |
| `DataSync:EmployeeInfo:SourceConnectionString` | SQL connection string to the HR source (KU1ICDDWV008 / KU_BOT_PROD). Contains credentials — `appsettings.json` is git-ignored; never commit it |
| `DataSync:EmployeeInfo:SourceTable` / `TargetTable` | Schema-qualified table names (default `dbo.ku_employee_info` on both sides) |
| `DataSync:EmployeeInfo:MinRowRatio` | Safety guard: abort when source rows < ratio × target rows (default 0.9; 0 disables) |
| `DataSync:EmployeeInfo:CommandTimeoutSeconds` | Per-command timeout for the sync (default 120) |
| `Serilog` | Structured logging configuration (Console + File with daily rolling) |
