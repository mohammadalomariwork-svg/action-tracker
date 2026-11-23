# Work Order WO-2026-11 — KUmmand Center Enhancements

**Dashboards · Calendars · Action Items · Projects**

| Field | Value |
|---|---|
| Application | KUmmand Center (KU Action Tracker) |
| Requested by | Mohammad Khalifa Al-Omari — Enterprise Applications, Digital Services |
| Executor | Claude Code, one work package per session |
| Date / status | 19 November 2026 — Draft for owner review |
| Code baseline | commit `af5ab3f` (2026-11-19), `CLAUDE.md` / `status.md` dated 2026-11-18 |
| Stack | .NET 9 Web API (4-layer), EF Core + SQL Server, Angular 20 standalone, Bootstrap 5, Chart.js / ng2-charts |

This work order was written against the code as it stands, not only the overview files. Section 2 lists what is already built for each requirement so Claude Code extends it instead of rebuilding it.

---

## 1. How to run this work order

1. Open the repo in Claude Code (it loads `CLAUDE.md`).
2. Answer the **open questions in Section 3** first. Each has a default; if you do nothing, the default is what gets built.
3. Hand Claude Code **one work package at a time** from Section 5, in the order of Section 4, using this prompt:

   > Read `Docs/WO-2026-11-Kummand-Center-Enhancements.md`. Implement work package **<ID>** only. Follow Section 6 (rules) and stop at the Section 7 stop conditions. Do not start the next package.

4. After every package: `dotnet build`, `dotnet test`, `ng build`, then check the three documentation files were updated (`status.md`, `BACKEND_OVERVIEW.md`, `FRONTEND_OVERVIEW.md`).
5. Commit per package so each one can be reviewed or reverted on its own.

---

## 2. Baseline — what exists today vs. what is requested

### 2.1 Dashboards

| # | Requested | Exists today | Gap |
|---|---|---|---|
| D1 | Dashboard management: build dashboards from ready components; link to role / user / group and to one or more workspaces | Four hard-coded pages: `/dashboard`, `/dashboard/actions`, `/dashboard/projects`, `/dashboard/strategy` (+ `/management`). Reusable pieces in `features/dashboard/shared/` (`DashStat`, `DashPanel`, `RankedBars`, `DashLegend`, `DrilldownPanel`, `chart-theme.ts`). Data from `IDashboardAnalyticsService` | No dashboard definition stored anywhere; no assignment model; **no "user group" entity in the system**; no workspace filter on dashboard data |
| D2 | Granular, more detail on click | Single-level drill: card / segment → drawer (`DrilldownRequest`: `actions` or `rows`) → record page | No multi-level drill (e.g. workspace → project → milestone → action items) |
| D3 | Cover strategy, projects, tasks, staff performance; each card configurable and shown/hidden by role | Strategy, projects and action-item dashboards exist. Staff view is limited to "Team Activity" (ranked count) and team workload | No staff-performance measures; no per-card configuration or per-role visibility |
| D4 | Admin creates dashboards and assigns to users / roles | — | New |
| D5 | Admin Panel link "Dashboards Management" | Admin panel is a card list in `AdminPanelComponent`; routes in `features/admin/admin.routes.ts` | New card + route |

### 2.2 Calendar

| # | Requested | Exists today | Gap |
|---|---|---|---|
| C1 | Admin creates system-wide calendars used when configuring workspaces | Nothing. (`KpiPeriodCalendar` is a KPI period helper, unrelated) | New entities, admin page, workspace setting |
| C2 | Working days, off days, working hours | Nothing — no weekend/holiday logic anywhere in the backend | New |
| C3 | Projects and action items in a workspace respect the calendar | Dates are plain `DateTime`; overdue = `DueDate < UtcNow` | Date validation, duration maths, Gantt shading |

### 2.3 Action items

| # | Requested | Exists today | Gap |
|---|---|---|---|
| A1 | Title, description, sub-items (description, assignees, start, end) | `ActionItem` has title, description, multi-assignee (`ActionItemAssignee`), `StartDate`, `DueDate`, `Progress` | No sub-items |
| A2 | Planned and actual start/end | `StartDate` (nullable) and `DueDate` only. Dates are frozen by the approval workflow for project-linked items | No actual dates |
| A3 | "Action updates" instead of comments, on item and sub-item level, Draft / Submitted, locked once submitted | `ActionItemComment` (content, author, high-importance) with edit and delete; `ActionItemWorkflowService` also writes system comments into this table. A separate polymorphic `Comment` entity serves other screens | No draft state, no lock, no sub-item level |
| A4 | Attach documents to action items, sub-items and updates | Polymorphic `Document` (`RelatedEntityType` + `RelatedEntityId`, 10 MB, stored in DB); action detail already shows `app-documents-section` | New entity types; lock attachments on submitted updates |
| A5 | Custom tags per action item | — | New |

### 2.4 Projects

