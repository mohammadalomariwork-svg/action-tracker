# KU Action Tracker — Implementation Status

> Last updated: 2026-11-24 (Planned and actual dates on action items — WO-2026-11 A-1. Previous: 2026-11-23 workspace calendar setting + working-day date rule — C-3; Admin Panel → Calendars page — C-2; shared confirm dialog clickable again; work calendars backend — C-1)

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
Actions, Projects, Milestones, KPIs, Strategic Objectives, Org Chart, Workspaces, User Management, Roles, Permissions Management, Reports, Dashboard, Notifications, Email Templates, Mail Server Config, Data Sync, Calendars, Risks

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
  - **Submitter cannot review their own plan (2026-05-19)**: `SubmitAsync` and `RequestRevisionAsync` now route through a shared `ResolveFirstApproverAsync` helper that rejects the request (400) when the submitter is anywhere in the configured chain (error names the offending levels) or when the level-3 fallback head resolves to the submitter. The mid-flight `ReviewAsync` advance also re-checks before creating the next-level Pending row, so an edited chain can't trap an in-flight workflow with the submitter as a downstream approver. No new endpoints; the rule is enforced at submit time and surfaces through the existing toast-on-error path in the UI.
- **Strategic objectives — delete guard for linked projects (2026-05-19)**: `DELETE /api/strategicobjectives/{id}` now returns **409 Conflict** with a `Cannot delete this strategic objective — N project(s) still link to it: …` message listing each offending project's `ProjectCode – Name`, instead of silently soft-deleting and leaving orphan `Project.StrategicObjectiveId` references. Soft-deleted projects don't count. UI behaviour unchanged — the existing toast already surfaces `err.error.message` verbatim — so users see the linked-project list and can open each project to unlink/reassign before retrying. **Update (CR-001-G)**: the same rule fires when a draft objective whose `OriginalObjectiveId` matches a still-linked published objective is deleted — the linked-project check follows the original FK, so removing an objective from a revision draft also requires unlinking projects first. The `PromoteDraftAsync` merge step re-runs the check as a belt-and-braces guard before soft-deleting any "removed in draft" objective.
- **Strategic objectives — collision-proof ObjectiveCode generation (2026-08-11)**: `CreateAsync` no longer derives the code from `COUNT(*)+1` (which raced under concurrent creates and, after any hard delete, could re-issue an existing code — the live DB sat at 17 rows with max code SO-18, so the next create would have failed on the unique index). Codes now come from a SQL Server sequence `dbo.ObjectiveCodeSeq` (`SELECT NEXT VALUE FOR` — atomic, no retries needed). Migration `Add_ObjectiveCodeSequence` creates the sequence idempotently, computing the start value at apply time from the highest existing `SO-n` code; existing rows/codes untouched. Applied to production 2026-08-11 (start value 19).
- **CR-001-G: Draft / revision versioning model for strategic plans (backend complete, 2026-05-19)**:
  - **Schema** — added nullable self-FKs `StrategicPlan.OriginalPlanId`, `StrategicObjective.OriginalObjectiveId`, `Kpi.OriginalKpiId`, `KpiTarget.OriginalKpiTargetId` (each `ON DELETE RESTRICT`). All four entities also gained a bool `IsDraft`. A filtered unique index `IX_StrategicPlans_OriginalPlanId WHERE [OriginalPlanId] IS NOT NULL` enforces ≤ 1 open draft per published plan. Three non-unique indexes back the new self-FK lookups on objectives / KPIs / targets. Migration `Add_StrategicPlan_Drafts` — additive, non-destructive.
  - **Fix (2026-05-19)** — `IX_StrategicPlans_OrgUnitId_ActiveUnique` filter now also requires `[IsDraft] = 0`. Without this, submitting a revision (draft `Status` transitions Draft → PendingApproval) collided with the published Approved parent in the same OrgUnit and triggered `SqlException: Cannot insert duplicate key row in object 'dbo.StrategicPlans'`. Migration `Filter_StrategicPlan_OrgUnitId_ActiveUnique_For_Drafts` rebuilds the index with filter `[IsDraft] = 0 AND [IsDeleted] = 0 AND [Status] IN (1, 2)` — non-destructive (index drop + recreate, no data loss).
  - **Global query filters** (AppDbContext) — `StrategicPlan`, `StrategicObjective`, `Kpi`, `KpiTarget` now also filter out `IsDraft = true` rows. Every "live" consumer (Strategy page, project linking, dashboards, KPI rollups, scope service) continues to see only published rows with zero code changes. Draft-aware services call `IgnoreQueryFilters()` explicitly.
  - **`IStrategicPlanDraftService`** (`OpenDraftAsync`, `GetCurrentDraftIdAsync`, `CancelDraftAsync`, `PromoteDraftAsync`) — clones the published plan + its full graph (objectives + KPIs + targets) into new rows tagged `IsDraft=true` with `Original*Id` back-pointers. `PromoteDraftAsync` walks the draft graph, applies field updates in place on each published row that has a draft counterpart (so `Project.StrategicObjectiveId` / `KpiActual.KpiTargetId` references stay valid), rebrands brand-new draft entries as published (`IsDraft=false`, FKs re-parented), and soft-deletes published rows no longer represented in the draft. The draft graph is hard-deleted at the end. Cancel hard-deletes everything; pending revision requests against the canceled draft are auto-rejected. **Documents on KpiTarget are not yet cloned into drafts** (deferred): targets cloned from published carry an `OriginalKpiTargetId` and on promote their docs stay with the published row.
  - **Revision workflow rewritten** — `RequestRevisionAsync` now requires a draft to exist first (`POST /api/strategic-plans/{id}/drafts` from the new endpoints). It validates that the caller is the draft's `CreatedBy`, then attaches the new approval request to the *draft* plan id and flips the draft's `Status` to `PendingApproval`. `ReviewAsync` distinguishes Initial (request points at published plan) vs Revision (request points at draft plan) — for Revision rejection it sends the draft back to `Draft`; on final-level approval it calls `PromoteDraftAsync` inside the same handler so the published plan reflects the merged content by the time the outcome notification fires. The mid-flight level-advance now looks up the chain on the published plan id (chain config lives on the published plan only — drafts inherit it).
  - **Lock guard semantics changed (CR-001-G)** — `StrategicPlanLockGuard` reads with `IgnoreQueryFilters()` so it can answer for draft rows. Published plans in `Approved` / `PendingApproval` / `Archived` are now **permanently locked** (no more "temporarily unlocked during revision" — drafts are the working copy). Draft plans are editable while `Status = Draft` and locked once submitted (`Status = PendingApproval`). The error message moved from "Submit a revision request to make changes" to "Open a revision draft to make changes."
  - **New endpoints** — `POST /api/strategic-plans/{id}/drafts` (open), `GET /api/strategic-plans/{id}/drafts/current` (peek), `DELETE /api/strategic-plans/drafts/{draftId}` (cancel). The existing edit endpoints (`PUT /api/strategic-plans/{id}`, `PUT /api/strategicobjectives/{id}`, `POST /api/strategicobjectives/`, etc.) were updated to call `IgnoreQueryFilters()` and skip the lock guard when the targeted entity is a draft. New objectives added under a draft plan inherit `IsDraft = true` automatically.
  - **`StrategicPlanDto` exposes** `isDraft: bool` + `originalPlanId: Guid?` so the UI can distinguish draft from published.
  - **Frontend integration (partial)** — `StrategicPlanWorkflowService` (frontend) has `openDraft`, `getCurrentDraft`, `cancelDraft`. The existing "Request Revision" dialog on the Strategy page now chains `openDraft` → `requestRevision` so the one-click UX still works end-to-end (creates a draft, submits it immediately). Status quo for users until PR 3 polish ships.
  - **Deferred to PR 3 (UI polish)**: dedicated draft editor route with intermediate edit step (open, edit, submit as 3 user actions), in-page Cancel-draft button, approval-chain progress panel (which approver currently has it, who has already approved, who is upcoming), explicit "switch to draft / published" toggle on the strategy page. Backend supports all of this — only the UI work is pending.
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
- **Private workspaces** — `Workspace.IsPrivate` flag, set via a "Private workspace" switch on the create/edit workspace form and list drawer. A private workspace and everything under it (projects, milestones, action items, project risks) is visible **only to that workspace's admins and members** — system Admins, PMO Head / PMO Analyst and every other user cannot see it, and its data is excluded from all of their calculations (workspace summary + per-workspace counts, dashboard KPIs / status breakdown / team workload / management dashboard, CSV reports, milestone stats, strategy roll-ups, Gantt, approvals lists). Enforced centrally by EF Core global query filters in `AppDbContext` keyed on the request user (`ICurrentUserService`); "include deleted" lists and restore endpoints re-apply the restriction via `WhereWorkspaceVisibleTo()`. System context (Hangfire jobs, seeders — no HTTP user) is unrestricted so reminders/escalations keep working. Private workspaces show a lock **Private** badge in the list and a **Visibility** row on the detail page; Excel exports include a Visibility column. Migration `AddWorkspaceIsPrivate` (additive `bit` column, default `false`).
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
- Priority levels, planned start / planned end (`StartDate` / `DueDate`), progress (0-100 auto-clamped)
- [x] **Planned and actual dates** (WO-2026-11 A-1) — `ActualStartDate` / `ActualEndDate` (nullable) next to the planned dates, which keep their columns and are now labelled "Planned start" / "Planned end" everywhere in the UI and in the DTO docs
  - Recorded automatically (Q9): the first move into In Progress sets the actual start when it is empty (also when PATCH `/status` stores a past-due In Progress as Overdue); the move into Done sets the actual end (or takes the value sent in the same PUT); leaving Done clears the actual end. An item that goes straight from To Do to Done keeps an empty actual start. "Today" is the calendar day in the workspace calendar's time zone (Asia/Dubai for KU Standard), stored at midnight like the planned dates; UTC when no calendar exists
  - Same rule on every status path: create, PUT, PATCH `/status`, an approved status-change request, `ApplyApprovedStatusChangeAsync` (shared `ActionItemActualDates`). The overdue job never touches them
  - Editable by anyone who may edit the item: not after today, actual end not before actual start, actual end only while Done (it cannot be cleared there), actual start can be cleared (`clearActualStartDate`). Refused values come back as 400 with the reason. Not subject to the planned-date freeze, the date-change requests or the working-day rule — a project item whose planned dates are frozen still takes actual dates
  - A Done item stays locked except for its actual dates: PUT with only `actualStartDate` / `actualEndDate` / `clearActualStartDate` is accepted, anything else still gets 422 "Completed action items cannot be edited." The detail page shows **Edit actual dates** for Done items (behind `ActionItems.Edit`)
  - Variance: `actualEndVarianceDays` = actual end − planned end in calendar days, shown on the detail page as "N days late" / "On time" / "N days early" and in the Gantt tooltip
  - Edit forms (action form, action detail, My Actions panel, milestone detail, workspace detail) have Actual start / Actual end fields; Excel exports (My Actions, milestone, workspace) carry all four dates
  - Migration `Add_ActionItem_ActualDates`: two nullable columns plus a backfill of `ActualEndDate` for existing Done items — the day of `UpdatedAt`, or of `CreatedAt` when `UpdatedAt` was never stamped (still 0001-01-01 on items created as Done). Verified first on throwaway LocalDB databases, then **applied to production on 2026-11-24**: 15 rows backfilled (10 from `UpdatedAt`, 5 from `CreatedAt`), row count and SHA-256 of the existing 18 columns unchanged, `ActualStartDate` NULL everywhere, no year-0001 dates
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

