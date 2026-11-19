# KU Action Tracker - Frontend Overview

> Last updated: 2026-11-18 (Dashboard hub with drill-down: Overview reworked, new Action Items / Projects / Strategy dashboards, shared dashboard components; brand redesign of inner pages)

## Tech Stack

- **Framework:** Angular 20.3 (Standalone Components, no NgModules)
- **Build Tool:** Vite with Angular CLI 20.3.17 (`analytics: false` in `angular.json`)
- **UI Framework:** Bootstrap 5.3.3 + Bootstrap Icons 1.13.1
- **Charts:** ng2-charts 6.0.1 / Chart.js 4.4.7
- **Notifications:** ngx-toastr 19.0.0
- **Multi-select:** @ng-select/ng-select 21.8+ (theme CSS bundled via `angular.json`)
- **Real-time:** @microsoft/signalr 10.0.0
- **Excel Export:** xlsx 0.18.5
- **Date Utils:** date-fns 4.1.0
- **Animations:** @angular/animations 20.3.16
- **Popper:** @popperjs/core 2.11.8
- **Auth:** @azure/msal-browser 3.30+ / @azure/msal-angular 3.1+

---

## Authentication

### Dual Authentication Flow
1. **Azure AD (MSAL):** User clicks login -> MSAL popup -> Azure login -> redirect to `/auth_fallback` -> `AuthService.loginWithAzureAd(msalToken)` -> backend validates -> JWT tokens stored in localStorage.
2. **Local (Email/Password):** User enters credentials -> `AuthService.login()` -> backend validates -> JWT tokens stored in localStorage.

### Token Management
- `access_token` - JWT Bearer token for API requests
- `refresh_token` - used to obtain new access tokens
- `auth_user` - user info JSON (userId, email, displayName, loginProvider, roles)

### Guards
| Guard | Purpose |
|-------|---------|
| `authGuard` | Checks JWT validity, redirects to `/login` if expired |
| `loginGuard` | Prevents authenticated users from accessing login page |
| `noAuthGuard` | Prevents authenticated users from accessing public pages |
| `permissionGuard` | Checks effective permissions (area + action), redirects to `/access-denied` |
| `permissionDataGuard` | Ensures permission data is loaded before route activation |
| `adminGuard` | Requires Admin role |
| `roleGuard` | Requires a specific role |

### HTTP Interceptors
| Interceptor | Purpose |
|-------------|---------|
| `authInterceptor` | Attaches Bearer token; handles 401 with token refresh + request queue |
| `refreshTokenInterceptor` | Manages refresh token rotation |
| `loadingInterceptor` | Shows/hides global loading bar |
| `errorInterceptor` | Displays toast errors for failed requests |

---

## Routing Structure

| Route | Component | Permission Required |
|-------|-----------|-------------------|
| `/` | Redirects to `/dashboard` | - |
| `/login` | Login page | loginGuard (redirect if authed) |
| `/unauthorized` | Unauthorized page | - |
| `/access-denied` | Access denied page | - |
| `/auth_fallback` | MSAL redirect handler | - |
| `/dashboard` | Dashboard — Overview (`TeamDashboardComponent`) | Dashboard.View |
| `/dashboard/actions` | Action Items dashboard | Dashboard.View |
| `/dashboard/projects` | Projects dashboard | Dashboard.View |
| `/dashboard/strategy` | Strategy dashboard | StrategicPlans.View |
| `/management` | Management Dashboard | ActionItems.View |
| `/actions` | My Actions list | - |
| `/actions/new` | Create action | - |
| `/actions/:id/edit` | Edit action | - |
| `/actions/:id/view` | Action detail | - |
| `/reports` | Reports & CSV export | Reports.View |
| `/workspaces` | Workspace list | Workspaces.View |
| `/workspaces/new` | Create workspace | Workspaces.Create |
| `/workspaces/edit/:id` | Edit workspace | Workspaces.Edit |
| `/workspaces/:id` | Workspace detail | Workspaces.View |
| `/workspaces/:id/settings` | Workspace settings — admins, members, PMs, strategic approvers, strategic plan link | Workspaces.View (write actions still need Workspaces.Edit) |
| `/projects/my` | My Projects | - |
| `/projects/new` | Create project | Projects.Create |
| `/projects/edit/:id` | Edit project | Projects.Edit |
| `/projects/:id` | Project detail | - |
| `/projects/:projectId/milestones/:milestoneId` | Milestone detail | - |
| `/notifications` | Notifications center | - |
| `/approvals` | My Approvals (pending reviews, submitted requests) | - |
| `/profile` | User profile | - |
| `/admin` | Admin panel | adminGuard |
| `/admin/org-chart` | Org units | OrgChart.View |
| `/admin/objectives` | Strategic objectives | StrategicObjectives.View |
| `/admin/kpis` | KPI management | KPIs.View |
| `/admin/kpis/:kpiId/targets` | KPI monthly targets | KPIs.View |
| `/admin/kpis/:kpiId/targets/:targetId/actuals` | Period-aware KPI Actuals grid (CR-001-F) | KpiActuals.View |
| `/admin/email-templates` | Email templates | EmailTemplates.View |
| `/admin/mail-server-config` | Mail server configuration (SMTP settings) | MailServerConfig.View |
| `/admin/data-sync` | Scheduled data-sync jobs (HR employee info) — schedule, run now, test connection, history | DataSync.View |
| `/admin/permissions/roles` | Role permission matrix | PermissionsManagement.View |
| `/admin/permissions/users` | User permission overrides | PermissionsManagement.View |
| `/admin/roles` | Roles list | Roles.View |
| `/admin/roles/:roleName/permissions` | Edit role permissions | Roles.Edit |
| `/admin/roles/:roleName/users` | Assign users to role | Roles.Assign |
| `/admin/users` | User management | UserManagement.View |
| `/strategy` | Strategy home — card grid of in-flight plans across the user's visible org units, with "New Strategy" creator | StrategicPlans.View |
| `/strategy/org-units/:orgUnitId` | Per-org-unit strategy page (plan header + objectives list) | StrategicPlans.View |
| `/strategy/objectives/:objectiveId` | Strategic objective detail with inline KPIs and current-year targets | StrategicObjectives.View |

---

## Components (~74 Total)

### Layout
| Component | Description |
|-----------|-------------|
| `LayoutComponent` | Main layout: loading bar, static brand backdrop (`.bg`, see `_background.scss`), sticky header, centred content column (`--page-max` 1760px, `--M` gutter), footer. `.wrap` / `.ct` must stay free of transform / opacity / filter so page-level fixed drawers and dialogs are not trapped under the header |
| `HeaderComponent` | Sticky single-row header (`--hdr-h` 64px / 56px mobile) on `--nav`, which equals the logo PNGs' baked-in background (`#262A33` dark / white light — do not make it translucent). KUmmand Center logo swapped by theme (`kummand_center_dark_mode_transparent.png` / `kummand_center_light_mode_transparent.png`), permission-based nav with brand-gradient underline on the active link, theme toggle, notification bell, avatar menu, brand hairline on the bottom edge. Nav degrades in steps: icons shown only ≥1800px, compact ≤1640px, off-canvas drawer (same logo) ≤1440px |
| `FooterComponent` | Application footer on `--nav` with KU logo (theme-aware: original artwork in light, white-inverted in dark), link columns aligned to the page column, brand hairline on the top edge, Command Center version pill |
| `LoadingBarComponent` | Global loading indicator on route transitions and HTTP requests |