| # | Requested | Exists today | Gap |
|---|---|---|---|
| P1 | Global classification (Operational / Strategic) + internal classification set by workspace admin | **Global classification already exists**: `ProjectType { Operational = 1, Strategic = 2 }`, and it drives the approval chain | Only the workspace-defined internal classification is new |
| P2 | Project manager(s), sponsor(s), viewer(s), team — any university user | One required `Project.ProjectManagerUserId`, restricted to the workspace's `WorkspaceProjectManager` list; multiple sponsors (`ProjectSponsor`), picked from workspace-scoped users | No multiple PMs, no viewers, no team; pickers are workspace-scoped; access rules do not know about project-level people |
| P3 | Two views, Gantt as default; milestones containing action items | Project detail has a milestones grid and a **custom-built, read-only Gantt hidden behind a toggle** (`showGantt = false`) inside `ProjectDetailComponent` (~800 lines TS) | Gantt is not the default, not a separate component, not editable |
| P4 | Drag and drop to add milestone / action item; default milestone if none | No drag-and-drop library installed (`@angular/cdk` is not a dependency) | New |
| P5 | Details panel on the right after drop / on select | Create/edit drawers exist for action items (offcanvas) | Wire into the Gantt |
| P6 | Action item dates must match the milestone's start and end | No such validation; project action items may also have no milestone | New rule |
| P7 | Risk record | **A full risk register already exists**: `ProjectRisk`, `ProjectRisksController`, `RiskRegisterSection`, `RiskForm`, `RiskDetail`, `Risks.*` permissions | Requirement has no detail — see Q16 |

---

## 3. Open questions — confirm before starting

Defaults are what Claude Code builds if a question is left unanswered. If an answer differs, edit the affected work package before running it.

| # | Question | Default | Affects |
|---|---|---|---|
| Q1 | What is a "group of users"? | New `UserGroup` + `UserGroupMember` entities, managed inside Dashboards Management. No Azure AD group sync | D-1, D-4 |
| Q2 | A user matches several dashboards — what do they see? | All of them, as tabs, ordered User → Group → Role, then by name. The first is the landing dashboard | D-3 |
| Q3 | What does linking a dashboard to workspaces mean? | It filters the dashboard's data to those workspaces, intersected with what the user may already see. No workspaces linked = all the user's workspaces. A link never grants access | D-1, D-3 |
| Q4 | "Ready components" — fixed catalogue or free-form builder? | Fixed catalogue of widgets defined in code (Appendix A), each with a few settings. No custom query builder | D-2 |
| Q5 | What happens to today's four dashboards? | Seeded as non-deletable **system dashboards** built from the same widgets and assigned to the roles that see them today, so nobody loses anything. Admin may clone them | D-3 |
| Q6 | Standard KU working week and hours? | Seed one default calendar "KU Standard": Monday–Friday working, Saturday–Sunday off, 08:00–17:00, time zone Asia/Dubai. **Please confirm the days and hours** | C-1 |
| Q7 | How strictly does the calendar apply? | Start and end dates may not fall on a non-working day (rejected by the server, flagged in the form); durations are shown in working days; the Gantt shades non-working days. Overdue and due-window counting stay calendar-date based | C-3 |
| Q8 | Existing records with dates on off days? | Left untouched; the rule applies only when a date is created or changed | C-3 |
| Q9 | Are actual dates typed or automatic? | Actual start is set when the item first moves to In Progress, actual end when it moves to Done; both editable by anyone who may edit the item. Planned dates stay in the existing `StartDate` / `DueDate` columns | A-1 |
| Q10 | Do sub-items have their own status, and do they drive the parent's progress? | Each sub-item has a Done flag (with date and user). Parent shows "3 of 5 done"; parent `Progress` is **not** auto-calculated. One level only (no sub-sub-items) | A-2 |
| Q11 | Existing comments when "Action Updates" arrives? | Kept and shown as **Submitted** updates (therefore read-only). Drafts are visible to their author only. Nobody, including Admin, can edit or delete a submitted update | A-3 |
| Q12 | Does the rename apply to comments on projects / milestones / risks too? | No — action items and sub-items only. Other screens keep "Comments" | A-3 |
| Q13 | Attachment limits? | Existing rules: 10 MB per file, same file types as KPI evidence, max 10 files per item / sub-item / update | A-4 |
| Q14 | Who can create tags, and are they shared? | Free text typed on the action item; reused as suggestions within the same workspace. Anyone who can edit the item can add tags. Max 10 tags per item, 30 characters each | A-5 |
| Q15 | "Any user inside the university" vs. the workspace PM list | Project managers stay restricted to the workspace's PM list (existing, deliberate rule). Sponsors, viewers and team can be **any active user**. Viewers get read-only access to that project; team members get read access and can be assigned its action items — including in private workspaces, for that project only | P-2 |
| Q16 | What is required under "Risk Record"? | No new entity. The existing risk register becomes a tab on the redesigned project page and gains risk widgets on dashboards. **Needs your detail if more was meant** | P-5, D-2 |
| Q17 | "Action item dates should match the milestone" — identical, or inside? | Inside: milestone start ≤ action start ≤ action end ≤ milestone end. Shrinking a milestone is blocked while items would fall outside, with the offending items listed | P-4 |
| Q18 | Can bars be dragged to move / resize dates on the Gantt? | No. Drag-and-drop **adds** milestones and action items; dates are edited in the right-hand panel. Existing date-freeze and approval rules stay in force | P-3 |
| Q19 | New dependency approval | `@angular/cdk` (same major version as Angular) for drag-and-drop. No commercial Gantt library; extend the existing custom Gantt | P-3, D-4 |
| Q20 | Today, **action items cannot be added to a project once it leaves Draft** (`ActionItemService.CreateAsync`). Should the Gantt allow adding milestones / action items on Active projects? | No change: adding by drag-and-drop works while the project is Draft; on submitted or active projects the palette is disabled with an explanation. Lifting this rule needs a decision on how additions are approved | P-3, P-4 |

---

## 4. Execution order

Calendars first (other modules read them), then action items, then projects (which depend on both), then dashboards last so the new widgets can show the new data. Dashboards D-1 to D-3 do not depend on the others and can be pulled forward if dashboards are the priority.