### Dashboard hub (`/dashboard`, tabs: Overview · Action Items · Projects · Strategy)
- **Everything drills down.** Stat cards, chart segments, legend entries and list rows open a right-hand drawer listing the records behind the figure; every record links to its own page. Action items are fetched page by page with server-side filters; projects, KPIs, objectives, plans and milestones are listed from rows the dashboard already holds, so a list always matches the segment that opened it.
- **Scoped numbers.** The new endpoints count only what the caller may see — workspace membership for action items and projects (`IWorkspaceAccessResolver`), strategic org-unit scope for strategy (`IStrategicScopeService`) — on top of the private-workspace query filters.
- **Overview** — greeting as the page heading in the top row (with the date, a New Action button and the dashboard tabs); portfolio cards (Admin) open Workspaces / the Projects dashboard; action cards (total, completion rate, on-time, escalations, overdue) open the matching item lists. **My Actions** is now charts: status doughnut, open-by-priority and open-by-due-date bars, plus open / overdue / due-in-7-days / completed pills. **Team Activity** lists each person's action-item count, busiest first, as stacked bars (completed / open / overdue); selecting a person lists their items. Recent Items table kept.
- **Action Items** (`/dashboard/actions`) — total, open, completed, overdue, escalated, completion rate, completed on time; status doughnut; created-vs-completed trend (8 weeks); open items by priority, due window and age; team workload and by-workspace ranked lists.
- **Projects** (`/dashboard/projects`) — total, active, completed, on track, at risk, delayed, average progress; health doughnut, by status, strategic vs operational, progress bands of projects under way, health by workspace; "needing attention first" table; late and upcoming milestones.
- **Strategy** (`/dashboard/strategy`, needs `StrategicPlans.View`) — year selector; plans, objectives, KPIs, average achievement, KPIs reporting, strategic projects, actuals awaiting approval; KPI health doughnut (on track ≥ 90%, at risk ≥ 60%, off track below); KPI health per objective; weakest-first KPI table; strategic-project delivery; plans table.
- **Definitions** (one place: `DashboardRules.cs`) — *open* = To Do / In Progress / In Review / Overdue; *overdue* = open and flagged Overdue or past its due date; *project progress* = share of its action items done (falls back to mean milestone completion; completed = 100%); *delayed* = active past planned end; *at risk* = active with overdue action items or late milestones; *KPI achievement* = approved actuals against targets, direction-aware, for the selected year.
- My Actions page accepts `?status=` / `?priority=` so a drill-down can open it pre-filtered.

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

