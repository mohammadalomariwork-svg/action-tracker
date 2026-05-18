# KU Action Tracker — Implementation Status

> Last updated: 2026-05-18 (workspace-scoped assignable-users for all user pickers; legacy unscoped endpoint removed)

---

## 1. Authentication & Authorization

### Azure AD SSO
- Login via Microsoft Azure AD (MSAL) with federated token exchange
- Local login fallback with email/password
- JWT access tokens with refresh token rotation
- Automatic user provisioning from Azure AD on first login

### Permission System
- **PermissionArea / PermissionAction / AreaActionMapping** catalog model
- **Role-based permissions** — matrix UI to assign permissions per role
- **User-level overrides** — grant or deny individual permissions on top of role defaults
- **Effective permission resolution** — merges role + user overrides at runtime
- **Angular `*hasPermission` directive** — conditionally renders UI elements
- **PermissionStateService** — caches effective permissions and visible org units for the session; clears on logout
- **Backend policy-based authorization** on every endpoint (e.g. `ActionItemsView`, `ProjectsEdit`)
- **Org-unit scoping** — users only see data belonging to their assigned org unit hierarchy
- **Strategic scope service (`IStrategicScopeService`)** — applies role-based filtering to Strategic Objectives, KPIs, and KPI Targets so StrategyEditor users see/manage only their level-2 ancestor org unit and its descendants

### Application Roles (single source of truth: `AppRoles` constants)
- **Admin** — full system access, unrestricted
- **Workspace Admin** — workspace-scoped admin: creates workspaces, manages users in their org unit
- **PMO Head** — cross-portfolio visibility, approves charters, sets standards
- **PMO Analyst** — portfolio reporting, data entry support, no approval authority
- **Project Sponsor** — approves charter and closure, monitors project health
- **Project Manager** — full project management: artifacts, team, reports
- **Project Coordinator** — supports PM with data entry, scheduling, documents
- **Team Member** — assigned action items, log progress, raise issues
- **Strategy Editor** — edits strategic objectives and KPIs in user's level-2 ancestor org unit + descendants only; cannot be assigned to a user whose org unit is at level 1
- **Viewer** — legacy read-only role
- **Deprecated:** Manager and User — auto-deleted by `RoleSeeder` on startup if still present

### Policies Implemented
Actions, Projects, Milestones, KPIs, Strategic Objectives, Org Chart, Workspaces, User Management, Roles, Permissions Management, Reports, Dashboard, Notifications, Email Templates, Risks

---

## 2. Organizational Structure

### Org Units
- Hierarchical org chart up to 10 levels deep
- Tree view and flat paged list in admin UI
- CRUD with soft-delete and restore
- Cascade soft-delete for child units
- Users assigned to org units for data scoping
- **Org unit head (CR-001-H):** optional `HeadUserId` (FK → `AspNetUsers`, `OnDelete: SetNull`) per unit. Heads at level 3 are resolved by `IApprovalAuthorityResolver` as the strategic-plan approver for their subtree. Manage via `GET/PUT/DELETE /api/orgunits/{id}/head` (gated by `OrgChart.AssignHead`, granted to Admin and PMO Head). The level-3 head preview for a given unit is exposed via `GET /api/orgunits/{id}/plan-approver`.
- **Org-unit member management (admin UI):** in the Organization Chart page, each node now has a "Members" button (gated by `OrgChart.Edit`) that opens a side panel listing current members (with remove) and a typeahead picker to add users. Backed by `GET /api/orgunits/{id}/users` plus the existing `PUT /api/users/{id}/assign-org-unit`. Reassigning a user already in a different unit prompts a move-confirmation since `ApplicationUser.OrgUnitId` is single-valued.
- **Bulk-add workspace members from an org unit:** on the Workspace Settings page, the Members section has an org-unit picker + "Add org unit users" button (next to the existing per-user picker). One server-side call enrolls every active user assigned to the chosen unit, skipping anyone who is already a member. Backed by `POST /api/workspaces/{id}/members/from-org-unit` (body `{ orgUnitId }`), gated by `Workspaces.Edit`. The returned summary drives the toast: "Added N member(s) (M already members, skipped)".

### Strategic Objectives
- Linked to org units
- Auto-generated `ObjectiveCode`
- CRUD with soft-delete/restore
- Referenced by strategic projects and KPIs
- Optional FK to a parent **Strategic Plan** (`StrategicPlanId`, nullable during the migration window)