| Order | ID | Work package | Size | Depends on |
|---|---|---|---|---|
| 1 | C-1 | Calendar entities, service, admin API | M | – |
| 2 | C-2 | Admin Panel: Calendars page | M | C-1 |
| 3 | C-3 | Workspace calendar setting + date rules | M | C-1 |
| 4 | A-1 | Planned / actual dates | S | – |
| 5 | A-2 | Sub-items | L | A-1, C-3 |
| 6 | A-3 | Action Updates (draft / submitted) | M | A-2 |
| 7 | A-4 | Attachments on items, sub-items, updates | S | A-2, A-3 |
| 8 | A-5 | Tags | S | – |
| 9 | P-1 | Internal classification | S | – |
| 10 | P-2 | Project people (PMs, sponsors, viewers, team) and access | L | – |
| 11 | P-3 | Gantt as default view, drag-and-drop, details panel | L | A-1, C-3 |
| 12 | P-4 | Milestone ↔ action-item date rule, default milestone | M | P-3 |
| 13 | P-5 | Risk register placement (pending Q16) | S | P-3 |
| 14 | D-1 | Dashboard definitions, assignments, groups (backend) | M | – |
| 15 | D-2 | Widget catalogue: extract existing dashboards into widgets | L | D-1 |
| 16 | D-3 | Dashboard renderer, system dashboards, routing | M | D-2 |
| 17 | D-4 | Admin Panel: Dashboards Management (builder) | L | D-3 |
| 18 | D-5 | Staff-performance widgets + multi-level drill-down | M | D-3, A-1 |

---

## 5. Work packages

Every package ends with the common acceptance criteria in Section 6.6 in addition to its own.

### 5.1 Calendar

#### C-1 — Calendar entities, service, admin API

**Domain** (`ActionTracker.Domain/Entities`)

- `WorkCalendar` — `Id` (Guid), `Name` (≤ 100, unique among non-deleted), `Description`, `TimeZoneId` (IANA/Windows id, default `Asia/Dubai`), `IsDefault`, `IsActive`, audit fields, `IsDeleted`.
- `WorkCalendarDay` — `WorkCalendarId`, `DayOfWeek`, `IsWorkingDay`, `StartTime`, `EndTime` (`TimeOnly?`). Exactly 7 rows per calendar.
- `WorkCalendarException` — `WorkCalendarId`, `Name` (e.g. "National Day", "Ramadan hours"), `StartDate`, `EndDate` (`DateOnly`, inclusive), `IsWorkingDay`, optional `StartTime` / `EndTime`. Covers holidays, special working days and reduced-hours periods.
- `Workspace.WorkCalendarId` (`Guid?`, FK, `Restrict`). Null means "use the default calendar".

**Application** — new folder `Features/Calendars/` (`DTOs`, `Interfaces`, `Services`, `Validators`), same shape as `Features/ProjectRisks`.

- `IWorkCalendarService`: CRUD; `SetDefaultAsync`; and the calculation API used by the other packages — `GetForWorkspaceAsync(workspaceId)`, `IsWorkingDay(date)`, `NextWorkingDay(date)`, `AddWorkingDays(date, n)`, `WorkingDaysBetween(start, end)`. Exceptions override the weekly pattern. Cache resolved calendars in memory; invalidate on save.
- Rules: exactly one default calendar at all times; a calendar in use by a workspace, or the default, cannot be deleted or deactivated; a working day needs `StartTime < EndTime`; exception `EndDate ≥ StartDate`.

**Infrastructure** — configurations, additive migration `Add_WorkCalendars`, seeder that inserts "KU Standard" (Q6) if no calendar exists.

**API** — `CalendarsController` at `/api/calendars`: list, get, create, update, delete, restore, `PUT /{id}/default`, `GET /{id}/usage` (workspaces using it). New permission area **`Calendars`** (View / Create / Edit / Delete): add constants to `PermissionPolicies.cs`, area GUID `1000001b-0000-0000-0000-000000000000` in `PermissionCatalogSeeder`, grants in `DefaultRolePermissionsSeeder` (Admin: all; Workspace Admin: View).

**Tests** — unit tests for the calculation API (weekend, holiday range, special working day, year boundary) and the delete-in-use guard.

**Acceptance** — default calendar seeded on a clean and on an existing database; second default is impossible; deleting a calendar in use returns 409.

#### C-2 — Admin Panel: Calendars page

- Route `/admin/calendars` in `admin.routes.ts` (`permissionGuard`, area `Calendars`, action `View`); new card "Calendars" in `AdminPanelComponent`.
- `features/admin/calendars/`: list (name, working days summary, hours, default badge, workspaces using it) and an editor drawer with three parts — **details**; **weekly pattern** (7 rows: working/off switch + start and end time); **exceptions** (table with add / edit / remove, date range, working or off, optional hours) plus a month preview that colours working days, off days and exceptions.
- Model in `models/work-calendar.model.ts`, service in `services/work-calendar.service.ts`. Buttons gated with `*hasPermission`.

**Acceptance** — an admin can create a calendar, flag days and hours, add a holiday, set it as default; the preview matches what `IWorkCalendarService` returns for the same dates.

#### C-3 — Workspace calendar setting + date rules