### Mail Server Configuration
- Admin-managed SMTP settings (Host, Port, UseSSL, From email/name, Username, Password)
- Persisted in the `MailServerConfigs` table (single active row)
- Password encrypted at rest via ASP.NET Core Data Protection (`ISecretProtector`)
- `EmailSender` reads the active DB configuration at send time and falls back to `appsettings.json` `Smtp` section when no DB row exists
- Admin Panel → "Mail Server Configuration" card, gated by `MailServerConfig.View` / `MailServerConfig.Edit`
- Password field never returned by API; clients only see a `hasPassword` flag; leaving the password field blank on save keeps the existing stored value

### Data Sync (Hangfire background jobs)
- [x] Hangfire 1.8 (SQL Server storage, `HangFire` schema in the app DB, 2 workers) hosted inside the API; dashboard at `/hangfire` restricted to local requests
- [x] `DataSyncJobs` / `DataSyncJobRuns` tables — admin-editable cron, time zone, enabled flag; per-run history (status, trigger, rows, duration, message)
- [x] **HR Employee Info Sync** (`employee-info-sync`) — copies `ku_employee_info` from KU_BOT_PROD (KU1ICDDWV008) into Action Center. Full refresh: reads the source into memory, `DELETE` + `SqlBulkCopy` inside one transaction, common-column matching, row-count safety guard (aborts if source < 90% of target or empty). Default schedule: hourly
- [x] Source connection string lives in `DataSync:EmployeeInfo:SourceConnectionString` in the git-ignored `appsettings.json`
- [x] Admin Panel → "Data Sync" page: schedule presets + raw cron + time zone, enable/disable, Run now, Test connection (source/target reachability, row counts, column diff), run history, auto-refresh while running
- [x] `DataSync.View` / `DataSync.Edit` permissions (Admin role by default)
- [x] Startup seeds a job row per registered handler and pushes schedules to Hangfire; `Testing` environment uses a no-op scheduler so integration tests never open a Hangfire SQL connection
- [x] `ku_employee_info.NearestAirport` relaxed to NULL (raw-SQL migration `Relax_KuEmployeeInfo_NearestAirport_Nullable`) — the HR source allows NULL there and the first sync failed on it