### Header Navigation Links
| Label | Route | Visibility |
|-------|-------|-----------|
| Dashboards | `/dashboard` | Dashboard.View |
| My Actions | `/actions` | All authenticated users |
| My Projects | `/projects/my` | All authenticated users |
| Reports | `/reports` | Reports.View |
| Team Actions | `/management` | ActionItems.View |
| Workspaces | `/workspaces` | Workspaces.View |
| Strategy | `/strategy` | StrategicPlans.View |
| Notifications | `/notifications` | All authenticated users |
| My Approvals | `/approvals` | All authenticated users (with pending count badge) |
| Admin Panel | `/admin` | Admin role only |

### Shared UI Components (13)
| Component | Description |
|-----------|-------------|
| `BreadcrumbComponent` | Navigation breadcrumb trail (home glyph on the first crumb, current page in accent). `AdminBreadcrumbComponent` shares the same styling |
| `PageHeaderComponent` | Page title block used by the inner pages: icon on a brand-gradient tile with a lighter "back card" (echoes the logo mark), title in `--th`, subtitle, right-hand action slot (`<ng-content>`) |
| `StatusBadgeComponent` | Color-coded status indicator |
| `PriorityBadgeComponent` | Color-coded priority indicator |
| `RiskRatingBadgeComponent` | Color-coded risk rating badge (Critical/High/Medium/Low) |
| `ProgressBarComponent` | Visual progress bar (0-100%) |
| `DataTableComponent` | Reusable sortable/paginated data table |
| `ConfirmDialogComponent` | Confirmation modal dialog |
| `CommentsSectionComponent` | Comments display with CRUD, high-importance flag |
| `DocumentsSectionComponent` | File upload, download, and delete |
| `KpiCardComponent` | KPI figure card. `icon` accepts a Bootstrap Icons class (`"bi bi-building"`) or a literal glyph; uses the shared icon tile; every card is brand blue and `colorClass="danger"` is the only value that changes colour (red tile, top bar and hover ring) — the other values are accepted and render as the default |
| `NotificationBellComponent` | Header notification bell with unread count |
| `WorkflowReviewDialogComponent` | Reusable approve/reject dialog for workflow requests |

### Shared Directives
| Directive | Description |
|-----------|-------------|
| `HasPermissionDirective` | `*hasPermission="'Area.Action'"` — conditionally renders elements |
| `HasOrgUnitDirective` | Conditionally renders elements based on org unit visibility |

### Shared Pipes
| Pipe | Description |
|------|-------------|
| `OrgUnitScopePipe` | Formats org unit scope display |
| `PermissionSourcePipe` | Formats permission source (Role/Override) |

### Dashboard (5 pages + shared pieces in `features/dashboard/shared/`)
| Component | Description |
|-----------|-------------|
| `TeamDashboardComponent` | `/dashboard` Overview. Top row with the greeting as the page heading (date beside it), a New Action button and the dashboard tabs; portfolio stat cards (Admin) and action-item stat cards — all clickable. **My Actions** as charts (status doughnut, open-by-priority and open-by-due bars, summary pills); **Team Activity** as a ranked list of people by action-item count; Recent Items table; New Action drawer. Shows a retry state (not an empty state) when a request fails |
| `ActionsDashboardComponent` | `/dashboard/actions`. 7 stat cards, status doughnut, created-vs-completed line (8 weeks), open by priority / due window / age bars, team workload and by-workspace ranked lists |
| `ProjectsDashboardComponent` | `/dashboard/projects`. 7 stat cards, health doughnut, status bars, strategic-vs-operational doughnut, progress bands, health by workspace, "needing attention first" table, milestone pills + table. Groups charts client-side from the project rows the API returns. Exports `projectDrillRow()` / `healthTone()` (reused by the strategy dashboard) |
| `StrategyDashboardComponent` | `/dashboard/strategy`. Year selector, 7 stat cards, KPI health doughnut, objectives ranked by KPI health, weakest-first KPI table, strategic projects table, plans table |
| `DrilldownPanelComponent` | Right-hand drawer fed a `DrilldownRequest`: `kind: 'actions'` (server-paged via `ActionItemService.getAll` / `getMyActions` with filters, "Show more", optional link to a full page) or `kind: 'rows'` (pre-built `DrillRow[]`). Rows link to the record's page; Esc / overlay closes; stale responses are ignored |
| `DashStatComponent` | Stat card as a `<button>` (icon tile, value, label, hover arrow, `tone="danger"`, loading skeleton); emits `activate` |
| `DashPanelComponent` | Titled card with icon tile, subtitle and a `[panelActions]` slot |
| `DashLegendComponent` | Chart legend as buttons — the keyboard / screen-reader route to the same drill-down as clicking a canvas segment |
| `RankedBarsComponent` | Sorted list of stacked bars scaled to the largest total (people, workspaces, objectives); rows are buttons; "Show all" beyond `limit` |
| `DashTabsComponent` | Overview / Action Items / Projects / Strategy switcher (Strategy hidden without `StrategicPlans.View`) |
| `chart-theme.ts` | Series colours (status, priority, due, age, health) and theme-aware `doughnutOptions` / `barOptions` / `lineOptions` builders with click-to-drill + pointer cursor |
| `_dash-page.scss` | Layout shared by the dashboard pages (`.dash`, `.dash-stats`, `.dash-grid--2/3`, fixed-height `.chart-box`, `.donut`, `.dash-table`, `.chip`, `.pbar`, `.dash-state`) |
| `ManagementDashboardComponent` | Team workload, at-risk items, status breakdown (doughnut chart), critical actions. Auto-refreshes every 30 seconds |

### Action Items (3)
| Component | Description |
|-----------|-------------|
| `ActionListComponent` | "My Actions" list with filters (status, priority, workspace), pagination, stats cards, "Created by Me" section, show-deleted toggle, export to Excel, PDF print. Create/edit via offcanvas panel |
| `ActionFormComponent` | Create/edit form with multi-assignee dropdown, status, priority, dates, escalation, workspace/project/milestone linking. The Assignee(s) chips are loaded per-workspace from `GET /api/workspaces/{id}/assignable-users` (admins + members + PMs) instead of every active user. The list is empty until a workspace is picked; changing the workspace clears any assignees no longer eligible and shows a toast; on edit, already-assigned users no longer on the workspace's people lists are merged in so they stay visible. |
| `ActionDetailComponent` | Detail view with comments and documents sections. The inline edit form's assignee picker pulls from the same workspace-scoped endpoint keyed off the action item's `workspaceId`. |
| `ActionListComponent` | The inline create/edit panel's Assignee(s) picker now cascades from the Workspace dropdown — disabled until a workspace is picked, refreshed when it changes, with any stale selections cleared. |