- **Workspace settings page** (`WorkspaceSettingsComponent`): new "Working Calendar" section — dropdown of active calendars, shows the effective calendar when none is chosen. Backed by `PUT /api/workspaces/{id}/calendar` (gated by `Workspaces.Edit`) and `GET /api/workspaces/{id}/calendar?from=&to=` returning the weekly pattern and exceptions in range, for the client.
- **Server-side rule** (Q7): in `ActionItemService`, `MilestoneService`, `ProjectService`, reject a create/update whose changed start or end date is a non-working day in the workspace's calendar (400, message naming the date and the next working day). Apply only to dates that changed (Q8). The workflow "apply approved date change" path uses the same check at request time, not at approval time.
- **Frontend**: shared helper `shared/utils/work-calendar.util.ts` + a form validator; action item, sub-item, milestone and project forms show the inline error and a "use next working day" shortcut; durations display as "N working days". No new date-picker library.
- `DashboardRules.cs` is **not** changed in this package.

**Acceptance** — in a workspace on a Mon–Fri calendar, saving an action item due on a Saturday is rejected by API and form; the same item in a workspace on another calendar saves; untouched legacy items with weekend dates still load and can be edited in other fields.

### 5.2 Action items

#### A-1 — Planned and actual dates

- `ActionItem`: add `ActualStartDate`, `ActualEndDate` (`DateTime?`). Existing `StartDate` / `DueDate` remain the **planned** dates — do not rename the columns (the date-freeze workflow, filters and `DashboardRules` depend on them); relabel them "Planned start" / "Planned end" in DTO docs and UI.
- Auto-fill (Q9): first transition to `InProgress` sets `ActualStartDate` if empty; transition to `Done` sets `ActualEndDate`; moving back out of `Done` clears `ActualEndDate`. Cover every path that changes status: `UpdateAsync`, `UpdateStatusAsync`, `ApplyApprovedStatusChangeAsync`.
- Validation: actual end ≥ actual start; actual dates not in the future. Actual dates are **not** subject to the planned-date freeze.
- Backfill in the migration: for existing `Done` items set `ActualEndDate = UpdatedAt`; leave the rest null.
- UI: action form, detail, list columns (optional), Excel export, Gantt tooltip. Show variance (actual vs planned end) on the detail page.

**Acceptance** — status changes set the dates as described; a frozen project item still accepts actual-date edits; export contains the four dates.

#### A-2 — Sub-items

- **Domain**: `ActionItemSubItem` — `Id`, `ActionItemId`, `Title` (≤ 255), `Description` (≤ 5000), `StartDate`, `EndDate`, `SortOrder`, `IsDone`, `DoneAt`, `DoneByUserId`, audit fields, `IsDeleted`. `ActionItemSubItemAssignee` (composite key `SubItemId` + `UserId`), mirroring `ActionItemAssignee`. Add the same private-workspace global query filter pattern used for `ActionItem` in `AppDbContext`.
- **Rules**: sub-item dates lie within the parent's planned dates; `EndDate ≥ StartDate`; calendar rule from C-3; assignees come from the same pool as the parent (`GET /api/workspaces/{id}/assignable-users`, plus project team after P-2); no sub-items on a `Done` / `Cancelled` parent; deleting the parent soft-deletes its sub-items. One level only.
- **API**: `/api/action-items/{id}/sub-items` — list, create, update, delete, `PATCH /{subId}/done`, `PUT /reorder`. Reads gated by `ActionItems.View`, writes by `ActionItems.Edit`; sub-item assignees may toggle `done` on their own sub-item.
- **Notifications**: assignment and completion through the existing `IWorkflowNotificationHelper` (in-app + e-mail + SignalR). Add e-mail templates via `EmailTemplateSeeder`.
- **Frontend**: "Sub-items" section on `ActionDetailComponent` (inline add, edit drawer, assignee chips, dates, done checkbox, reorder); "x of y done" on list rows and the Gantt tooltip; My Actions includes parents where the user is assigned only to a sub-item, marked as such.
- **Dashboards**: counting rules stay on the parent action item — sub-items are not counted as separate action items.

**Acceptance** — CRUD works with date and assignee rules enforced server-side; a user assigned only to a sub-item sees the parent in My Actions and is notified; private-workspace sub-items are invisible to non-members.

#### A-3 — Action Updates (replaces comments on action items)

- **Data** — keep the `ActionItemComments` table (no rename, no data loss). Add `SubItemId` (`Guid?`, FK), `Status` (`Draft = 0`, `Submitted = 1`), `SubmittedAt`. Migration backfill: every existing row → `Submitted`, `SubmittedAt = CreatedAt`. The C# entity may be renamed `ActionItemUpdate` mapped to the same table.
- **Rules** (Q11): a draft is visible and editable only by its author and can be deleted; `Submit` is one-way; after submit, edit and delete return 409 for everyone; attachments on it are locked (A-4). System comments written by `ActionItemWorkflowService` are created as `Submitted`.
- **API**: `/api/action-items/{id}/updates` and `/api/action-items/{id}/sub-items/{subId}/updates` — list (submitted + my drafts), create draft, update draft, `POST /{updateId}/submit`, delete draft. Replace the comment methods on `IActionItemService`; remove the old comment routes once the UI no longer calls them.
- **Frontend**: new `ActionUpdatesSectionComponent` (do not alter `CommentsSectionComponent`, still used elsewhere — Q12). "Save draft" and "Submit" buttons, Draft badge, confirm on submit ("cannot be changed afterwards"), high-importance flag retained. Used on the action detail page and inside each sub-item. Rename all "Comments" wording on action-item screens, notifications and e-mail templates to "Action Updates".
- **Notifications**: only on submit, never on draft.

**Acceptance** — existing comments appear as submitted updates; another user cannot see a draft; a submitted update cannot be edited or deleted through UI or API by any role.

#### A-4 — Attachments on items, sub-items and updates