### Work Calendars (WO-2026-11 C-1 backend + C-2 admin page + C-3 workspace setting and date rule)
- [x] Entities `WorkCalendar`, `WorkCalendarDay` (weekly pattern, always 7 rows), `WorkCalendarException` (holidays, special working days, reduced-hours periods; inclusive date range) and nullable `Workspace.WorkCalendarId` (`Restrict` FK; null = use the default calendar)
- [x] Additive migration `Add_WorkCalendars` — three new tables, one nullable column, indexes; no backfill, no existing column touched
- [x] Default calendar **"KU Standard"** (Monday–Friday 08:00–17:00, Saturday–Sunday off, `Asia/Dubai`) seeded at startup by `WorkCalendarSeeder` when no calendar exists — on a clean database and on an existing one
- [x] `IWorkCalendarService` — CRUD, set default, restore, usage, and the calculation API for other features: `GetForWorkspaceAsync(workspaceId)` / `GetDefaultAsync()` return a `ResolvedWorkCalendar` with `IsWorkingDay`, `NextWorkingDay`, `AddWorkingDays`, `WorkingDaysBetween` (exceptions override the weekly pattern); resolved calendars cached in memory and invalidated on every save
- [x] Rules: exactly one default calendar at all times (also enforced by a filtered unique index); the default calendar and any calendar a workspace uses cannot be deleted or deactivated (`409`); working day needs `StartTime < EndTime`; exception needs `EndDate ≥ StartDate`; name unique among non-deleted calendars
- [x] Admin API `/api/calendars` — list, get, create, update, soft delete, restore, `PUT /{id}/default`, `GET /{id}/usage` (names only workspaces the caller may see; private ones are reported as a count)
- [x] New permission area **`Calendars`** (View / Create / Edit / Delete) — Admin: all, Workspace Admin: View
- [x] Tests: calculation API (weekend, holiday range, special working day, year boundary, overlapping exceptions), service rules incl. the delete-in-use guard, seeder, endpoint policies, and a controller integration test (401 / 403 / 409)
- [x] **Admin Panel → Calendars page** `/admin/calendars` (WO-2026-11 C-2) — "Calendars" card on the Admin Panel; route gated by `Calendars.View`. Frontend only — it uses the C-1 API unchanged
  - List: name with Default / Inactive badges and description, working days ("Mon–Fri", runs wrap round the week), hours ("08:00–17:00" or "Varies by day"), time zone, exception count with the next upcoming one, and the workspaces using it (names the caller may see, a count of private ones, and for the default calendar the workspaces that inherit it). Summary strip, Refresh, "Show deleted" with Restore. Stacked cards below 768px
  - Actions behind `*appHasPermission`: New (`Calendars.Create`), Edit / Set default / Restore (`Calendars.Edit`), Delete (`Calendars.Delete`). Delete is disabled with the reason for the default calendar and for a calendar a workspace uses; Set default is disabled for an inactive calendar; the API's 409 message is shown on the page if it still refuses. Users with View only get a read-only View drawer
  - Editor drawer: **details** (name, description, time zone, active — the switch is locked with the reason on the default or an in-use calendar), **weekly pattern** (7 rows Monday first, working/off switch + start/end time), **exceptions** (table with add / edit / remove; off = holiday or closure, working = special working day or changed hours with optional times; dates inclusive), and a **month preview** that colours working days, off days, holidays and special days (each with an icon too) from the drawer's unsaved values, with month navigation, today marker and working-day count. Client checks mirror the server validator; server 400/409 messages are shown in the drawer. Unsaved changes ask before closing; Esc closes; focus moves into the drawer, stays inside it, and returns to the button that opened it
  - New calendars start as Monday–Friday 08:00–17:00, `Asia/Dubai`
  - The preview uses `shared/utils/work-calendar.util.ts`, a client copy of the `ResolvedWorkCalendar` rules (exceptions override the week; the most specific overlapping exception wins — order fixed in C-3, see below; off days carry no hours). Verified against the server: the spec `work-calendar.util.spec.ts` repeats the `ResolvedWorkCalendarTests` cases, a one-off comparison through `IWorkCalendarService` matched 517 days (Nov 2026 – Mar 2028) and 17 monthly counts of a calendar with overlapping, year-boundary and tie exceptions, and the page's own create request replayed through the API on the test host matched its preview for every day of December 2026
  - Checked in a headless browser against a mocked API holding the seeded "KU Standard" row: dark and light, 1440 and 390px, keyboard only, View-only user, and the create → add holiday → set default flow (70 checks)