### Workspaces (4)
| Component | Description |
|-----------|-------------|
| `WorkspaceListComponent` | All workspaces with filters, org-unit dropdown, stats (projects, milestones, action items), export/print, **inline create/edit drawer** with custom searchable multi-select admin picker (filters across name, email, org unit; chip-style selected display); admin users loaded lazily on first drawer open. Reads `?orgUnitId=<id>` on init so deep-links from `StrategyPageComponent`'s "Projects" stat card land on a pre-filtered list |
| `WorkspaceFormComponent` | Standalone create/edit workspace screen (kept for direct routes) — also uses the same custom multi-select admin picker as the list drawer. Both this form and the list drawer have a **Private workspace** switch (`isPrivate`, create + edit) with a warning that non-members lose access after saving; the list shows a lock **Private** badge (`.wl-private-badge`) and the detail page a **Visibility** row. `Workspace` / `WorkspaceList` models carry `isPrivate`; `CreateWorkspace` / `UpdateWorkspace` send it (the settings page passes the current value through so admin edits don't reset it) |
| `WorkspaceDetailComponent` | Detail view with projects, milestones, action items, stats, export/print. Access management (admins, members, project managers, strategic-project approvers, strategic plan link) is **no longer** inline — replaced by a read-only access summary (admin/member counts) and a **"Configure Workspace"** button that routes to `/workspaces/:id/settings`. The PM dropdown in the create/edit project drawer is restricted to the workspace's Project Managers list. |
| `WorkspaceSettingsComponent` | Dedicated `/workspaces/:id/settings` page that owns all access configuration: Linked Strategic Plan, Admins, Members, **Project Managers**, and **Strategic Project Approvers** (sequential chain with up/down arrows to reorder; level-1 badge on each entry). Every section uses the same searchable multi-select picker pattern, supporting bulk add. Operational projects bypass the approver chain — only strategic submissions consult it. The Members section also exposes a bulk **"Add org unit users"** action — an org-unit picker (uses `getOrgUnitsForDropdown()`) + button that calls `POST /api/workspaces/{id}/members/from-org-unit` and appends the returned members in one round-trip (existing members are skipped server-side). |

### Projects (7)
| Component | Description |
|-----------|-------------|
| `MyProjectsComponent` | Role-based filtered projects list |
| `ProjectFormComponent` | Create/edit with workspace, type (Strategic/Operational), status, priority, manager, sponsors, dates, budget, strategic objective linking. Activation validation |
| `ProjectDetailComponent` | Detail view with Gantt chart, milestones grid (inline action items), completion stats, export/print |
| `MilestoneSectionComponent` | Milestone list within a project |
| `MilestoneDetailComponent` | Milestone detail with action items, export/print |
| `RiskRegisterSectionComponent` | Risk list within a project |
| `RiskFormComponent` | Create/edit risk with probability/impact scoring, mitigation/contingency plans |
| `RiskDetailComponent` | Risk detail view |

### Notifications (1)
| Component | Description |
|-----------|-------------|
| `NotificationsPageComponent` | Full notifications center. Groups by date (Today, Yesterday, This Week, Earlier). Filters by type and read/unread. Mark as read, delete individual/batch, load more pagination |

### Workflow (1)
| Component | Description |
|-----------|-------------|
| `MyApprovalsComponent` | My Approvals page with five tabs (Pending Reviews, My Requests, Project Approvals, **Strategy Approvals**, **KPI Actuals**), stat cards, approve/reject with comments, pagination. Pending reviews show date/status change details; Project / Strategy / KPI Actual tabs each split into Pending and My Requests/Submissions sections, each with an inline review modal (rejection requires comment). Strategy tab shows the request type badge (Initial / Revision) and the org-unit name. KPI Actuals tab (CR-001-F) shows the period label, actual vs target, and evidence count |

### Admin Panel (10)
| Component | Description |
|-----------|-------------|
| `AdminPanelComponent` | Admin dashboard home with feature cards |
| `OrgChartListComponent` | Org unit hierarchy list (up to 10 levels) |
| `OrgUnitFormComponent` | Create/edit org unit |
| `OrgUnitMembersComponent` | Side panel on the Org Chart page (mode `members`) — lists users assigned to the selected org unit with a "remove from unit" button per row, and a typeahead search (debounced 250 ms, min 2 chars, calls `userMgmt.getUsers(1, 20, term)`) to add new members. Uses `orgUnitService.getUsers(id)` + `userMgmt.assignOrgUnit(userId, { orgUnitId })`. When the picked user is already in a different unit, prompts a move-confirmation since `OrgUnitId` is single-valued |
| `ObjectivesListComponent` | Strategic objectives list (paginated, filterable by org unit) |
| `ObjectiveFormComponent` | Create/edit strategic objective. Three create-mode inputs: `defaultOrgUnitId` pre-fills the picker (admin flow); `restrictToOrgUnitSubtree` runs a parentId BFS over the loaded units and **filters the dropdown to root + descendants only**, defaulted to the root; `strategicPlanId` is forwarded in the create payload so the new objective is explicitly attached to that plan instead of relying on the backend's "active plan for the picked org unit" auto-resolve. The Strategy page passes both restrict + plan-id so a user can drop objectives on any sub-unit of the strategy's org unit while staying linked to the parent plan |
| `KpiListComponent` | KPI management list |
| `KpiFormComponent` | Create/edit KPI. Two ways to drive the objective field: `[preselectedObjectiveId]` pre-fills the dropdown but leaves it editable (admin filter flow); `[restrictToObjectiveId]` fetches **only** that one objective via `getById`, renders it as the sole option, and disables the control (used by `ObjectiveDetailComponent` so the inline "Add KPI" form can't be retargeted to an unrelated objective). Edit mode disables the field regardless |
| `KpiTargetsComponent` | Period-aware target management (12 / 4 / 2 / 1 row(s) per year by KPI period); inline edit with Enter/Esc keybindings, year navigation, variance + % achievement colour coding, Save All bulk upsert. Per-row evidence button opens an `app-documents-section` modal scoped to the saved `KpiTarget` (max 10 files, PDF/DOC/DOCX/XLS/XLSX/PPT/PPTX, 10 MB cap). Reusable in two modes: standalone (`/admin/kpis/:kpiId/targets`) and embedded (passed `[embedded]`, `[locked]`, `[planStartYear]`, `[planEndYear]` from `ObjectiveDetailComponent`). When `locked` is true the component applies field-level read-only — the Target cell renders as a lock-decorated span (no input, even mid-edit), `commitEdit` pins the existing target value, the banner explains "target values are read-only but Actual / Notes / Evidence remain editable", and the Save All button is relabeled "Save Actuals & Notes". When the year-range inputs are set the year picker clamps navigation and hides both chevrons for single-year plans. Below 576 px the table collapses into stacked label/value cards via `data-label` attributes + a `::before` rule for full mobile readability |
| `EmailTemplatesPageComponent` | Email template management |
| `EmailTemplateEditComponent` | Edit email template subject and HTML body |
| `MailServerConfigComponent` | Admin form to configure the SMTP server used by Command Center (host, port, SSL, from, credentials); password stored encrypted via DataProtection on the backend |
| `DataSyncComponent` | Admin page listing every background sync job as a card: source → target summary, schedule editor (frequency presets + raw cron + time zone + enabled switch, Save gated by `DataSync.Edit`), next-run time, last-run status/rows/duration/message, **Run now**, **Test connection** (source/target reachability, row counts, column diff), collapsible run-history table. Polls every 3 s while a run is in progress |

### Permissions (7)
| Component | Description |
|-----------|-------------|
| `RolePermissionsPageComponent` | Role permission matrix editor |
| `RolePermissionMatrixComponent` | Permission matrix grid |
| `RolePermissionRowComponent` | Single permission row in matrix |
| `UserOverridesPageComponent` | User-level permission overrides |
| `UserOverrideFormComponent` | Create/edit user permission override |
| `UserOverridesListComponent` | List of user overrides |
| `UserEffectivePermissionsSummaryComponent` | Effective permissions summary |

### Roles (3)
| Component | Description |
|-----------|-------------|
| `RolesListPageComponent` | All roles with user count, 4-card-per-row layout |
| `RolePermissionsPageComponent` | Edit role permissions |
| `RoleUsersPageComponent` | Assign users to roles, assign org units. Paginated user list |

### User Management (3)
| Component | Description |
|-----------|-------------|
| `UserListComponent` | All registered users with pagination, search, inline role/org-unit editing |
| `RegisterAdUserComponent` | Register existing Azure AD user (search employee directory) |
| `RegisterExternalUserComponent` | Register local (non-AD) user with email/password |

### Auth (3)
| Component | Description |
|-----------|-------------|
| `LoginComponent` | Login page on a pure-CSS brand backdrop (no images) using component-scoped `--lg-*` tokens sampled from the KUmmand logo (navy / royal / blue / sky): slanted ribbon shapes + square "pixel" clusters, brand-blue radial light, reduced-motion aware. Card surface matches the logo PNGs' baked-in backgrounds (`#262A33` dark / white light; light logo also uses `mix-blend-mode: multiply`). Royal-gradient primary Azure AD button (KU emblem cropped from `cropped-logo.png`), tinted-outline secondary local Sign In. Theme-aware KUmmand logo, Command Center branding, Azure AD and local auth support |
| `UnauthorizedComponent` | 401 unauthorized error page |
| `AccessDeniedComponent` | 403 access denied error page |

### Profile (1)
| Component | Description |
|-----------|-------------|
| `ProfileComponent` | User profile showing display name, email, login provider, assigned roles, employee info |

### Strategy (4) — CR-001-B / CR-001-D / CR-001-E
| Component | Description |
|-----------|-------------|
| `StrategyShellComponent` | `/strategy` home page. Lists existing in-flight strategic plans across the user's visible org units as a card grid (workspaces-themed). Page header has a "New Strategy" button (gated by `StrategicPlans.Create`) that opens `PlanFormDialogComponent` with an org-unit selector restricted to in-scope units without an in-flight plan; the button is disabled when every unit in scope already has one. Summary cards show total / approved / pending / draft counts; toolbar includes a search box matching plan name, org-unit name, and code. Empty states for "no org units in scope" and "no plans yet" |
| `ObjectiveDetailComponent` | `/strategy/objectives/:objectiveId`. Objective + KPIs + targets viewer, opened via the "View objective" link on `StrategyPageComponent`. Breadcrumb `Strategy → {OrgUnit} → {ObjectiveCode}`; 3-up summary (KPIs, Targets in current year, Objective code); detail card with statement / description / org unit; per-KPI card with code, name, period badge, unit, YTD achievement % (averaged across slots that have both target + actual; colour-coded good ≥90% / warn ≥60% / bad <60%), and a period-aware target table (Monthly = 12 rows, Quarterly = Q1..Q4, Semi-Annual = H1..H2, Yearly = Annual). "Manage targets" button on each KPI opens `KpiTargetsComponent` in a right-side offcanvas drawer (`embedded` mode strips the breadcrumb / page-header / back-button chrome); the drawer is wired with `[locked]` / `[planStartYear]` / `[planEndYear]` from the enriched objective DTO (see Admin entry for `KpiTargetsComponent`) so it renders read-only when the parent plan is Approved/Archived and clamps the year picker to the plan cycle. On close the objective + KPI targets reload so saved changes appear on the cards. The standalone full-page route `/admin/kpis/:kpiId/targets` is still available for direct deep-links. **Inline KPI creation + edit**: when the parent plan is in **Draft**, the "KPIs & targets" section header shows an "Add KPI" button (gated by `KPIs.Create`) and each KPI card carries a ghost-style "Edit" button (gated by `KPIs.Edit`). Both open the same narrower offcanvas (`.od-offcanvas--narrow`, 520 px) hosting `KpiFormComponent`; Add binds `[restrictToObjectiveId]` only (locks the objective dropdown to the page's objective), Edit also binds `[existingKpi]`. After a *create* the page auto-opens the targets drawer for the new KPI; after an *edit* it just refreshes the cards. **Inline objective edit**: an "Edit objective" button next to the page header opens a separate offcanvas hosting `ObjectiveFormComponent` with `[existingObjective]` bound — also Draft-gated, also re-uses the same `load()` refresh path so plan-status / lock fields are reconciled after save. On save, the page reloads its objective + KPIs + targets and **auto-opens the targets drawer for the newly-created KPI** so the user can add targets without a second click |
| `StrategyPageComponent` | `/strategy/org-units/:orgUnitId` per-unit view, themed to match `MyProjects`. Uses shared `app-breadcrumb` (`Strategy → {OrgUnit}`) and `app-page-header`; right-slot action buttons cover Submit for Approval / Edit / Archive / Request Revision (all permission- and lock-gated). The whole page is strictly scoped to the **selected strategy** — once the active plan is resolved, objectives are loaded via the dedicated `GET /api/strategicobjectives/by-plan/:planId` endpoint (server-side scoping, no client-side filter) and the KPIs + Strategic Projects sections derive from those plan-scoped objective ids. Page load is two-phase: org unit + plan list + workspace summary first, then objectives → projects + KPIs once the plan id is known. 4-up stat cards: Plan Status (informational, not interactive), Strategic Objectives, KPIs (sum of `objective.kpiCount` across filtered objectives), **Strategic Projects** (count of all non-deleted **strategic-type** projects, any status, whose `StrategicObjectiveId` matches one of this plan's objectives, fetched via `GET /api/projects?projectType=strategic&strategicObjectiveIds=…` — operational projects are excluded). All three clickable cards smooth-scroll to dedicated in-page sections (`#sp-objectives-section`, `#sp-kpis-section`, `#sp-projects-section`). Themed plan-detail card with status pill, cycle, approver preview, and org-unit code. Themed objectives, **KPIs** (objective code + KPI number, name, period pill, unit, parent objective statement, target count, "View objective" link — populated by fanning out `GET /api/kpis/by-objective/:objectiveId` per plan-linked objective and merging), **strategic-projects** (project code + name + status pill + linked objective + PM, with a "View project" link to `/projects/:id`), and approval-history sections. **Each of the three lists (objectives / KPIs / projects) paginates client-side at 5 rows per page** with a Prev / "Page X of Y · N total" / Next pager footer; pages reset to 1 on data reload. Pending-approval / approved-and-locked / revision-pending banners surface workflow state. **Inline objective creation**: when the active plan is in **Draft**, the objectives section header shows an "Add Strategic Objective" button (gated by `StrategicObjectives.Create`) that opens a right-side offcanvas hosting `ObjectiveFormComponent`, pre-filled with the page's `orgUnitId`. On save, the plan-scoped objective list refreshes and KPIs/projects re-derive |
| `KpiActualsGridComponent` | CR-001-F. Period-aware actuals grid at `/admin/kpis/:kpiId/targets/:targetId/actuals`. Rows are Q1..Q4 / H1..H2 / Annual depending on the parent KPI's measurement period. Each row shows period label, target value, inline-editable actual + notes (only when Draft), evidence chip count + "Manage" modal (reuses `DocumentsSectionComponent` with `entityType=KpiActual`), status badge, direction-aware achievement % (only when Approved), and actions: Save Draft / Submit for Approval / Delete / History. The Submit button is disabled with a tooltip ("Available after {Label} ends on {Date}") when the period has not yet ended (server still enforces). History modal lists all prior actuals (including Rejected) for that slot |
| `PlanFormDialogComponent` | Right-side dialog used for both create and edit plan flows. Validates name (≤ 200), startYear (2000–2100), durationYears (1–10); previews the cycle (`StartYear`–`EndYear`) inline; surfaces 409 conflicts (existing active plan) inline. Renders the `app-org-unit-select` whenever `orgUnitOptions` is non-empty — `StrategyShellComponent` passes available units in create mode, and `StrategyPageComponent.openEdit()` lazy-loads visible units (current + units without a non-archived plan) for edit mode so the user can re-assign a Draft plan. SCSS uses the project design tokens (`--el` / `--bd` / `--t1` / `--t2` / `--inp` / `--accent`) to match the visual language of `ObjectiveFormComponent` and `KpiFormComponent` in both themes |
| `JustificationDialogComponent` | Reusable right-side dialog with a single 2000-char justification textarea. Reused by both Submit for Approval and Request Revision flows; the title and primary button label are inputs (CR-001-D + CR-001-E) |
| `ManageApproversDialogComponent` | Right-side dialog for configuring the per-plan **sequential approval chain**. Inputs: `planId`, `existingApprovers`. Uses `WorkspaceService.getActiveUsersForDropdown()` for the active-users picker (search by name / email / org-unit, filters out users already in the chain). Add via picker, reorder with up/down arrows, remove with ×. Save calls `PUT /api/strategic-plans/{id}/approvers` (replace-the-chain). Backend rejects edits while the plan is not in Draft, so the parent only mounts this dialog when `canManageApprovers()` is true |

### Reports (1)
| Component | Description |
|-----------|-------------|
| `ReportsComponent` | Analytics dashboard with global KPIs, action items breakdown by category/priority (bar + doughnut charts), team performance stats, filterable CSV export |

---

## Services & API Integration

### Core Services (in `core/services/`)

| Service | Key Endpoints |
|---------|--------------|
| `AuthService` | `POST /auth/login`, `/azure-login`, `/refresh-token`, `/logout` |
| `ActionItemService` | Full CRUD on `/action-items`, plus `/my-actions`, `/created-by-me`, `/my-stats`, `/process-overdue`, comments CRUD. Assignee lookup now lives on `WorkspaceService.getAssignableUsers(workspaceId)` — the legacy unscoped `/api/action-items/assignable-users` endpoint was removed. |
| `DashboardService` | `GET /dashboard/kpis`, `/management`, `/team-workload`, `/status-breakdown` (used by the Management dashboard and Reports) |
| `DashboardAnalyticsService` | `GET /dashboard/my-actions`, `/team-activity`, `/action-items`, `/projects`, `/strategy?year=` — scoped server-side to what the user may see |
| `ReportService` | `GET /reports/export-csv`, `/summary` |
| `DocumentService` | `GET/POST/DELETE /documents`, `GET /documents/:id/download` |
| `CommentService` | CRUD on polymorphic entity comments |
| `NotificationService` | `GET /notifications`, `/unread-count`, `/summary`, mark as read, delete |
| `ProfileService` | `GET /profile/me` |
| `UserService` | User data retrieval |
| `LoadingService` | Global loading state (BehaviorSubject) |
| `ToastService` | Toast notifications via ngx-toastr |

### Feature Services (in `services/` or feature folders)

| Service | Key Endpoints |
|---------|--------------|
| `WorkspaceService` | Full CRUD on `/workspaces`, plus `/summary`, `/stats`, `/org-units`, `/active-users`, `/strategic-plans`, `/{id}/members` (GET/POST/DELETE), `/{id}/assignable-users` (GET — admins + members + PMs; called by every workspace-context user picker: action items, project sponsors, milestone assignees, risk owners), `/{id}/strategy` (PUT), **`/{id}/project-managers` (GET/POST/DELETE)**, **`/{id}/strategic-project-approvers` (GET/POST/DELETE + `/reorder` PUT)** |
| `ProjectService` | Full CRUD on `/projects`, plus `/stats`, `/strategic-objectives-for-workspace/:id`. Assignee lookup was removed — callers now use `WorkspaceService.getAssignableUsers(workspaceId)`. |
| `MilestoneService` | Full CRUD on `/projects/:id/milestones`, plus `/stats`, `/baseline` |
| `ProjectRiskService` | Full CRUD on `/projects/:id/risks`, plus `/stats` |
| `EmailTemplateService` | `GET /email-templates`, `PUT /email-templates/:id`, `GET /email-templates/logs` |
| `MailServerConfigService` | `GET /mail-server-config`, `PUT /mail-server-config` |
| `DataSyncService` | `GET /data-sync/jobs`, `GET /data-sync/jobs/:id`, `PUT /data-sync/jobs/:id`, `POST /data-sync/jobs/:id/run`, `POST /data-sync/jobs/:id/probe`, `GET /data-sync/jobs/:id/runs?take=` |
| `PermissionCatalogService` | Permission areas and actions catalog |
| `PermissionStateService` | `GET /user-permissions/me/effective`, `GET /users/me/org-units`; manages permission state, loads via APP_INITIALIZER |
| `RolePermissionService` | Role-level permission assignment |
| `UserPermissionService` | User permission overrides and effective permissions |
| `KpiService` | Full CRUD on `/kpis`, plus `/targets/bulk-upsert`, `/:id/targets`. Bulk upsert request now carries optional `Baseline`, `DataSource`, `CalculationNotes`, and `Direction` per target row (null preserves the existing value) |
| `StrategicObjectiveService` | Full CRUD on `/strategicobjectives`, plus `/by-orgunit/:id` |
| `StrategicPlanService` | Full CRUD on `/strategic-plans` plus `/by-orgunit/:id`, `/by-orgunit/:id/active`, and `PATCH /:id/restore` |
| `StrategicPlanWorkflowService` | Strategic plan approval workflow on `/strategic-plans/workflow` — `submit`, `requestRevision`, `review`, `getForPlan`, `getPendingReviews`, `getMyRequests`, `getPendingSummary`, `canReview`, `getLockState` (CR-001-D + CR-001-E) |
| `KpiActualsService` | CR-001-F. KPI actuals CRUD + workflow on `/kpis/actuals` and `/kpis/actuals/workflow` — `getGrid`, `listByKpi`, `getHistory`, `getById`, `create`, `update`, `delete`, `submit`, `review`, `getPendingReviews`, `getMyRequests`, `getPendingSummary`, `canReview` |
| `OrgUnitService` | Org unit CRUD and tree, plus org-unit head endpoints (`getHead` / `assignHead` / `removeHead` / `getPlanApprover`) for CR-001-H |
| `RoleManagementService` | Role listing, user assignment, permission matrix |
| `UserManagementService` | User CRUD, `/register-external`, `/register-ad`, `/search-employees`, role/org-unit assignment |
| `WorkflowService` | Full CRUD on `/action-items/workflow` (date/status change requests, review, escalate, give direction, pending summary) |
| `ProjectWorkflowService` | Project approval workflow: submit for approval, review, get pending/my requests, pending summary, can-review check |
| `WorkflowStateService` | Shared pending approval count (BehaviorSubject) — combines action item + project + strategic plan + **KPI actual** pending counts, refreshed on SignalR events and 60s interval. Catches 403 from `StrategicPlanWorkflowService.getPendingSummary()` and `KpiActualsService.getPendingSummary()` for users without the corresponding View permissions |
| `ThemeService` | Dark/light theme toggle (persisted to localStorage) |

---

## Models & Interfaces

### Core Models (in `core/models/`)
| Model | Key Fields |
|-------|-----------|
| `ActionItem` | id, actionId (ACT-001), title, description, workspaceId, projectId, milestoneId, isStandalone, priority, status, startDate, dueDate, progress (0-100), isEscalated, createdByUserId |
| `ActionStatus` (enum) | ToDo, InProgress, InReview, Done, Overdue, Deferred, Cancelled |
| `ActionPriority` (enum) | Low, Medium, High, Critical |
| `DashboardKpi` | totalActions, completionRate, onTimeRate, escalations, criticalHighCount, overdueCount |
| `dashboard-analytics.model.ts` | `ChartSlice`, `TeamMemberActivity`, `MyActionsDashboard`, `ActionItemsDashboard` (+ `ActionTrendPoint`, `WorkspaceActions`), `ProjectsDashboard` (+ `ProjectRow`, `MilestoneRow`, `ProjectHealth`), `StrategyDashboard` (+ `StrategyPlanRow`, `StrategyObjectiveRow`, `StrategyKpiRow`, `KpiHealth`) |
| `ActionItemFilter` (drill-down filters) | `openOnly`, `overdueOnly`, `isEscalated`, `dueBucket` (overdue / next7 / next30 / later), `ageBucket` (0-7 / 8-30 / 31-90 / 90+) — sent by `getAll` and `getMyActions` |
| `AuthResponse` | accessToken, refreshToken, user info |
| `CommentInfo` | id, content, authorUserId, isHighImportance, createdAt |
| `DocumentInfo` | id, name, fileName, contentType, fileSize, createdAt |
| `Notification` | id, userId, title, message, type, actionType, relatedEntityType, relatedEntityId, isRead, createdAt |
| `EmployeeProfile` | Employee profile data from KU directory |
| `ApiResponse<T>` | success, data, message |
| `PagedResult<T>` | items, totalCount, page, pageSize |

### Feature Models (in `models/` or feature folders)
| Model | Key Fields |
|-------|-----------|
| `Workspace` | id, title, organizationUnit, orgUnitId, **strategicPlanId**, **strategicPlanName**, isActive, admins, members, **projectManagers**, **strategicProjectApprovers** |
| `WorkspaceMember` | userId, userName, email, orgUnitName, addedAt |
| `WorkspaceProjectManager` | userId, userName, email, orgUnitName, addedAt — drives the workspace-scoped PM dropdown on the project form |
| `WorkspaceStrategicProjectApprover` | userId, userName, email, orgUnitName, **order**, addedAt — one entry per level in the workspace's sequential strategic-project approval chain |
| `StrategicPlanDropdownItem` | id, name, orgUnitId, orgUnitName, startYear, endYear, status |
| `WorkspaceSummary` | totalWorkspaces, activeWorkspaces, strategicProjects, operationalProjects |
| `Project` | id, projectCode (PRJ-YYYY-001), name, workspaceId, projectType, projectStatus, priority, projectManagerUserId, sponsors, budget, isBaselined |
| `ProjectType` (enum) | Operational, Strategic |
| `ProjectStatus` (enum) | Draft, Active, Suspended, Closed |
| `Milestone` | id, milestoneCode (MS-YYYY-001), name, projectId, sequenceOrder, status, completionPercentage, isDeadlineFixed, approverUserId, baseline dates |
| `MilestoneStatus` (enum) | NotStarted, InProgress, AtRisk, Completed |
| `ProjectRisk` | id, riskCode (RISK-001), title, probabilityScore, impactScore, riskScore, riskRating, status, mitigationPlan, contingencyPlan |
| `RiskRating` (enum) | Critical, High, Medium, Low |
| `RiskStatus` (enum) | Open, Mitigated, Closed |
| `EmailTemplate` | id, templateKey, name, subject, htmlBody, isActive |
| `MailServerConfig` / `UpdateMailServerConfig` | host, port, useSsl, fromEmail, fromName, username, hasPassword (read) / password (write — null keeps existing) |
| `DataSyncJob` / `UpdateDataSyncJob` | id, jobKey, name, description, cronExpression, timeZoneId, isEnabled, scheduleDescription, nextRunAt, isRunning, lastRun* summary, sourceSummary, targetSummary, hasHandler / write: cronExpression, timeZoneId, isEnabled |
| `DataSyncJobRun` | id, jobId, startedAt, finishedAt, durationMs, status (`Running`/`Succeeded`/`Failed`), trigger (`Scheduled`/`Manual`), triggeredBy, rowsRead, rowsWritten, message |
| `DataSyncProbeResult` | sourceReachable/RowCount/Error, targetReachable/RowCount/Error, sourceOnlyColumns, targetOnlyColumns, commonColumnCount, elapsedMs |
| `CRON_PRESETS` / `TIME_ZONE_OPTIONS` | UI presets for the schedule editor (15 min, 30 min, hourly, 6 h, daily 02:00 / 06:00, custom; UTC / Gulf Standard Time) |
| `PermissionArea` (enum) | Dashboard, Workspaces, Projects, Milestones, ActionItems, StrategicObjectives, KPIs, Reports, OrgChart, UserManagement, PermissionsManagement, Roles, EmailTemplates, Notifications, Risks, **StrategicPlans**, **MailServerConfig** |
| `PermissionAction` (enum) | View, Create, Edit, Delete, Approve, Export, Assign, **AssignHead** |
| `Kpi` | id, kpiNumber, name, calculationMethod, period, unit, strategicObjectiveId |
| `KpiTarget` | id, kpiId, year, month, target, actual, notes, **baseline**, **dataSource**, **calculationNotes**, **direction** (KpiDirection) |
| `KpiDirection` (enum) | HigherIsBetter (0), LowerIsBetter (1), OnTarget (2) |
| `KpiActual` (CR-001-F) | id, kpiTargetId, kpiId, kpiName, orgUnitId, periodType, periodIndex, year, periodLabel, actualValue, targetValue, achievementPercent, notes, status, submittedBy*, assignedReviewer*, reviewedBy*, reviewerComment, documentCount |
| `KpiActualStatus` (enum) | Draft, PendingApproval, Approved, Rejected |
| `KpiActualPeriodType` (enum) | Quarter (0), SemiAnnual (1), Annual (2) |
| `KpiActualGridResponse` | kpiId, kpiTargetId, kpiName, periodType, year, rows[] (`{ periodLabel, periodIndex, targetValue, direction, current?, periodEndUtc, periodHasEnded }`) |
| `StrategicObjective` | id, objectiveCode, statement, description, orgUnitId |
| `StrategicPlan` | id, name, orgUnitId, startYear, durationYears, endYear, status, approvedAtUtc, approvedByUserId, objectiveCount |
| `StrategicPlanStatus` (enum) | Draft (0), PendingApproval (1), Approved (2), Archived (3) |
| `OrgUnit` | id, name, code, level, parentId, **headUserId**, **headDisplayName**, **headEmail** |
| `OrgUnitHead` (CR-001-H) | orgUnitId, orgUnitName, orgUnitLevel, userId, displayName, email |
| `OrgUnitPlanApprover` (CR-001-H) | userId, displayName, email, sourceOrgUnitId, sourceOrgUnitLevel, sourceOrgUnitName |
| `WorkflowRequest` | id, actionItemId, actionItemCode, actionItemTitle, requestType, status, requester/reviewer info, date/status change details, reason, reviewComment |
| `WorkflowRequestSummary` | pendingDateChanges, pendingStatusChanges, totalPending |
| `WorkflowRequestType` (enum) | DateChangeRequest, StatusChangeRequest |
| `WorkflowRequestStatus` (enum) | Pending, Approved, Rejected |
| `ProjectApprovalRequest` | id, projectId, projectCode, projectName, requester/reviewer info, status, reason, reviewComment, timestamps |
| `ProjectApprovalSummary` | pendingProjectApprovals |
| `ProjectApprovalStatus` (enum) | Pending, Approved, Rejected |
| `SubmitProjectApprovalRequest` | projectId, reason |
| `ReviewProjectApprovalRequest` | requestId, isApproved, reviewComment |
| `StrategicPlanApprovalRequest` (CR-001-D) | id, strategicPlanId, planName, orgUnitId, orgUnitName, requestType (Initial/Revision), status (Pending/Approved/Rejected), requester/reviewer info, justification, reviewerComment, timestamps |
| `StrategicPlanApprovalSummary` | pendingPlanApprovals |
| `StrategicPlanApprovalStatus` (enum) | Pending, Approved, Rejected |
| `StrategicPlanApprovalRequestType` (enum) | Initial, Revision |
| `StrategicPlanLockState` (CR-001-E) | planId, status, isLocked, pendingRevisionId |
| `SubmitStrategicPlanApprovalRequest` | planId, justification |
| `RequestStrategicPlanRevision` | planId, justification |
| `ReviewStrategicPlanApprovalRequest` | requestId, isApproved, reviewComment |

---

## Key Features

### Dashboards
- **Dashboard hub:** Overview, Action Items, Projects and Strategy dashboards behind one tab bar. Every stat card, chart segment, legend entry and list row drills down — to a drawer listing the records behind the figure, or to the deeper dashboard for that area — and every record links to its own page. Chart callbacks run through `NgZone.run()` because Chart.js fires them outside Angular's zone
- **Management Dashboard:** Team workload with completion %, at-risk items with severity, status breakdown (doughnut chart), critical actions list. Auto-refreshes every 30 seconds

### Action Items
- Create standalone or linked to project/milestone
- Multiple assignees per action item
- Status workflow: ToDo -> InProgress -> InReview -> Done (+ Overdue auto-processing, Deferred, Cancelled via approval)
- Priority levels: Low, Medium, High, Critical
- Progress tracking (0-100%)
- Escalation with reason tracking
- Threaded comments with high-importance flag
- File attachments (upload, download, delete)
- "My Actions" and "Created by Me" sections with stats cards
- Create/edit via right-side offcanvas panel
- Show-deleted toggle for viewing soft-deleted items
- Mobile-responsive card layout
- Excel export and PDF print with filters
- Soft delete and restore

### Projects
- Types: Strategic (linked to strategic objectives) and Operational
- Status workflow: Draft -> Active -> Suspended -> Closed
- Project Manager and multiple Sponsors assignment
- Budget tracking (AED currency)
- Baseline date snapshots for variance tracking
- Interactive Gantt chart with milestone bars, status-colored action item bars, and hover tooltips
- Milestones with sequence ordering, fixed/flexible deadlines, completion %, approver sign-off
- Inline action items per milestone in project detail
- Milestone validation: block activation when milestones/items incomplete; validate on status transitions to Active/Completed
- Excel export and PDF print

### Risk Register
- Per-project risk management
- Auto-generated risk codes (RISK-001 per project)
- Probability x Impact scoring (1-5 scale, risk score 1-25)
- Risk ratings: Critical, High, Medium, Low
- Mitigation and contingency planning
- Risk owner assignment
- Stats summary

### Workspaces
- Organizational container scoped to org unit (Level-2 ancestor)
- Multiple admin users per workspace — picked via custom searchable multi-select dropdown that filters across name, email, and org-unit name; each option renders display name with email/org-unit subtitle, and selected admins appear as removable chips
- Admin users dropdown lazy-loaded on first drawer open; org units eagerly loaded so the toolbar filter is responsive immediately
- Stats: project count, milestone count, action item counts
- Create/edit via offcanvas side drawer (consolidated into the list page)
- Org-unit filter dropdown (sorted by org-unit level)
- Soft delete and restore
- Excel export and PDF print

### Notifications
- In-app notification center
- Grouped by date (Today, Yesterday, This Week, Earlier)
- Filter by type: ActionItem, Project, Milestone, Workspace, Risk, Kpi, StrategicObjective, System
- Filter by read/unread status
- Mark as read (individual + mark all)
- Delete individual or batch delete read notifications
- Header bell icon with unread count
- Paginated with "load more"

### Admin Panel
- **Org Chart:** Hierarchical org unit management (up to 10 levels)
- **Strategic Objectives:** Tied to org units, auto-generated codes (SO-001); reads/writes scope-filtered to the user's org-unit subtree for Strategy Editors
- **KPIs:** Tied to strategic objectives, period-aware targets (12 / 4 / 2 / 1 rows/year), bulk upsert, calculation methods
- **KPI Target Evidence:** modal-driven document attachments per target row — up to 10 files, PDF/DOC/DOCX/XLS/XLSX/PPT/PPTX, 10 MB max; paperclip badge shows current file count
- **Email Templates:** Template management with subject/HTML body editing, active/inactive toggle, delivery logs
- **Mail Server Configuration:** Admin-managed SMTP settings (host, port, SSL/TLS, from, username, password) persisted in the database and used for all outgoing emails; password is stored encrypted via ASP.NET Core Data Protection and never returned to the client (a `hasPassword` flag is shown); leaving the password field blank on save keeps the existing stored value
- **Data Sync:** Admin Panel page for Hangfire-scheduled sync jobs (currently the HR `ku_employee_info` copy from KU_BOT_PROD). Per job: frequency presets or raw cron, time zone, enable/disable, next-run display, Run now, Test connection with column diff, and run history; auto-refreshes while a run is in progress. Gated by `DataSync.View` / `DataSync.Edit`

### Permission System
- **15 Permission Areas** x **7 Permission Actions** = 105 possible permissions
- Role-based permission matrix editor
- User-level permission overrides (grant or deny)
- Org unit scoping for data isolation
- Dynamic header navigation based on permissions
- Route guards enforce permission checks
- Permissions pre-loaded via APP_INITIALIZER on hard refresh
- Cleared on logout to prevent stale state

### Role & User Management
- List all roles with user counts (4-card-per-row layout)
- Assign/remove users from roles
- Assign org units to individual users
- Register AD users (search KU employee directory)
- Register external/local users with email/password
- Paginated user list with search and inline editing
- **Strategy Editor role guard:** the backend rejects assignment unless the user has an active org unit at level 2 or deeper; UI surfaces the validation error inline

### Export & Reporting
- Excel export (.xlsx) on all major pages (actions, workspaces, projects, milestones)
- PDF print (browser Ctrl+P friendly layouts)
- CSV export via reports page with filters
- Reports dashboard with bar and doughnut charts (Chart.js)

### Action Item Workflow
- **Date freeze:** Standalone action item dates are locked after creation; editing requires a date change request with justification
- **Status change approval:** Terminal status transitions (Done, Deferred, Cancelled) on standalone items require approval; ToDo → InProgress is direct
- **My Approvals page:** Two tabs (Pending Reviews, My Requests), stat cards, approve/reject with comments
- **Reviewers:** Action item creator + direct manager(s) of assignees (resolved via KuEmployeeInfo)
- **Escalation workflow:** Notifies creator + managers, direction can be given as high-importance comment
- **Real-time notifications:** 8 workflow-specific toast types (info/success/warning/error), clickable to navigate
- **Pending count badge:** Header nav shows pending approval count, refreshes every 60s and on SignalR events
- **Shared review dialog:** Reusable WorkflowReviewDialogComponent used in My Approvals and Action Detail
- **WorkflowStateService:** Shared pending count state (BehaviorSubject), cleared on logout

### Charts
- Doughnut charts: Status breakdown
- Bar charts: Team workload, completion rates, category/priority breakdown
- Gantt chart: Project timelines with milestones and action items
- Chart.js is registered per chart component — `providers: [provideCharts(withDefaultRegisterables())]` on `TeamDashboardComponent`, `ActionsDashboardComponent`, `ProjectsDashboardComponent`, `StrategyDashboardComponent`, `ManagementDashboardComponent`, `ReportsComponent` (keeps chart.js in their lazy chunks). **A new chart component must add the same provider** or its canvas throws `"category" is not a registered scale` and renders blank
- Chart colors resolve CSS theme variables at runtime (`cssVar()` helper — Chart.js canvas cannot read `var()` strings) and re-render on `ThemeService.currentTheme$` changes (Team Dashboard, Management Dashboard, Reports). Status series: To Do `#94A3B8`, In Progress `#4D8BFF` (logo blue), In Review `#8B6CF6`, Done `#10B981`, Overdue `#EF4444`
- Chart containers need a fixed height and must not flex/grow with their content (Chart.js sizes the canvas from the parent — a stretching parent causes a resize loop)

### Theming System
- Toggled via `data-theme="dark" | "light"` attribute on `<html>` (`ThemeService`, persisted to localStorage key `ku-theme`, restored pre-paint by an inline script in `index.html`); each theme also sets `color-scheme` so native controls and scrollbars follow
- **Single source of truth:** `src/assets/styles/_theme-variables.scss` (short names: `--canvas --bg --sf --sfh --el --inp --bd --th --t1..t3 --accent --ar ...`). `_variables.scss` only *aliases* the legacy long names onto them (`--bg-card: var(--sf)`, `--primary: var(--accent)` …) — never put raw colour values there
- **Palette sampled from the KUmmand Center logo** (brand constants on `:root`, identical in both themes): `--brand-navy #0B1B4A`, `--brand-royal #2A5CF0`, `--brand-royal-deep #1E46D2`, `--brand-blue #4D8BFF`, `--brand-sky #7AADFF` (+ `-r` RGB channels), `--brand-grad` / `--brand-grad-h` (sky → blue → royal)
- **Dark** (default): canvas `#1B1E25`, opaque raised surface `--bg` / header `--nav` = `#262A33` (the dark logo's background), translucent blue-tinted cards, accent `#659CFF`, headings `--th #F5F7FC`
- **Light**: canvas `#F3F6FC`, white surfaces and header (the light logo's background), navy-tinted keylines and shadows, accent `#2A5CF0`, headings `--th #0B1B4A`, AA-contrast text (`--t3` ≥ 4.5:1)
- Accent roles: `--accent` is for text / icons / borders / tints; **filled controls use `--accent-fill` (royal gradient) with `--on-fill` (white)**, hover `--accent-fill-h`. `--on-accent` remains for text on a flat `--accent` fill (navy in dark, white in light)
- Semantic tokens (both themes): status palette `--ok --warn --err --info --teal` + RGB channels `--okr --warnr --errr --infor --tealr` for `rgba(var(--*r), α)` tints (light values darkened for AA); elevation `--csh` (cards), `--neon` / `--neon2` (historical names — now a restrained brand-tinted hover lift, not a glow), `--sh-panel` (offcanvas) / `--sh-modal` (dialogs) / `--sh-drop` (dropdowns/toasts); overlay scrim `--ov`
- Layout tokens: `--hdr-h` (header height), `--page-max` (content column cap), `--M` (outer gutter)
- Bootstrap bridge: `--bs-body-color`, `--bs-body-bg`, `--bs-border-color`, `--bs-link-color`, `--bs-primary(-rgb)` etc. point at the theme tokens
- Global partials (`src/assets/styles/`, loaded in this order by `styles.scss`): `_theme-variables` → `_global-resets` (typography, scrollbars) → `_background` (static brand backdrop: royal/sky washes + pixel clusters; no animation) → `_components` (cards, buttons, soft tinted badges, alerts, tabs, pagination, progress) → `_forms` (inputs, selects, switches, range, input groups) → `_charts` → `_variables` (aliases) → `_overrides` (toasts, modals, offcanvas, dropdowns, tooltips, tables, utility classes, focus ring, print). The old `_rails.scss` (animated side rails) was removed
- **Icon tiles — one treatment app-wide:** `src/assets/styles/_tile.scss` exports `icon-tile($size)` (white glyph on the brand gradient with a lighter back card, as on the page header) and `icon-tile-danger` (the only colour variant — red, for cards that flag a problem: critical / escalated / overdue). Component SCSS imports it with a relative `@use '…/assets/styles/tile'` and applies it to a **wrapper** element around the `<i class="bi …">` (the mixin needs `::before`, which Bootstrap Icons uses for the glyph). Used by `PageHeaderComponent`, `KpiCardComponent`, all stat cards (Dashboard, My Actions, My Projects, My Approvals, Workspaces list/detail, Strategy shell/page/objective, Project and Milestone detail), detail-page headers, Reports section + metric icons, Admin Panel cards, Data Sync job cards, notification icons (page + bell) and Profile section titles. New icon tiles must use the mixin rather than their own colours
- Bootstrap utilities are remapped: `.text-primary/-success/-danger/-warning/-info/-muted` → theme tokens; `.badge.bg-*` → soft tinted chips; `.bg-primary` → royal, `.bg-info` → brand blue (non-badge uses such as progress bars)
- Component SCSS must use tokens — never hardcode status colors (`#22c55e`-family), `color: #000` on accent fills, or `rgba(0,0,0,x)` panel shadows. When restyling a `<select>`, set `background-color` (not the `background` shorthand, which wipes the caret image)

---

## Environment Configuration

| Key | Value |
|-----|-------|
| API Base URL | `https://localhost:7135/api` |
| Azure AD Client ID | Configured in `environment.ts` |
| Azure AD Authority | `https://login.microsoftonline.com/{tenantId}` |
| MSAL Redirect URI | `http://localhost:4200/auth_fallback` |
| MSAL Scopes | `api://{clientId}/access_as_user` |
| Cache Location | localStorage |

---

## Dependencies

### Production
| Package | Version |
|---------|---------|
| @angular/core | ^20.3.0 |
| @angular/animations | ^20.3.16 |
| @azure/msal-angular | ^3.1.0 |
| @azure/msal-browser | ^3.30.0 |
| @microsoft/signalr | ^10.0.0 |
| @ng-select/ng-select | ^21.8.2 |
| @popperjs/core | ^2.11.8 |
| bootstrap | ^5.3.3 |
| bootstrap-icons | ^1.13.1 |
| chart.js | ^4.4.7 |
| ng2-charts | ^6.0.1 |
| ngx-toastr | ^19.0.0 |
| date-fns | ^4.1.0 |
| xlsx | ^0.18.5 |
| rxjs | ~7.8.0 |
| zone.js | ~0.15.0 |

### Dev
| Package | Version |
|---------|---------|
| @angular/cli | ^20.3.17 |
| typescript | ~5.9.2 |
| karma | ~6.4.0 |
| jasmine-core | ~5.9.0 |