- Reuse `Document` / `DocumentService` / `app-documents-section`. Register entity types `ActionItemSubItem` and `ActionItemUpdate` alongside the existing `ActionItem` type; add them to `MaxDocumentsPerEntity` (10 each, Q13).
- Extend `EnsureScopedWriteAsync` and the read path: for these three types resolve the parent action item and require workspace access (`IWorkspaceAccessResolver`), so documents of private workspaces cannot be listed or downloaded by id.
- Uploads/deletes on a **submitted** update are rejected; on a draft only the author may upload. Deleting a draft removes its documents.
- UI: paperclip with count on sub-item rows and updates; attach files while writing a draft.

**Acceptance** — files attach at all three levels; a non-member gets 403 on download; a submitted update's attachments cannot be changed.

#### A-5 — Tags

- `Tag` (`Id`, `WorkspaceId`, `Name` ≤ 30, `NormalizedName`, unique per workspace) and `ActionItemTag` (composite key). Tags are created on first use; unused tags may remain.
- Create/update DTOs take `Tags: string[]` (trimmed, de-duplicated case-insensitively, max 10 — Q14). `GET /api/workspaces/{id}/tags?search=` for suggestions. `ActionItemFilterDto.Tags` filters lists (any-of).
- UI: chip input with suggestions on the action form and detail; tag chips on list rows; tag filter on My Actions / Team Actions / workspace lists; tags column in the Excel export.

**Acceptance** — tags save, suggest within the workspace only, filter lists, and never leak names from a private workspace.

### 5.3 Projects

#### P-1 — Internal classification