- [x] **Workspace calendar setting + working-day date rule** (WO-2026-11 C-3) — no migration; uses `Workspaces.WorkCalendarId` from C-1 (null = the default calendar)
  - **Workspace Settings → Working Calendar** section: the calendar in effect (name, "Default" badge when inherited, working days, hours and time zone, next exception) for everyone who can open the page; for workspace managers with `Workspaces.Edit`, a dropdown "Use the default calendar (…)" plus every active calendar, and Save. A change applies to dates saved from then on — existing records are not re-checked or changed
  - API: `GET /api/workspaces/{id}/calendar?from=&to=` (any signed-in user, like the other form lookups; 404 for a private workspace the caller is not part of; default window one year back to three years ahead, at most 3660 days), `PUT /api/workspaces/{id}/calendar` (`Workspaces.Edit` + workspace access; 400 for an unknown, deleted or inactive calendar), `GET /api/workspaces/calendars` (`Workspaces.View`; active calendars for the dropdown)
  - **Server rule (Q7 / Q8)**: a create or update that creates or changes a start / end date on a non-working day of the workspace's calendar is refused with 400 and a message naming the date and the next working day — e.g. `Due date 2026-11-28 (Saturday) is not a working day in the workspace calendar "KU Standard". Next working day: 2026-11-30 (Monday).` Applies to action items (start, due), milestones (planned start, planned due), projects (planned start, planned end) and date-change requests. Dates are compared by calendar day; a date the record already holds is never checked, so legacy weekend dates keep saving
  - Approved date changes: checked when the request is made (`CreateDateChangeRequestAsync`); approving applies the request as it stands. Editing the dates of a standalone action item is checked before the edit is turned into a request, so a refused date is now reported instead of being dropped silently
  - Not checked: actual dates (milestone actual completion, project actual start), unchanged dates when an item moves to another workspace. Overdue and due-window counting (`DashboardRules`), the approval workflows and the date freeze are unchanged
  - **Forms**: every start / end date in the action item, milestone and project forms (action form + its date-change request, My Actions panel, action detail, Overview "New Action" drawer, milestone detail, milestone section, workspace detail, My Projects create / edit, project form, project detail — 15 date pairs) shows an inline warning with a **"Use <next working day>"** button and **"N working days"** under the pair, and refuses the save with the server's wording. Reactive forms also carry `workingDayValidator`. Taking the suggestion returns focus to the date field
  - **Fix: overlapping exceptions resolve deterministically** — two exceptions of the same length on the same date used to be decided by database order. Order now (server and client): shortest range; non-working before working; a working exception with hours of its own before one without; shorter working day; earlier start time; name (ordinal); earlier start date
  - Tests: server unit tests for the rule on all four services (refused Saturday, accepted on a six-day calendar, legacy weekend item still saves, standalone edit not swallowed, approval still applies), the workspace setting (inherited default, set / reset, inactive / deleted / unknown calendar, out-of-scope and private workspace, window) and the tie-break (each new case fails on the old ordering); integration tests on the test host (403 / 404 / 401 / 400, set then get, Saturday refused with the message and accepted on a six-day calendar, date-change request refused, Swagger lists the endpoints); client specs for the util tie-break, next / previous / add working days, and the validator
  - Checked in a headless browser against a mocked API: Working Calendar section and the action form's Saturday warning in dark and light, at 1440 and 375px, mouse and keyboard (109 checks)