### Strategic Plans (CR-001-A, CR-001-C, CR-001-B)
- Time-bounded wrapper around strategic objectives, scoped to a single org unit
- Fields: `Name`, `OrgUnitId`, `StartYear` (2000–2100), `DurationYears` (1–10), computed `EndYear`
- **Statuses:** Draft, PendingApproval, Approved, Archived (workflow itself wired in CR-001-D)
- **Uniqueness rule:** at most one active (`Approved` or `PendingApproval`) plan per org unit, enforced by a filtered unique index
- **Delete-while-children block:** soft-deleting a plan is rejected when any non-deleted objective still references it
- **Backfill migration:** existing `StrategicObjective` rows are linked to a "{OrgUnit} — Legacy Strategy" plan that is created on migration; `StrategicPlanId` stays nullable for now and will be tightened in a follow-up migration
- **Plan creation gated** to **Admin** and **PMO Head** only via `StrategicPlans.Create`. Strategy Editors get `View + Edit` (no Create / Delete / Approve)
- On creation, every Strategy Editor whose `OrgUnitId` falls within the plan's org-unit subtree receives an in-app + email notification (`StrategicPlan.Initiated` template), plus a self-notification to the creator
- **Strategy home (`/strategy`)** lists existing strategic plans across the user's visible org units as a card grid (one card per in-flight plan). Cards show name, cycle, objective count, status badge, and org-unit chip; clicking a card opens its detail page (`/strategy/org-units/:orgUnitId`). Toolbar has a search box; summary stats show total / approved / pending / draft counts. Empty state shown when no plans exist in scope.
- **"New Strategy" button** in the strategy home page header (gated by `StrategicPlans.Create`) opens the plan-form dialog with an org-unit selector restricted to units the user can access that have no in-flight plan; disabled when every in-scope unit already has an active plan
- Per-unit detail page (`/strategy/org-units/:orgUnitId`) uses the shared `app-breadcrumb` (`Strategy → {OrgUnit}`) and `app-page-header`, with a 4-up summary row (Plan Status, Strategic Objectives, KPIs, Projects), a themed plan-detail card (cycle, approver preview, org-unit code), and theme-aligned objectives + approval-history sections. Action buttons (Submit for Approval / Edit / Archive / Request Revision) sit in the page-header right slot. The "Projects" stat is sourced from `GET /api/workspaces/summary?orgUnitId=…` (sum of strategic + operational), so it reflects the same scope the workspaces page uses.
- **Frontend `StrategicPlanStatus` enum aligned with backend serialization** — the API uses `JsonStringEnumConverter(CamelCase)` so the TypeScript enum now uses camelCase string values (`'draft' | 'pendingApproval' | 'approved' | 'archived'`) instead of numeric values. This fixes the strategy detail page showing an "UNKNOWN" status badge for plans whose status arrived as a string from the API
- **Strategic objective detail page** at `/strategy/objectives/:objectiveId` — opened via the "View objective" link on the strategy detail page. Shows the objective's statement / description / org unit, a 3-up summary (KPIs, Targets in current year, Objective code), and a card per KPI with its current-year targets in a period-aware table (Monthly / Quarterly / Semi-Annual / Yearly slots). Each KPI card shows code, name, period badge, unit, YTD achievement % (good/warn/bad colour-coded), and a "Manage targets" button that opens the existing `KpiTargetsComponent` editor in a right-side offcanvas drawer (consistent with the New Workspace / New Project drawer pattern). The full-page route `/admin/kpis/:kpiId/targets` is still available for direct deep-linking
- **Manage Targets drawer is plan-aware** — `GET /api/strategicobjectives/:id` now returns parent strategic-plan context (`strategicPlanId`, `strategicPlanStatus`, `planStartYear`, `planEndYear`, `isPlanLocked`); `ObjectiveDetailComponent` threads these into `<app-kpi-targets>` via new `[locked]` and `[planStartYear]`/`[planEndYear]` inputs. The year picker clamps navigation to the plan's cycle and hides both chevrons when the plan spans a single year. The targets table now collapses into stacked label/value cards below 576 px so the full row data is readable on phones without horizontal scroll
- **Field-level lock on Approved plans** — when the parent plan is Approved (no in-flight Revision) or Archived, the drawer renders a softened banner clarifying that **Target** values are read-only but **Actual**, **Notes**, and **Evidence** can still be updated (per CR-001-E §3 + CR-001-F §4.5: actuals are ongoing reporting, not target planning). The Edit button stays enabled, the Target cell renders as a lock-decorated read-only span instead of an input even in edit mode, the Save All button stays visible (relabeled "Save Actuals & Notes"). Backend: `IStrategicPlanLockGuard` gained a non-throwing `IsLockedForOrgUnitAsync` companion to `EnsureNotLockedForOrgUnitAsync`, and `KpiService.BulkUpsertTargetsAsync` now uses it to preserve Target / Baseline / DataSource / CalculationNotes / Direction on locked plans while accepting Actual + Notes; attempting to add new month rows or change a Target value still throws "Plan is approved and locked"
- **Strategy page stat cards are now drill-down entry points** — the Strategic Objectives and KPIs cards smooth-scroll to the in-page `#sp-objectives-section` (each row carries `kpiCount` + a "View objective" link, so KPIs surface one click deeper); the Projects card navigates to `/workspaces?orgUnitId=<id>` and `WorkspaceListComponent` now reads the `orgUnitId` query param on init and pre-applies the toolbar filter so the user lands on the projects scoped to that unit. Plan Status remains informational (not clickable). Cards rendered as `<button>` with a hover lift, focus outline, and a directional arrow chip (down for in-page scroll, up-right for cross-page navigation)
- **Pagination on strategy-page sections** — the Strategic Objectives, KPIs, and Strategic Projects in-page lists each now paginate at **5 rows per page** with a Prev / "Page X of Y · N total" / Next footer. Paging is client-side over the already-loaded plan-scoped collections, so navigating pages doesn't re-fetch. Pages reset to 1 whenever the underlying data reloads (route param change, plan switch, refresh)
- **In-page KPIs section on the strategy page** — the KPIs stat card no longer shares a scroll target with Strategic Objectives; it now smooth-scrolls to a new `#sp-kpis-section` that lists every KPI under the plan's objectives (objective code + KPI number, name, period pill, unit, parent objective statement, target count, with a "View objective" link to `/strategy/objectives/:objectiveId`). Loaded by fanning out `GET /api/kpis/by-objective/:objectiveId` per filtered objective and merging the results, sorted by objective code then `KpiNumber`. Empty states cover "no objectives yet" and "no KPIs yet"
- **In-page Strategic Projects section on the strategy page** — the Projects stat card is relabeled **Strategic Projects** and now smooth-scrolls to a new `#sp-projects-section` instead of leaving the page. The section lists every project whose `StrategicObjectiveId` matches one of the current plan's objectives (project code + name + status pill + linked objective + PM, with a "View project" link to `/projects/:id`). Server-side filter `strategicObjectiveIds` (repeated query param) added to `ProjectFilterDto` / `ProjectService.GetAllAsync` so the UI fetches only the relevant rows. Empty states cover "no objectives yet" and "no linked projects yet"
- **Inline objective creation on the strategy page** — Draft plans now show an **"Add Strategic Objective"** button in the in-page objectives section header (gated by `StrategicObjectives.Create`). Clicking it opens a right-side offcanvas hosting the existing `ObjectiveFormComponent` pre-filled with the page's `orgUnitId`. On save, the plan-scoped objective list refreshes (and KPIs/projects re-derive) so the new entry appears immediately. Hidden for `PendingApproval` / `Approved` / `Archived` plans
- **Auto-link new objectives to the active plan** — `StrategicObjectiveService.CreateAsync` now resolves the org unit's newest non-archived plan and assigns its id to `StrategicObjective.StrategicPlanId`. Without this the new row was invisible everywhere that filters by plan id (per-unit Strategy page via `GetByPlanAsync`, plan card `ObjectiveCount` on the Strategy home page). Objectives created against an org unit with no active plan still get a null `StrategicPlanId` (legacy admin-flow behaviour preserved)
- **Inline KPI creation on the objective detail page** — Draft plans now show an **"Add KPI"** button in the "KPIs & targets" section header on `/strategy/objectives/:objectiveId` (gated by `KPIs.Create` and the parent plan's `strategicPlanStatus === Draft`). Clicking it opens a narrower right-side offcanvas (520 px) hosting `KpiFormComponent` with `preselectedObjectiveId` bound to the page's objective. After save, the page reloads its objective + KPIs + targets and **automatically opens the existing "Manage targets" drawer for the newly-created KPI** so the user can immediately fill in target values without an extra navigation
- **KPI form objective dropdown is locked when called from the strategy view** — `KpiFormComponent` now has a `restrictToObjectiveId` input. When set (Strategy / Objective Detail flow) it fetches **only that one objective** via `GET /api/strategicobjectives/{id}` instead of the full list, renders it as the sole `<option>`, and disables the form control. The admin → KPIs page is unchanged (still uses the open-list `preselectedObjectiveId` pre-fill)
- **Inline-objective form is now subtree-scoped + plan-bound** — when opened from the Strategy page (Draft plan), `ObjectiveFormComponent` receives `restrictToOrgUnitSubtree` (the page's `orgUnitId`) and `strategicPlanId` (the active plan's id). The Org Unit dropdown is filtered client-side via a parentId BFS to **the strategy's unit + descendants only**, defaulted to the root, so a user creating an objective from HR's strategy can land it on HR or any sub-unit of HR but never on an unrelated branch. The form forwards `strategicPlanId` in `CreateStrategicObjectiveRequest` so even a sub-unit objective stays attached to the parent plan instead of falling back to `StrategicPlanId = null`. Backend `CreateStrategicObjectiveRequestDto` accepts `StrategicPlanId`; `StrategicObjectiveService.CreateAsync` validates the plan exists, isn't archived, isn't locked, and the requested `OrgUnitId` is inside the plan's subtree (via `IOrgUnitScopeResolver.GetDescendantOrgUnitIdsAsync`) — then attaches directly. The auto-resolve path is preserved for callers that don't pass an explicit plan id (admin / legacy flow)
- **Multi-level sequential strategy approval (per-plan approval chain)**:
  - **New entity** `StrategicPlanApprover` (PlanId, UserId, Order; unique on `(PlanId, Order)` + `(PlanId, UserId)`) holds the ordered chain configured per plan. `StrategicPlan.Approvers` nav added.
  - **`StrategicPlanApprovalRequest` extended** with `Level` (1-based position) + `TotalLevels` (chain-length snapshot at submit time). Defaults of `1`/`1` mean every existing row reads as a single-level chain — legacy plans + admin/PMO flows continue to work without back-fill scripts.
  - **New endpoints** `GET /api/strategic-plans/{id}/approvers` (view, scope-checked) + `PUT /api/strategic-plans/{id}/approvers` (replace-the-chain upsert; rejected on any non-Draft status). Server-side normalises gaps and duplicates and validates that every supplied user id maps to an active user.
  - **Workflow service** (`StrategicPlanWorkflowService.SubmitAsync` / `RequestRevisionAsync`) consults the chain at submit time. With approvers configured, it creates a single Pending row for level 1 only and stamps `TotalLevels = chain.Count`. With **no chain configured**, it falls back to the existing `IApprovalAuthorityResolver` (level-3 head) — full backwards compatibility.
  - **`ReviewAsync` now advances levels**: an *approval* at level N where N < TotalLevels marks the current row Approved, creates a new Pending row for level N+1 assigned to the next chain user, and leaves plan-level state alone (plan stays PendingApproval). The final-level approval finalises the plan as before. Rejection at any level rolls the plan back to Draft (existing behaviour); subsequent levels never get a chance. Each level transition fires both the existing "Reviewed" notification *and* a fresh "Submitted"-style notification to the next approver.
  - **Frontend**: new `ManageApproversDialogComponent` (right-side dialog, design-tokenised) with a search-and-pick approver picker (reuses `WorkspaceService.getActiveUsersForDropdown()`), up/down arrows for reorder, × to remove, save replaces the chain wholesale. Strategy page surfaces a **Manage approvers** button next to **Edit plan** (Draft only, gated by `StrategicPlans.Edit`) and renders an **Approval chain** section showing each level's status (Approved / Reviewing / Upcoming) by reading the latest Pending request from history. Approval history table grew a **Level** column showing `L{n} / {total}` for multi-level chains and `—` for legacy single-level rows.
  - **Migration** `Add_StrategicPlanApprovers_And_RequestLevel`: adds two non-null int columns (default 1) on `StrategicPlanApprovalRequests`, creates `StrategicPlanApprovers` with the two unique indexes. Non-destructive — the migration must be regenerated cleanly with the .NET host stopped (the running host locked the API DLL during `dotnet ef migrations add`); then `dotnet ef database update` applies it without data loss.
- **Inline edit for Strategic Objectives + KPIs (Draft plans only)**:
  - On the Strategy page, each objective row now has a small **Edit** button (gated by `canAddObjective` + `StrategicObjectives.Edit`). Clicking it opens the same right-side offcanvas as Add, with `existingObjective` bound — pre-fills statement / description / org unit, applies the strategy-scoped subtree filter (the form's BFS narrow now runs in edit mode too, with the existing record's unit kept in scope as a defensive fallback for legacy data), and re-uses the same toast / refresh logic. New shared computed `canEditObjective = canAddObjective` so future divergence has a clear seam
  - On the Objective Detail page, an **Edit objective** button sits next to the page header (Draft only) and opens an `ObjectiveFormComponent` in a narrower offcanvas; the existing KPI offcanvas is reused for both Add and Edit by binding `existingKpi`. Each KPI card grew a ghost-style **Edit** button next to "Manage targets" (gated by `KPIs.Edit`). After a KPI *create* the page still auto-opens the targets drawer; after an *edit* it just refreshes the cards, since the user's intent there is metadata-only
- **Edit Strategy dialog restyled + supports org-unit re-assignment**:
  - The dialog now uses the project's design tokens (`--el`, `--bd`, `--t1`, `--t2`, `--t3`, `--inp`, `--inpf`, `--bdh`, `--accent`) instead of hardcoded light-grey fallbacks (`--surface, #fff` / `--border, #e5e7eb` / `--text-muted, #6b7280`), so labels / inputs / borders / buttons read correctly in both dark and light themes — matches the visual language of `ObjectiveFormComponent` and `KpiFormComponent`
  - `showOrgUnitPicker()` now renders the org-unit picker whenever options are passed, regardless of mode. `StrategyPageComponent.openEdit()` lazy-loads the user's visible org units, filters out units that already host another non-archived plan (keeping the plan's *current* unit selectable), and passes them to the dialog
  - Frontend: `UpdateStrategicPlanRequest` accepts `orgUnitId?: string`; the strategy page only sends it when the user actually picked a different unit, then redirects to `/strategy/org-units/<new>` so the URL doesn't go stale
  - Backend: `UpdateStrategicPlanRequestDto` accepts `OrgUnitId`. `StrategicPlanService.UpdateAsync` re-assigns the plan only when **plan.Status == Draft**, the user has write scope on the destination, and no other non-archived plan owns it. Linked `StrategicObjective.OrgUnitId` rows are cascaded to the new unit in the same `SaveChangesAsync` call so plan + objective references stay aligned