- Global classification is the existing `ProjectType` — **no change**; relabel the field "Classification (global)" in the form and detail page.
- `WorkspaceProjectClassification` (`Id`, `WorkspaceId`, `Name` ≤ 100 unique per workspace, `SortOrder`, `IsActive`, `IsDeleted`); `Project.InternalClassificationId` (`Guid?`, must belong to the project's workspace).
- Workspace settings: new "Project Classifications" section (add, rename, reorder, deactivate; delete blocked while used). Endpoints under `/api/workspaces/{id}/project-classifications`, writes gated by `Workspaces.Edit`.
- Project form: optional dropdown fed by the workspace; shown on detail, lists, filters, Excel export and available as a grouping in dashboard widgets (D-2).

**Acceptance** — a workspace admin maintains the list; a project cannot take another workspace's classification.

#### P-2 — Project people and access

- **Data**: new `ProjectMember` (`ProjectId`, `UserId`, `Role`: `ProjectManager`, `Viewer`, `TeamMember`; unique on the triple). Keep `Project.ProjectManagerUserId` as the **lead PM** (approval routing, notifications and the PM-list rule depend on it); additional PMs are `ProjectMember` rows with role `ProjectManager` and must also be on the workspace PM list. Sponsors stay in the existing `ProjectSponsor` table.
- **Pickers** (Q15): PMs from `GET /api/workspaces/{id}/project-managers` (unchanged); sponsors, viewers, team from a university-wide user search (active users; reuse the user lookup used by the admin typeahead). Remove the "sponsors are workspace-scoped" reset in `ProjectFormComponent`.
- **Access — the sensitive part.** Extend `IWorkspaceAccessResolver` and the private-workspace query filters so that a user who is PM, sponsor, viewer or team member of a project can read **that project** and its milestones, action items, risks and documents, without gaining the rest of the workspace. Viewers and sponsors: read-only. Team members: read + act on action items assigned to them. Additional PMs: same project rights as the lead PM, except approval routing. Everything that calls `IgnoreQueryFilters()` must be re-checked (`WhereWorkspaceVisibleTo`).
- Action-item assignee pool for project items = workspace assignable users ∪ project team.
- UI: "People" block on the project form (four chip pickers) and a People panel on the project page; My Projects lists projects where the user holds any of the four roles, with the role shown.
- Notifications when a user is added to a project.

**Tests (mandatory)** — viewer of one project in a private workspace: sees that project, gets 403/404 on sibling projects and on the workspace; team member cannot edit project fields; removing the membership removes access; dashboards and reports for that user include only the permitted project.

**Acceptance** — the tests above pass; existing projects keep their PM and sponsors unchanged.

#### P-3 — Gantt as default view, drag-and-drop, details panel

- **Refactor first, no behaviour change**: move the Gantt out of `ProjectDetailComponent` into `features/projects/components/project-gantt/` (component + `gantt.model.ts` + a pure `gantt-layout.util.ts` for range, month headers and bar geometry). Remove the `.bak` files in `milestone-section` only if untracked by anything.
- **Views**: a view switch on the project page — **Gantt** (default) and **List** (today's milestones grid). Remember the last choice per user in `localStorage`; deep link `?view=list`.
- **Timeline**: rows = milestones, each expandable to its action items (sub-item count on the bar); non-working days shaded from the workspace calendar (C-3); today marker; zoom week / month / quarter; planned bar with an actual-dates overlay (A-1).
- **Drag-and-drop to add** (`@angular/cdk/drag-drop`, Q19): a palette with two chips, *Milestone* and *Action item*.
  - Drop *Milestone* on the timeline → draft milestone starting at the drop date (snapped to a working day), default length 10 working days.
  - Drop *Action item* on a milestone row → draft action item in that milestone starting at the drop date, default length 5 working days, clamped to the milestone.
  - Drop *Action item* when the project has **no milestones** → create a default milestone "General" spanning the project's planned dates, then the action item inside it (P-4 holds the server rule).
  - Keyboard / no-drag alternative: "Add milestone" and "Add action item" buttons do the same.
- **Right-hand details panel**: opens immediately after a drop with the draft pre-filled (name, dates, parent) and on click of any existing bar for editing. Milestone fields = existing milestone form; action item fields = existing action form plus sub-items, tags and updates tabs. Nothing is saved until **Save**; Cancel discards the draft bar.
- **Rules that stay** (Q20): new action items are only accepted while the project is Draft — on other statuses the palette is disabled with the reason shown. Creation and date edits follow the existing project-status and date-freeze rules in `ActionItemService` / `MilestoneService` / `ProjectWorkflowService` — the Gantt calls the same endpoints and shows the server's message; when a date change needs approval, the panel offers the existing change-request flow. Hide the palette for users without `Milestones.Create` / `ActionItems.Create` and for viewers.
- Bars are not draggable for move/resize (Q18).

**Acceptance** — project page opens on the Gantt; dropping each chip creates the right draft and opens the panel; saving persists via existing endpoints; a frozen project shows read-only bars and the approval route; works in dark and light themes and with keyboard only.

#### P-4 — Milestone ↔ action-item date rule, default milestone

- Server rule (Q17) in `ActionItemService`: for an item with a milestone, `milestone.PlannedStartDate ≤ StartDate` and `DueDate ≤ milestone.PlannedDueDate`; `StartDate` becomes required for milestone-linked items. In `MilestoneService`: reject a date change that would leave live action items outside, returning the list.
- Milestone dates must lie within the project's planned dates (add if not already enforced).
- Default milestone: `POST /api/projects/{id}/milestones/default` (idempotent) creates "General" when the project has none; used by P-3. New project action items must have a milestone; existing items without one stay as they are and are listed under "No milestone" on the Gantt with a prompt to assign.
- A read-only report endpoint `GET /api/projects/{id}/date-conflicts` lists existing items outside their milestone so PMs can fix legacy data; show it as a warning banner on the project page.

**Acceptance** — out-of-range saves are rejected with a clear message; legacy data is reported, not altered; no migration changes existing dates.

#### P-5 — Risk register placement (pending Q16)

Default scope only: the existing `RiskRegisterSectionComponent` becomes a **Risks** tab on the redesigned project page (next to Gantt / List), with the open-risk count on the tab; risk widgets are added to the catalogue in D-2. No entity or API change. If Q16 is answered with more detail, replace this package.

### 5.4 Dashboards

#### D-1 — Dashboard definitions, assignments, groups (backend)

- **Entities**
  - `Dashboard` — `Id`, `Name` (≤ 100), `Description`, `IsSystem`, `IsActive`, `SortOrder`, audit, `IsDeleted`.
  - `DashboardWidget` — `Id`, `DashboardId`, `WidgetKey` (catalogue key), `Title` (override), `SortOrder`, `ColSpan` (1–3), `ConfigJson` (validated against the widget's settings), `IsVisible`.
  - `DashboardAssignment` — `DashboardId`, `TargetType` (`Role`, `User`, `Group`), `TargetId`.
  - `DashboardWidgetAudience` — `DashboardWidgetId`, `TargetType`, `TargetId`. No rows = everyone who sees the dashboard; rows = only those roles / users / groups.
  - `DashboardWorkspace` and `DashboardWidgetWorkspace` — workspace scope at dashboard level, optionally narrowed per widget (Q3).
  - `UserGroup` (`Id`, `Name`, `Description`, `IsDeleted`) and `UserGroupMember` (Q1).
- **Services** (`Features/Dashboards/`): admin CRUD, clone, assignment management, group management; `GetMyDashboardsAsync()` resolving the current user's dashboards via user id, groups and roles (Q2), and `GetForUserAsync(dashboardId)` returning only the widgets the user may see — audience match **and** the widget's required permission (e.g. strategy widgets need `StrategicPlans.View`).
- **Security rule**: assignments decide what is *shown*; data access is always decided by the existing resolvers (`IWorkspaceAccessResolver`, `IStrategicScopeService`, private-workspace filters). A dashboard can narrow data, never widen it.
- **API**: `/api/dashboards/mine`, `/api/dashboards/{id}` (viewer); `/api/admin/dashboards` (+ `/clone`, `/assignments`, `/widgets`, `/workspaces`), `/api/admin/user-groups`. New permission area **`DashboardManagement`** (View / Create / Edit / Delete / Assign), area GUID `1000001c-0000-0000-0000-000000000000`, Admin only by default.
- System dashboards cannot be deleted or have their key widgets removed; they can be cloned and their assignments changed.

**Acceptance** — resolution tests: user-only, group-only, role-only, overlapping, inactive dashboard, widget hidden by audience, widget hidden by missing permission.

#### D-2 — Widget catalogue: extract existing dashboards into widgets

- Create a **widget registry** on both sides with matching keys: backend `Features/Dashboards/Widgets/` (key, category, required permission, settings schema, data provider), frontend `features/dashboard/widgets/` (`widget-registry.ts` mapping key → standalone component, default size, settings form). Catalogue in Appendix A.
- Move each block of `TeamDashboardComponent`, `ActionsDashboardComponent`, `ProjectsDashboardComponent`, `StrategyDashboardComponent` into its own widget component, reusing `DashStat`, `DashPanel`, `RankedBars`, `DashLegend`, `chart-theme.ts`. Widgets take `{ config, workspaceIds }` and emit `DrilldownRequest`.
- Backend: extend `IDashboardAnalyticsService` methods with an optional workspace filter and common settings (period, top N, group-by incl. internal classification from P-1). Keep the counting rules in `DashboardRules.cs` as the single source — a drill-down list must still equal its chart.
- `GET /api/dashboards/widget-catalog` for the builder.
- **No visual or numeric change** to the four existing pages in this package: they render the same widgets in the same layout.

**Acceptance** — the four pages look and count exactly as before (compare against `DashboardAnalyticsServiceTests` and a manual side-by-side); each widget renders standalone with a workspace filter.

#### D-3 — Dashboard renderer, system dashboards, routing

- `DashboardViewComponent` renders any dashboard definition: 3-column grid (`.dash-grid` from `_dash-page.scss`), widgets by `SortOrder` / `ColSpan`, one shared `DrilldownPanelComponent`, per-widget loading / error / retry states, a workspace selector when the dashboard spans several workspaces.
- Routes: `/dashboard` → the user's landing dashboard; `/dashboard/:id` → a specific one. Keep `/dashboard/actions`, `/dashboard/projects`, `/dashboard/strategy` as redirects to the matching system dashboards so existing links and bookmarks work.
- `DashTabsComponent` lists the user's assigned dashboards instead of the fixed four.
- Seeder (Q5): create the four system dashboards from the widget catalogue and assign them to the roles that hold `Dashboard.View` / `StrategicPlans.View` today. Idempotent.
- A user with no assigned dashboard sees a friendly empty state, not an error. `/management` is unchanged.

**Acceptance** — every existing role lands on an equivalent dashboard after upgrade; a custom dashboard assigned to a role appears as a new tab for those users only.

#### D-4 — Admin Panel: Dashboards Management

- New card **"Dashboards Management"** in `AdminPanelComponent`; route `/admin/dashboards` (`permissionGuard`, area `DashboardManagement`, action `View`).
- **List**: name, system badge, active switch, assigned-to summary, workspaces, widget count; actions edit / clone / delete.
- **Builder** (`/admin/dashboards/:id`):
  - *Details* — name, description, active.
  - *Audience* — three pickers: roles, users, groups (with "manage groups" drawer for `UserGroup`).
  - *Workspaces* — multi-select; empty = all the viewer's workspaces.
  - *Layout* — widget palette grouped by category (Strategy, Projects, Tasks, Staff performance, Risks); drag from palette to grid and reorder with `@angular/cdk/drag-drop`; set column span; remove.
  - *Widget settings drawer* — title, widget-specific settings, workspace narrowing, **visibility** (everyone on this dashboard, or chosen roles / users / groups), show / hide switch.
  - *Preview as* — pick a role or user and see the dashboard exactly as they would, with their data scope.
- Save is explicit; warn on unsaved changes.

**Acceptance** — an admin builds a dashboard from the palette, hides one card from a role, assigns it to a role and to a single user for two workspaces, and both users see the expected result.

#### D-5 — Staff-performance widgets + multi-level drill-down

- **Staff performance** (new provider in `DashboardAnalyticsService`, rules added to `DashboardRules.cs`): per person — assigned, completed, completed on time %, overdue, average days late, open workload; uses planned vs actual dates from A-1. Visible people limited to those the viewer may already see through workspace access. Widgets: on-time completion ranking, overdue by person, workload vs completion, person scorecard. These widgets require the existing `ManagementDashboard.View` permission.
- **Multi-level drill-down**: extend `DrilldownRequest` with a `children` resolver and give `DrilldownPanelComponent` a breadcrumb stack with back navigation. Paths: workspace → projects → milestones → action items → sub-items; objective → KPIs → targets / actuals; person → their action items by status → item; risk rating → risks → risk. The last level always links to the record's page. Server-paged lists stay server-paged.

**Acceptance** — each path can be walked down and back with mouse and keyboard; counts at every level equal the number of rows shown at the next.

---

## 6. Rules for every work package

### 6.1 Read first
`CLAUDE.md`, `BACKEND_OVERVIEW.md`, `FRONTEND_OVERVIEW.md`, `status.md`, then the files named in the package, then one existing feature of the same shape to copy the pattern from (suggested: `Features/ProjectRisks` + `ProjectRisksController` for CRUD; `features/admin/data-sync` for an admin page; `WorkspaceSettingsComponent` for settings sections).

### 6.2 Follow the code, not the briefing, where they differ
- `CLAUDE.md` says "CQRS with MediatR" — **the codebase has no MediatR**. The real pattern is `Application/Features/<Feature>/{DTOs, Interfaces, Services, Validators}` with services injected into controllers (some services live in `Infrastructure/Services`). Use that pattern. Do not introduce MediatR.
- `CLAUDE.md` says versioned routes (`/api/v1/...`) — actual routes are `/api/<resource>`. Use the actual style.
- Entity keys are `Guid`; user ids are `string` (ASP.NET Identity).

### 6.3 Data
- EF Core migrations, **additive only**; new foreign keys nullable; backfills inside the migration; no column renames or drops; never lose data. Command: `dotnet ef migrations add <Name> --project ActionTracker.Infrastructure --startup-project ActionTracker.API`.
- Soft delete (`IsDeleted`) for user data; add global query filters consistent with `AppDbContext`.
- Any new entity hanging off a workspace, project or action item must honour **private workspaces** — extend the query filters, and re-apply `WhereWorkspaceVisibleTo` wherever `IgnoreQueryFilters()` is used.

### 6.4 Security and permissions
- Every new endpoint carries an `[Authorize(Policy = PermissionPolicies.X)]`. New permissions need all four places: constant in `PermissionPolicies.cs`, area/mapping in `PermissionCatalogSeeder` (stable GUIDs), default grants in `DefaultRolePermissionsSeeder`, and `*hasPermission` + `permissionGuard` on the UI.
- Data scoping always goes through `IWorkspaceAccessResolver` / `IStrategicScopeService`.
- DTOs only at the API boundary; FluentValidation for new request DTOs.

### 6.5 Frontend
- Standalone components, signals or services (no NgRx), models in `models/` or the feature's `models/`, lazy routes.
- Theme tokens from `_theme-variables.scss` only; verify dark and light. Charts provide `provideCharts(withDefaultRegisterables())` and sit in fixed-height containers; chart callbacks run inside `NgZone.run()`.
- Reuse shared components (`PageHeader`, `DataTable`, `ConfirmDialog`, `DocumentsSection`, `DashStat`, `DashPanel`, the searchable multi-select pattern). Only new dependency allowed: `@angular/cdk` (Q19).
- Notifications through `IWorkflowNotificationHelper`; e-mail templates through `EmailTemplateSeeder`.

### 6.6 Common acceptance criteria
1. `dotnet build`, `dotnet test` and `ng build` pass with no new warnings.
2. Migration applies to a database holding production-like data and to an empty one; nothing existing is altered beyond the stated backfills.
3. New endpoints appear in Swagger with their policy; a user without the permission gets 403.
4. Private-workspace data is not exposed through any new endpoint, widget, export or document download.
5. New UI works in dark and light mode, at mobile width, and by keyboard.
6. Unit tests in `ActionTracker.Tests/Unit` for new rules; one integration test per new controller using `ActionTrackerWebApplicationFactory`.
7. `status.md`, `BACKEND_OVERVIEW.md`, `FRONTEND_OVERVIEW.md` updated with what was actually built and their "Last updated" dates bumped; `CLAUDE.md` entity and feature lists updated.

---

## 7. Stop conditions

Claude Code stops and asks before continuing if:

- an open question in Section 3 that the package depends on has no answer **and** the default would be hard to reverse (Q11 comment migration, Q15 access model, Q5 system dashboards);
- a migration would need to drop, rename or re-type an existing column, or a backfill touches more rows than expected;
- the code differs materially from Section 2 (e.g. someone has started the same feature);
- a change would alter existing counting rules in `DashboardRules.cs`, the approval / date-freeze workflows, or private-workspace filtering in a way this document does not describe;
- a package would need a dependency other than `@angular/cdk`.

---

## 8. Out of scope

Azure AD group sync; free-form report or query builder; drag-to-move / resize on the Gantt; task dependencies and critical path; resource levelling by working hours; calendar-aware overdue counting; changes to the approval workflows; changes to `/management`; the V2.0 register items (goals layer, sectors, AI, Outlook add-in) covered by `Docs/KummandCenter V2.0`.

---

## Appendix A — Widget catalogue (initial)

| Category | Widget key | Source today | Drill path |
|---|---|---|---|
| Strategy | `strategy.stats`, `strategy.kpi-health`, `strategy.objectives-ranked`, `strategy.kpis-weakest`, `strategy.projects`, `strategy.plans` | `StrategyDashboardComponent` | objective → KPIs → targets / actuals |
| Projects | `projects.stats`, `projects.health`, `projects.status`, `projects.type-split`, `projects.progress-bands`, `projects.health-by-workspace`, `projects.attention`, `projects.milestones`, `projects.by-classification` *(new, P-1)* | `ProjectsDashboardComponent` | workspace → projects → milestones → action items |
| Tasks | `actions.stats`, `actions.status`, `actions.created-vs-completed`, `actions.by-priority`, `actions.by-due`, `actions.by-age`, `actions.by-workspace`, `actions.my-actions`, `actions.recent`, `actions.by-tag` *(new, A-5)* | `ActionsDashboardComponent`, `TeamDashboardComponent` | status / bucket → action items → sub-items |
| Staff performance | `staff.team-activity`, `staff.workload`, `staff.on-time-ranking` *(new)*, `staff.overdue-by-person` *(new)*, `staff.scorecard` *(new)* | `TeamDashboardComponent`, D-5 | person → items by status → item |
| Risks | `risks.by-rating` *(new)*, `risks.top-open` *(new)* | `ProjectRiskService` stats | rating → risks → risk |

Common settings for every widget: title, column span, workspace narrowing, audience (roles / users / groups), visible on/off. Charts and lists add period and top-N where meaningful.

## Appendix B — Requirement traceability

| Requirement | Package(s) |
|---|---|
| Dashboards 1 (management, link to role / user / group / workspaces) | D-1, D-2, D-4 |
| Dashboards 2 (granular drill-down) | D-5 (existing single level kept in D-2) |
| Dashboards 3 (strategy, projects, tasks, staff; per-card config and role visibility) | D-2, D-4, D-5 |
| Dashboards 4 (admin creates and assigns) | D-1, D-4 |
| Dashboards 5 (Admin Panel link) | D-4 |
| Calendar 1 (admin calendars used by workspaces) | C-1, C-2, C-3 |
| Calendar 2 (working days, off days, hours) | C-1, C-2 |
| Calendar 3 (projects and action items respect it) | C-3, P-3 |
| Action Items 1 (sub-items) | A-2 |
| Action Items 2 (planned / actual dates) | A-1 |
| Action Items 3 (action updates, draft / submitted) | A-3 |
| Action Items 4 (attachments) | A-4 |
| Action Items 5 (tags) | A-5 |
| Projects 1 (classifications) | P-1 (global already built) |
| Projects 2 (PMs, sponsors, viewers, team) | P-2 |
| Projects 3 (two views, Gantt default) | P-3 |
| Projects 4 (drag and drop, default milestone) | P-3, P-4 |
| Projects 5 (right-hand details panel) | P-3 |
| Projects 6 (action dates within milestone) | P-4 |
| Projects 7 (risk record) | P-5 — pending Q16 |