---

## 14. UI / UX

- **Theme:** Fluent / Palantir design aesthetic, branded in the KUmmand Center logo palette
- **Brand redesign of the inner pages (2026-11)** — both themes now derive from the logo: navy `#0B1B4A`, royal `#2A5CF0`, blue `#4D8BFF`, sky `#7AADFF`, slate `#262A33`. Dark mode drops the neon cyan accent for logo blue (`--accent #659CFF`) on a deeper slate canvas; light mode uses navy ink and royal accent on a cool blue-tinted canvas. Filled controls use a royal gradient with white labels (`--accent-fill` / `--on-fill`). Shell: single-row 64px header on the exact logo background (the PNGs are not transparent) with brand-underlined active link, off-canvas drawer from 1440px down, slim footer, static brand backdrop (colour washes + pixel clusters) replacing the animated lines / orbs / side rails. Shared: page header with icon on a brand-gradient tile, breadcrumb with home glyph, KPI cards with variant icon tiles, soft tinted badges, tinted outline buttons, brand tabs / pagination / progress, themed form controls. Dashboard: calm greeting panel (surface card with a faint brand wash, the user's name in the logo blues) in place of a separate page header, stat cards in the logo blue with status colours reserved for Escalations / Overdue, Bootstrap icons instead of emoji, working status colours on the stacked bar. Verified with headless-browser captures of the main pages in dark and light at 1440 / 1100 / 390px.
- **One icon style app-wide (2026-11)** — every icon tile (page and detail headers, stat cards, KPI cards, section headers, Reports metrics, Admin Panel cards, Data Sync jobs, notifications, Profile sections) uses the shared `icon-tile` mixin in `assets/styles/_tile.scss`: white glyph on the brand gradient with a lighter card behind it. Decorative colour variants (teal / purple / orange / green tiles and card edges) were removed; red is the only other colour and only flags a problem (critical, escalated, overdue). Milestone-detail stat icons moved from emoji to Bootstrap icons.
- **Charts now render** — Chart.js controllers/scales were never registered, so every `<canvas baseChart>` threw `"category" is not a registered scale` and stayed blank; the three chart components now provide `provideCharts(withDefaultRegisterables())`. Reports chart areas are fixed-height (they previously had no bound) and its chart options are theme-aware.
- **Dark and light mode** toggle (persisted)
- **Redesigned light theme (2026-07)** — solid white surfaces on a soft blue-grey aurora canvas, visible keyline borders, layered neutral shadows instead of neon glows, AA-contrast text tokens, accent aligned to the KUmmand logo blue (`#2563EB`). New theme tokens: `--on-accent` (text on accent fills), semantic status palette `--ok/--warn/--err/--info/--teal` (+ RGB channels for tints; darker light-mode values for WCAG AA), elevation shadows `--sh-panel/--sh-modal/--sh-drop`, overlay scrim `--ov`. ~40 component SCSS files migrated off hardcoded status colors/dark-tuned shadows; dark mode visually unchanged. Chart.js colors in Team Dashboard now resolve CSS variables at runtime and re-render on theme switch.
- **KUmmand Center branding** — logo with the blue route/flag mark and "KUmmand Center" wordmark as PNGs; dedicated light/dark variants (`kummand_center_light_mode_transparent.png` — navy wordmark on baked-in white / `kummand_center_dark_mode_transparent.png` — white wordmark on baked-in `#262A33`) swapped by theme in the header and on the login page
- **Login page** with KU branding on a pure-CSS brand backdrop (no photos) in the logo palette — navy `#0B1B4A`, royal `#2A5CF0`, blue `#4D8BFF`, sky `#7AADFF`: slanted ribbon shapes and square "pixel" clusters echoing the logo mark, brand-blue radial light; card surface matches each logo PNG's background (`#262A33` dark / white light) with a sky→royal top keyline; royal-gradient primary "Sign in with Khalifa University" button (KU emblem only) and tinted-outline secondary local Sign In; honors `prefers-reduced-motion`; theme-aware logo and "Command Center" branding
- **Header:** Dynamic nav with permission-based link visibility, theme toggle, notification bell, user menu, mobile hamburger drawer
- **Footer** with KU logo
- **Loading bar** on route transitions
- **Browser title:** KU Action Tracker with KU favicon
- **Responsive:** Mobile card layouts for actions and lists
- **Confirm dialogs clickable again (2026-11-23)** — the shared `ConfirmDialogComponent` (Bootstrap modal) rendered inside `app-layout`, whose host is `position: relative; z-index: 1`; Bootstrap puts the backdrop on `<body>` at z-index 1050, so the backdrop covered the dialog and mouse users could not press Confirm or Cancel (keyboard still worked). The component now moves its modal element to `<body>` when it is created and removes it when destroyed. Fixes the Calendars page and the existing dialogs on Notifications ("Clear read"), user permission overrides, risk detail, risk register and My Actions
- **Standalone components** throughout (Angular best practice)
- **Angular signals** for state management

---

## 15. Action Item Workflow

### Date Freeze
- After creation, planned start / planned end on standalone action items are locked
- Editing dates requires submitting a date change request with justification
- Lock icon (🔒) displayed on frozen date fields in the UI
- Non-standalone (project/milestone-linked) items are unaffected
- Actual start / actual end (WO-2026-11 A-1) are never frozen and never go through a request

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
- **Integration test host repaired (2026-11-23):** `ActionTrackerWebApplicationFactory` now also removes EF Core 9's `IDbContextOptionsConfiguration<AppDbContext>` (the SQL Server provider used to stay registered next to InMemory and the host never started), starts hosts one at a time (Serilog bootstrap logger), and offers `EnsureUserAsync` to create test users; `Program.cs` calls `MigrateAsync` only on a relational provider and `EnsureCreatedAsync` otherwise. The older Auth / ActionItems integration tests were updated to the current login contract (`AuthResponseDto`, `/api/auth/refresh-token`). The last known failure, `ActionItemServiceTests.UpdateStatusAsync_SetToDone_SetsProgressTo100`, was a stale test (written before standalone items needed approval to reach Done) and was fixed in WO-2026-11 A-1: it now uses a project-linked item, and a new test covers the standalone path creating a status-change request. Suite: 232 passed, 0 failed (2026-11-24)

---

## 21. Not Yet Implemented

- [ ] Audit trail / activity log
- [ ] Advanced search (cross-entity, full-text)
- [ ] Scheduled reminders (automated due-date reminders)
- [ ] Gantt shading of non-working days from the workspace calendar (WO-2026-11 P-3); the calendars themselves (C-1, C-2, C-3) are done, see section 13