- **Strategy page is now strictly scoped to the *selected* plan** — the per-unit page (`/strategy/org-units/:orgUnitId`) used to display every objective in the org unit regardless of which strategic plan they belonged to; it now loads objectives via the new server-side endpoint **`GET /api/strategicobjectives/by-plan/{planId}`** (`IStrategicObjectiveService.GetByPlanAsync`) so scoping is enforced in the database, not in the client. The Strategic Objectives list, the KPIs count + section, and the Strategic Projects section all derive from those plan-scoped objective ids. The page now loads in two phases: (1) org unit + plan list + workspace summary in parallel, then (2) once the active plan is resolved, objectives → projects + KPIs. The Strategic Projects section shows **strategic-type projects only** (`projectType=strategic`; operational projects are excluded) in **all statuses** (Draft / PendingApproval / Active / OnHold / Completed / Cancelled) linked to one of the plan's objectives — soft-deleted projects are excluded by the existing server-side query filter

---

## 3. Workspaces

- Container for projects and action items, associated with an org unit
- Workspace admins assigned per workspace via custom searchable multi-select dropdown — searches name, email, and org unit; chip-style display for selected admins; ng-select theme CSS bundled
- Active-users dropdown (`/api/workspaces/active-users`) returns `{Id, DisplayName, Email, OrgUnitName}` so the picker can disambiguate users by email or department
- Summary stats (project counts, open action items)
- **Workspace-membership access control** — `IWorkspaceAccessResolver` returns the workspaces visible to a user: privileged roles (Admin / PMO Head / PMO Analyst) see everything; everyone else only sees workspaces where they are an admin or member. Replaces the prior org-unit scoping for `GET /api/workspaces`, `GET /api/projects`, and `GET /api/action-items` (My Actions / Created By Me still bypass the filter).
- **Workspace members (`WorkspaceMember`)** — separate from `WorkspaceAdmin`, lets workspace admins assign regular users via `GET/POST/DELETE /api/workspaces/{id}/members`. Members get read access to the workspace's projects, milestones, and action items without needing org-unit privileges. Members are added through a searchable multi-select picker (one POST per user in parallel) on the **Workspace Settings page**.
- **Workspace Settings page (`/workspaces/:id/settings`)** — dedicated home for managing Admins, Members, **Project Managers**, **Strategic Project Approvers** (ordered chain with up/down reorder), and the **Linked Strategic Plan**. Replaces the inline editors that used to live on the workspace detail page; the detail page now shows a read-only access summary + a "Configure Workspace" button gated by `Workspaces.Edit`.
- **Workspace Project Managers (`WorkspaceProjectManager`)** — workspace-scoped whitelist of users eligible to be picked as Project Manager. Managed via `GET/POST/DELETE /api/workspaces/{id}/project-managers`. The PM dropdown on the project form (both the workspace-detail drawer and the standalone `/projects/new` form) pulls from this list. Backend rejects project create/edit when the chosen PM isn't on the workspace's list. Adding a user to the PM list also auto-grants them the `Project Manager` Identity role (no-op if they already have it).
- **Strategic Project Approvers (`WorkspaceStrategicProjectApprover`)** — workspace-scoped sequential approval chain for strategic projects. Managed via `GET/POST/DELETE /api/workspaces/{id}/strategic-project-approvers` and `PUT .../reorder`. On strategic-project submit, a single `ProjectApprovalRequest` is created at level 1 (mirrors the strategic-plan workflow); each Approve advances to the next chain entry and the project only goes Active when the final level approves. Rejection at any level sends the project back to Draft. Operational projects bypass the chain entirely — their submit short-circuits Draft → Active with a self-approved historical request stamped "Auto-approved: operational project (PM authority)."
- **Workspace ↔ Strategic Plan link** — optional `Workspace.StrategicPlanId` set via `PUT /api/workspaces/{id}/strategy`. When set, the project form's strategic-objective dropdown is restricted to objectives from that plan; when not set, no strategic objectives are available (strategic projects in that workspace are disabled).
- Search, sort, pagination, org-unit filter dropdown
- Archive/restore (soft-delete)
- Export to Excel and Print to PDF
- Create/edit via right-side offcanvas drawer (consolidated into list page — no longer routes to a separate form)
- Workspace detail page has dedicated Members and Linked Strategic Plan sections (admin-only edit, gated by `Workspaces.Edit`)

---

## 4. Projects

### Project Management
- **Project types:** Strategic and Operational
- **Statuses:** Draft, Active, On Hold, Completed, Cancelled, **Pending Approval**
- Auto-generated `ProjectCode` (PRJ-YYYY-NNN)
- Fields: budget (AED), priority, sponsors, project manager, planned/actual dates
- Linked to workspace and (optionally) a strategic objective
- My Projects page with role-based filtering
- Activation validation: blocks if milestones or action items are missing
- Soft-delete cascades to milestones and action items

### Milestones
- Nested under projects, ordered by sequence
- Auto-generated `MilestoneCode` (MS-YYYY-NNN)
- **Statuses:** NotStarted, InProgress, AtRisk, Completed
- **Project phase** (mandatory): Initiation, Planning, Execution, Monitoring & Controlling, Closing
- Completion percentage tracking (0-100)
- Approver user for sign-off
- Baseline locking — saves planned dates as baseline for variance tracking
- Status transition validation (Active/Completed checks)
- Export to Excel and Print to PDF

### Gantt Chart
- Interactive Gantt visualization of milestones and action items
- Color-coded bars by status
- Hover tooltips with details
- Positioned below project details section

### Risk Register
- Per-project risk management
- Auto-generated `RiskCode` (RISK-NNN per project)
- Probability x Impact scoring (1-5 scale, risk score 1-25)
- **Risk ratings:** Critical (20-25), High (12-19), Medium (5-11), Low (1-4)
- **Statuses:** Open, Mitigated, Closed
- Mitigation plan, contingency plan, and notes fields
- Risk owner assignment
- Stats summary endpoint
- Soft-delete/restore

---

## 5. Action Items

- Core tracked entity across the system
- Auto-generated `ActionId` (ACT-NNN)
- **Statuses:** ToDo, InProgress, InReview, Done, Overdue, Deferred, Cancelled
- Priority levels, start/due dates, progress (0-100 auto-clamped)
- Can be standalone or linked to workspace/project/milestone
- Multi-user assignee support — the Assignee(s) picker on the action-item create/edit form (and the inline edit panel on the action-item detail page, the workspace-detail page's New Action Item drawer, and the My Actions list panel) is scoped to the chosen workspace's admins, members, and project managers via `GET /api/workspaces/{id}/assignable-users`, instead of every active user in the system. Cascade behavior: empty state shown until a workspace is picked; changing the workspace clears stale assignee selections and surfaces a toast prompting re-selection; already-assigned users who are no longer on the workspace's people lists stay visible in edit mode so they can be deselected. The same workspace-scoped endpoint also powers Sponsor pickers on project create/edit, Assignee pickers on milestone-detail and the milestone-section action create form, the Risk Owner dropdown on the risk form, and the legacy unscoped `/api/action-items/assignable-users` endpoint has been removed.
- Escalation tracking
- Created-by tracking (`CreatedByUserId`)
- **My Actions page:**
  - Scoped to current user (assigned + created by me sections)
  - 7 stat cards (total, by status, overdue, etc.)
  - Show-deleted toggle
  - Create/edit via right-side offcanvas panel
  - Mobile-responsive card layout
- Overdue processing endpoint (admin)
- Comments on action items
- Soft-delete/restore
- Export to Excel and Print to PDF

---

## 6. KPIs

- Linked to strategic objectives
- Auto-numbered per objective
- Measurement periods: Monthly, Quarterly, SemiAnnual, Yearly
- Calculation method and unit fields
- **Targets:** per-period target and actual values with notes
- Period-aware target rows: Monthly = 12 rows, Quarterly = Mar/Jun/Sep/Dec, SemiAnnual = Jun/Dec, Yearly = Dec
- Bulk upsert for monthly targets by year
- CRUD with soft-delete/restore
- **KPI Target evidence:** up to 10 documents (PDF/DOC/DOCX/XLS/XLSX/PPT/PPTX, max 10 MB each) attachable per target via the `KpiTarget` polymorphic entity type
- Inline-editable target rows with year navigation, achievement % colour coding, variance calculation, and Save All bulk action
- All reads/writes scoped through `IStrategicScopeService` — StrategyEditor users limited to their org-unit subtree
- **Per-target details (CR-001-G):** each `KpiTarget` carries optional `Baseline` (decimal), `DataSource` (≤ 500 chars), `CalculationNotes` (≤ 2000 chars), and a non-nullable `Direction` enum (`HigherIsBetter` / `LowerIsBetter` / `OnTarget`); existing rows backfill to `HigherIsBetter`. Bulk upsert uses null-means-no-change semantics so partial edits do not wipe existing details

---

## 7. Dashboards

### Team Dashboard
- Personal action items due this week
- Recent activity feed
- KPI metrics (completion rate, on-time delivery)
- Status breakdown (horizontal bar chart via Chart.js)
- Workspace summary statistics
- 6 equal-height stat cards

### Management Dashboard
- Executive-level view (Admin/Manager roles only)
- Auto-refresh every 30 seconds
- Team workload distribution (horizontal bar chart)
- At-risk items with severity levels
- Critical action items
- Status breakdown (doughnut chart)
- Organization-wide KPI metrics

---

## 8. Reports & Export

### Reports Page
- Global KPI metrics: total actions, completion rate, on-time delivery, escalations, overdue count
- Action items breakdown by category and priority (bar + doughnut charts)
- Team member performance statistics
- Filterable by status, priority, assignee, date range
- CSV export with applied filters

### Export Capabilities (across features)
- **Export to Excel (XLSX):** Workspaces, projects, milestones, my actions
- **Print to PDF:** Workspaces, projects, milestones, my actions

---

## 9. Comments & Documents

### Comments
- Generic comment system (polymorphic: works on any entity type)
- High-importance flag
- Author tracking with timestamps
- Also available as action-item-specific comments

### Documents
- Generic document attachment (polymorphic: any entity type)
- File upload with 10 MB limit, restricted MIME types: PDF, DOC, DOCX, XLS, XLSX, PPT, PPTX
- Download endpoint
- Tracks uploader, file size, content type
- **Per-entity-type cap:** `KpiTarget` is limited to 10 documents per target (extensible via `MaxDocumentsPerEntity` lookup)
- **Scope-aware authorization:** when uploading/deleting a document attached to a `KpiTarget`, the document service calls `IStrategicScopeService.EnsureCanWriteAsync` against the target's parent strategic objective's org unit

---

## 10. Notifications

- In-app notification system
- Grouped by date (Today, Yesterday, This Week, Earlier)
- Filter by type: ActionItem, Project, Milestone, Workspace, Risk, Kpi, StrategicObjective, System
- Filter by read/unread status
- Mark as read (individual + mark all)
- Delete individual or batch delete read notifications
- Unread count endpoint for header bell icon
- Paginated with "load more"

---

## 11. User Management

- Paginated, sortable user list with search
- Inline role and org-unit assignment
- **Registration flows:**
  - Azure AD user registration (auto-synced from directory)
  - External/local user registration (username/password)
- User activation/deactivation
- KU employee directory search integration
- Profile page showing display name, email, login provider, roles
- **Strategy Editor role validation:** assignment is rejected unless the target user has an active org unit at level 2 or deeper — protects against accidentally granting the role tree-wide scope

---

## 12. Role Management

- Role CRUD with inline creation dialog
- Role-specific user listing and assignment
- Permission matrix per role
- Prevents deletion of roles with active users
- 4-card-per-row layout with paginated user lists

---

## 13. Email Templates

- System-level email template management
- Template key-based lookup
- Editable subject and HTML body
- Active/inactive toggle
- Delivery logging for audit

---

## 14. UI / UX

- **Theme:** Fluent / Palantir design aesthetic
- **Dark and light mode** toggle (persisted)
- **Login page** with KU branding and animated particle background
- **Header:** Dynamic nav with permission-based link visibility, theme toggle, notification bell, user menu, mobile hamburger drawer
- **Footer** with KU logo
- **Loading bar** on route transitions
- **Browser title:** KU Action Tracker with KU favicon
- **Responsive:** Mobile card layouts for actions and lists
- **Standalone components** throughout (Angular best practice)
- **Angular signals** for state management

---

## 15. Action Item Workflow

### Date Freeze
- After creation, start/due dates on standalone action items are locked
- Editing dates requires submitting a date change request with justification
- Lock icon (🔒) displayed on frozen date fields in the UI
- Non-standalone (project/milestone-linked) items are unaffected

### Status Change Approval
- Moving standalone items to terminal statuses (Done, Deferred, Cancelled) requires approval
- Direct transition ToDo → InProgress is allowed without workflow
- Two new statuses added: Deferred and Cancelled
- Status change requests include justification reason

### Approval Flow
- **My Approvals page** (`/approvals`) with two tabs: Pending Reviews and My Requests
- Stat cards showing pending date/status change counts
- Reviewers: action item creator + direct manager of assignee(s)
- Manager lookup via KuEmployeeInfo (supervisor email/name)
- Approve with optional comment; reject with required comment
- Approved changes are applied automatically (dates updated, status changed)

### Escalation Workflow
- Escalating an action item notifies creator + direct manager(s) of assignees
- Creator/manager can give direction (creates high-importance comment)
- Direction notification sent to all assignees

### Workflow Notifications
- 8 notification action types: DateChangeRequested/Approved/Rejected, StatusChangeRequested/Approved/Rejected, ActionItemEscalated, EscalationDirectionGiven
- In-app notifications via AppNotification + SignalR real-time push
- Email notifications using 5 new templates (DateChangeRequested, DateChangeReviewed, StatusChangeRequested, StatusChangeReviewed, DirectionGiven)
- Workflow-specific toast popups (info/success/warning/error by event type)
- Clickable toasts navigate to relevant page
- Header badge showing pending approval count (refreshes every 60s + on SignalR events)
- Deduplication: same user in multiple roles receives one notification per event

### Shared Components
- Reusable WorkflowReviewDialog component (used in My Approvals and Action Detail)
- WorkflowStateService for shared pending count state
- Workflow request history section in action form and detail views

---

## 16. Project Approval Workflow

### Project Creation Notifications
- On project creation, email + in-app notification sent to direct line manager and all project sponsors

### Draft Phase & Submit for Approval
- Project manager can add/edit milestones and action items while project is in Draft
- "Submit for Approval" button on project detail (visible to PM when status is Draft)
- Opens modal for justification reason
- Project status transitions from Draft → PendingApproval
- Notification sent to all sponsors + PM's direct line manager

### Approval Gate
- Any single sponsor or the PM's direct line manager can approve/reject
- Approve: project transitions to Active, ActualStartDate set, IsBaselined set, milestone dates baselined
- Reject: project returns to Draft with reviewer comment
- All other pending requests auto-closed on approval

### Date Freeze (Post-Approval)
- Once project leaves Draft status, the following dates become read-only:
  - Project: PlannedStartDate, PlannedEndDate
  - Milestones: PlannedStartDate, PlannedDueDate
  - Action items (linked to project): StartDate, DueDate
- New milestones and action items cannot be added to non-Draft projects
- Milestones and action items cannot be deleted from non-Draft projects
- Backend enforcement via service-level guards + frontend UI enforcement (disabled inputs, lock icons, hidden buttons)

### Approval Notifications
- Email + in-app notification to the project manager with reviewer's decision and comment
- `ProjectApproval.Requested` and `ProjectApproval.Reviewed` email templates

### UI Components
- "Submit for Approval" button and justification modal on project detail page
- "Pending Approval" banner with inline approve/reject buttons for reviewers
- Approval History section (collapsible table) on project detail page
- "Project Approvals" tab on My Approvals page with pending reviews and submitted requests
- Review modal for approve/reject with comment
- Header badge includes project approval count in total
- "Create New Project" button on My Projects page (permission-gated)
- PendingApproval status badge (amber) throughout project list and detail views
- Date inputs locked with disabled state when project is not in Draft

### Entity Model
- `ProjectApprovalRequest` entity with FK to Project
- `ProjectApprovalStatus` enum: Pending, Approved, Rejected
- `PendingApproval` added to `ProjectStatus` enum

### API Endpoints (ProjectWorkflowController)
- `POST /api/projects/workflow/submit` — submit for approval
- `PUT /api/projects/workflow/requests/{id}/review` — approve or reject
- `GET /api/projects/workflow/project/{id}` — get approval requests for a project
- `GET /api/projects/workflow/pending-reviews` — get pending reviews for current user
- `GET /api/projects/workflow/my-requests` — get user's submitted requests
- `GET /api/projects/workflow/pending-summary` — pending count for header badge
- `GET /api/projects/workflow/can-review/{id}` — check if user can review a project

---

## 17. Strategy / KPI Org-Unit Scoping

### IStrategicScopeService
- Resolves visible org-unit IDs for the current user using their roles + `IOrgUnitScopeResolver`
- **Read-unrestricted roles (CR-001-I):** Admin, PMO Head, PMO Analyst, Project Manager, Workspace Admin all return `null` (full visibility) so they can browse plans, objectives, and KPIs across every org unit
- **StrategyEditor** → restricted to user's level-2 ancestor org unit + descendants
- StrategyEditor with no `OrgUnitId` returns an empty visibility list (sees nothing)
- `EnsureCanWriteAsync` throws `UnauthorizedAccessException` when the user attempts to write outside scope. Read-unrestricted PMO/PM roles holding only `View` permissions still get rejected by the standard `*.Create` / `*.Edit` / `*.Delete` policies, so cross-unit visibility never grants write access

### Scope-Enforced Endpoints
- `GET /api/strategicobjectives` — list filtered to visible org units
- `GET /api/strategicobjectives/{id}` — returns null when out of scope (does not leak existence)
- `GET /api/strategicobjectives/by-orgunit/{id}` — empty list when org unit out of scope
- `POST/PUT/DELETE/RESTORE /api/strategicobjectives` — write scope enforced on both source and target org unit (no out-of-scope reassignment)
- `GET /api/kpis`, `GET /api/kpis/{id}`, `GET /api/kpis/by-objective/{id}` — filter by parent objective's org unit
- `POST/PUT/DELETE/RESTORE /api/kpis` — write scope enforced via the parent objective's org unit
- `POST /api/kpis/targets/upsert` and `POST /api/kpis/targets/bulk-upsert` — scope enforced via parent KPI → objective chain
- `POST /api/documents` for `entityType=KpiTarget` — write scope enforced via the target's parent KPI → objective chain
- `GET /api/strategic-plans` (paged), `/{id}`, `/by-orgunit/{orgUnitId}`, `/by-orgunit/{orgUnitId}/active` — same scope rules as objectives
- `POST/PUT/DELETE/PATCH /api/strategic-plans` — write scope enforced; uniqueness conflict (active plan exists) returns 409; delete-while-children returns 409
- Controller error mapping: `UnauthorizedAccessException` → 403 Forbidden, `KeyNotFoundException` → 404, `ArgumentException` → 400, `InvalidOperationException` (uniqueness, delete-with-children) → 409 Conflict

---

## 18. Strategic Plan Approval Workflow (CR-001-D + CR-001-E + CR-001-H)

### Approval flow
- **Entity:** `StrategicPlanApprovalRequest` (mirrors `ProjectApprovalRequest`) with `RequestType` (`Initial` / `Revision`), `Status` (`Pending` / `Approved` / `Rejected`), `AssignedReviewerUserId`, `ReviewedByUserId`, `Justification`, `ReviewerComment`
- **Submit:** Strategy Editor (or any user with `StrategicPlans.Edit` in scope) on a `Draft` plan → plan transitions to `PendingApproval`. Submission requires at least one objective and at least one KPI per objective; otherwise the API returns 400.
- **Reviewer resolution:** `IApprovalAuthorityResolver` walks the org-unit chain to the closest level-3 ancestor and uses its `HeadUserId`. If no level-3 head is configured the submission fails with a 409 — there is no PMO Head fallback.
- **Review:** assigned reviewer (or Admin) approves/rejects via `PUT /api/strategic-plans/workflow/requests/{id}/review`. Comment is required on rejection (≥ 1 char, ≤ 2000). Approve transitions plan to `Approved` with `ApprovedAtUtc` and `ApprovedByUserId`. Reject sends the plan back to `Draft`.
- **Notifications:** in-app + email to the assigned reviewer (`StrategicPlan.Submitted` template), in-app cc to all PMO Heads, self-notification for the submitter, in-app + email to the submitter on review (`StrategicPlan.Reviewed` template).
- **Race protection:** reviewing a request whose plan status no longer matches an in-flight Initial returns 409. Reviewing an already-reviewed request returns 400.

### Edit lockdown (CR-001-E)
- **`IStrategicPlanLockGuard`:** centralised lock guard. `EnsureNotLockedAsync(planId)` and `EnsureNotLockedForOrgUnitAsync(orgUnitId)` are sprinkled across mutation handlers in `StrategicPlanService`, `StrategicObjectiveService`, and `KpiService` (Create/Update/Delete + bulk upsert).
- **Lock semantics:** `Approved` plan with no in-flight Revision → locked (HTTP 409 with the standard "plan-locked" message on any mutation). `Approved` + pending `Revision` → temporarily unlocked. `Archived` → always locked, no escape hatch.
- **Lock state endpoint:** `GET /api/strategic-plans/workflow/lock-state/{planId}` returns `{ planId, status, isLocked, pendingRevisionId }` so the frontend can cache the lock state without inferring it from status alone.

### Revision cycle
- **`POST /api/strategic-plans/workflow/request-revision`** opens a `RequestType=Revision` request against an `Approved` plan (gated by `StrategicPlans.Edit`). One revision in flight at a time (409 on duplicate).
- **Plan status unchanged** while a revision is pending — the lock guard reads the request to decide whether edits are allowed.
- **Approve a Revision** keeps the plan at `Approved` and re-locks it. **Reject a Revision** keeps the plan locked with the comment recorded.

### My Approvals integration
- New **Strategy Approvals** tab on the My Approvals page with sub-sections for "Pending Strategy Reviews" and "My Strategy Requests". Reuses the inline review modal pattern from project approvals; reject requires a comment.
- Header pending-approval badge aggregator (`WorkflowStateService.refreshPendingCount`) now sums action item + project + strategic plan pending counts. The strategic-plan summary call swallows 403s for users without `StrategicPlans.View`.
- **Strategy page** gains a "Submit for Approval" button (Draft only), a pending-approval banner, an approved-and-locked banner with a "Request Revision" button, an approver preview line ("Approver: {Name} (head of {OrgUnitName})"), and an Approval History table below the plan content.

### Permissions
- `StrategicPlans.Approve` policy already exists. The actual reviewer check is the `AssignedReviewerUserId` match (or Admin escape hatch); `StrategicPlans.View` is sufficient for the review endpoint to be reachable.
- New `OrgChart.AssignHead` permission added to the catalog and seeded for Admin + PMO Head.
- Frontend `PermissionAction` enum extended with `AssignHead = 7`.

---

## 19. KPI Actuals & Evidence (CR-001-F)

### Entity & schema
- **Entity:** `KpiActual` — captures a measured value for a single reporting period of a `KpiTarget`. Acts as its own workflow record (Status / Submitted* / Reviewed* fields live directly on the entity).
- **Period model:** `KpiActualPeriodType` enum (`Quarter`, `SemiAnnual`, `Annual`). The accepted period type for a given KPI is derived from the KPI's `MeasurementPeriod`; Monthly KPIs roll up to Quarterly for actuals reporting.
- **Status enum:** `KpiActualStatus` — `Draft → PendingApproval → Approved | Rejected`.
- **Uniqueness:** filtered unique index on `(KpiTargetId, PeriodType, PeriodIndex, Year)` where `IsDeleted = 0 AND Status <> Rejected`. Rejected actuals are kept in history; a new draft can be created for the same slot.
- **CHECK constraint** on `(PeriodType, PeriodIndex)` enforces valid index ranges (Q1..Q4, H1..H2, Annual=1).
- **Migration:** `Add_KpiActual` — additive; no `KpiTarget.actual` backfill is required because the column never existed in this codebase.

### Workflow
- **`IKpiActualWorkflowService.SubmitAsync`** — Strategy Editor submits a Draft. Validates the period has ended via `KpiPeriodCalendar.GetPeriodEndUtc` (Q7 — 400 if not yet ended). Resolves reviewer via `IApprovalAuthorityResolver` (the level-3 org-unit head). Transitions `Draft → PendingApproval`.
- **`ReviewAsync`** — assigned reviewer (or Admin) approves/rejects. Comment required on reject. Rejected actuals are final — submitter must create a new draft for that slot.
- **Notifications:** in-app + email to assigned reviewer (`KpiActual.Submitted` template), in-app cc to PMO Heads, self-notification for the submitter, in-app + email to the submitter on review (`KpiActual.Reviewed` template).
- **Plan lock interaction:** the workflow deliberately does **not** call `IStrategicPlanLockGuard` — actuals stay editable when the parent plan is `Approved`/locked.

### Period-aware grid (frontend)
- **Component:** `KpiActualsGridComponent` at `/admin/kpis/:kpiId/targets/:targetId/actuals`.
- **Rows:** Q1..Q4 / H1..H2 / Annual depending on the parent KPI's measurement period.
- **Per row:** Period label, target value (read-only), inline-editable actual + notes (only when Draft), evidence chip count + "Manage" button, status badge, Achievement % column (only when Approved; uses `KpiDirection` for direction-aware computation), and actions: Save Draft / Submit for Approval / Delete / History.
- **Period-end client guard:** the Submit button is disabled with a tooltip ("Available after {Label} ends on {Date}") when the period has not yet ended; server still enforces.
- **History modal:** lists all actuals (including rejected) for a single (target, period, year) slot.

### Evidence
- **Document cap:** `KpiActual` is capped at 10 documents (registered in `DocumentService.MaxDocumentsPerEntity`). Uploads beyond the cap return 409.
- **Scope-aware authorization:** `DocumentService.EnsureScopedWriteAsync` walks `KpiActual → KpiTarget → Kpi → StrategicObjective → OrgUnitId` and enforces `IStrategicScopeService.EnsureCanWriteAsync` when the entity type is `KpiActual`.

### My Approvals tab + header badge
- New **KPI Actuals** tab on the My Approvals page with "Pending KPI Actual Reviews" and "My KPI Actual Submissions" sub-sections. Reuses the inline review modal pattern; comment required on reject.
- `WorkflowStateService.refreshPendingCount` aggregator now sums action item + project + strategic plan + **KPI actual** pending counts. The KPI actuals summary call swallows 403s for users without `KpiActuals.View`.

### Permissions
- New area `KpiActuals` with actions `View`, `Create`, `Edit`, `Approve` (seeded by `PermissionCatalogSeeder`).
- Default role grants (`DefaultRolePermissionsSeeder`):
  - **Admin** — full (View/Create/Edit/Approve)
  - **PMO Head**, **PMO Analyst**, **Project Manager**, **Workspace Admin** — View only
  - **Strategy Editor** — View + Create + Edit (no Approve; reviewer authority is the `AssignedReviewerUserId` match)

### API endpoints
| Method | Endpoint | Permission |
|---|---|---|
| GET | `/api/kpis/actuals/by-kpi/{kpiId}?year=` | `KpiActuals.View` |
| GET | `/api/kpis/actuals/grid?kpiTargetId=&year=` | `KpiActuals.View` |
| GET | `/api/kpis/actuals/history?kpiTargetId=&periodType=&periodIndex=&year=` | `KpiActuals.View` |
| GET | `/api/kpis/actuals/{id}` | `KpiActuals.View` |
| POST | `/api/kpis/actuals` (create draft) | `KpiActuals.Create` |
| PUT | `/api/kpis/actuals/{id}` (edit draft) | `KpiActuals.Edit` |
| DELETE | `/api/kpis/actuals/{id}` (soft-delete draft) | `KpiActuals.Edit` |
| GET / POST / DELETE | `/api/kpis/actuals/{id}/documents[/{documentId}]` | View / Edit |
| POST | `/api/kpis/actuals/workflow/submit` | `KpiActuals.Create` |
| PUT | `/api/kpis/actuals/workflow/requests/{actualId}/review` | `KpiActuals.View` |
| GET | `/api/kpis/actuals/workflow/{pending-reviews \| my-requests \| pending-summary}` | `KpiActuals.View` |
| GET | `/api/kpis/actuals/workflow/can-review/{actualId}` | `KpiActuals.View` |

---

## 20. Developer Experience

- **Auto-launch Swagger UI on startup (Development only):** `Program.cs` registers a lifetime callback that opens the browser to `{baseUrl}/swagger` once the host is ready, regardless of how the app is launched (`dotnet run`, Visual Studio, VS Code). `launchSettings.json` `launchBrowser` set to `false` to avoid duplicate browser tabs
- **Angular CLI analytics disabled** in `angular.json`
- **ng-select default theme CSS** bundled in `angular.json` build/test asset lists

---

## 21. Not Yet Implemented

- [ ] Audit trail / activity log
- [ ] Advanced search (cross-entity, full-text)
- [ ] Scheduled reminders (automated due-date reminders)
