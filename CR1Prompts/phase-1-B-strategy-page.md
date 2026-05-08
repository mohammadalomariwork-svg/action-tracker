# Prompt: CR-001-B — Per-Org-Unit Strategy Workspace ("Strategy Page")

> **CR reference:** `CR-001-Strategic-Plans-and-KPI-Actuals.docx`, section CR-001-B
> **Phase:** 1 (Structure)
> **Depends on:** CR-001-A (entity), CR-001-C (notifications), CR-001-I (read access)
> **Blocks:** CR-001-D will hang the approval banner on this page

---

## 0. Read first

1. `CLAUDE.md`
2. `FRONTEND_OVERVIEW.md` — Workspaces feature (best precedent for layout),
   the offcanvas pattern, the export/print helpers
3. The Workspaces detail page source (`features/workspaces/`) — copy its
   shape, don't reinvent
4. The KPIs feature (`features/kpis/`) — the inline target grid you'll embed
5. `src/app/services/strategic-plan.service.ts` (added by CR-001-A)
6. `src/app/services/strategic-objective.service.ts`, `kpi.service.ts`

---

## 1. Open questions

None blocking. Layout choices below are recommendations — adapt to match the
existing Workspaces / Projects pages exactly.

---

## 2. Scope

A new feature module that gives each org unit a **Strategy landing page**
showing:

- The active Strategic Plan (header card with name, cycle, status).
- The plan's Strategic Objectives (list).
- KPIs grouped by objective (count + period mix).
- KPI Targets (the existing inline-editable grid, now rendered inside this page).
- Export to Excel + Print to PDF (consistent with Workspaces / Projects).

**No approval banner yet** — that is CR-001-D. Just CRUD UX over the entity
graph CR-001-A produced.

---

## 3. Module structure

`frontend/action-tracker-ui/src/app/features/strategy/`:

```
strategy/
├── strategy.routes.ts                 # lazy-loaded
├── strategy-shell/                    # /strategy — landing page, org-unit picker
├── strategy-page/                     # /strategy/org-units/:orgUnitId
│   ├── strategy-page.component.ts
│   ├── strategy-page.component.html
│   ├── strategy-page.component.scss
│   └── components/
│       ├── plan-header-card/
│       ├── plan-create-dialog/        # used by PMO Head
│       ├── plan-edit-dialog/
│       ├── objectives-list/           # embedded
│       └── kpis-summary/              # embedded; deep-links to KPI detail
├── strategy.service.ts                # facade: composes plan + objectives + kpis fetches
└── strategy.module.ts                 # if non-standalone is used in this codebase; otherwise omit
```

Use **standalone components** (per `CLAUDE.md` conventions). Lazy-load via
`app.routes.ts`:

```ts
{
  path: 'strategy',
  loadChildren: () => import('./features/strategy/strategy.routes')
                        .then(m => m.STRATEGY_ROUTES),
  canActivate: [authGuard],
}
```

---

## 4. Routing

| Path | Component | Notes |
|---|---|---|
| `/strategy` | `StrategyShellComponent` | Org-unit picker. For Strategy Editors, redirects to their level-2 ancestor automatically. For Admin / PMO, shows a tree picker. |
| `/strategy/org-units/:orgUnitId` | `StrategyPageComponent` | The actual strategy view |

Guard: redirect to `/access-denied` if the user lacks `StrategicPlans.View`.

---

## 5. Strategy shell (landing page)

Behaviour:

1. On load, call `IStrategicScopeService` equivalent on the frontend (use
   `permissionStateService.getVisibleOrgUnitIds()` per `status.md` §1).
2. If exactly one org unit is visible (typical Strategy Editor case),
   navigate immediately to `/strategy/org-units/:that-id`.
3. If multiple, render a tree-or-flat-list picker. Match the picker style
   used by the Workspaces page's org-unit filter.
4. If zero, render a clear empty state ("No org units in your scope —
   contact your administrator").

---

## 6. Strategy page layout

Match the Workspaces detail page's visual hierarchy. Top to bottom:

### 6.1 Plan header card

A single hero card containing:

- **Title row:** Plan `Name` + status badge (`Draft` / `Approved` /
  `PendingApproval` / `Archived`). Status badge colour: amber for
  Pending, green for Approved, slate for Draft, neutral for Archived.
- **Cycle:** `StartYear–EndYear` and `DurationYears` years.
- **Org unit:** name + breadcrumb to org chart.
- **Action buttons** (right-aligned, permission-gated):
  - `*hasPermission="'StrategicPlans.Create'"` — "New plan for this org unit"
    (only visible if no active plan exists).
  - `*hasPermission="'StrategicPlans.Edit'"` — "Edit plan".
  - `*hasPermission="'StrategicPlans.Delete'"` — "Archive plan" (calls soft delete).
  - **No "Submit for approval" button yet** — that's CR-001-D.

If the org unit has **no plan**, render an empty state with a single
"Create Strategic Plan" CTA, gated by `StrategicPlans.Create`.

### 6.2 Objectives list

Re-use the existing strategic objectives list component — pass `orgUnitId`
and (new prop) `strategicPlanId` to filter.

If the plan filter is not yet supported by the existing component, add it.
This is the right time to start scoping objectives by plan, even though the
old global view still works.

Each row shows:

- `ObjectiveCode` (e.g. `SO-001`)
- `Statement` (truncate at ~120 chars, full on hover)
- KPI count + period mix (e.g. "3 KPIs · 2 Quarterly · 1 Annual")
- Inline "View KPIs" link → expands the KPIs section

### 6.3 KPIs section (collapsible per objective)

Each objective expands to show its KPIs. Each KPI row shows:

- `KpiNumber` and `Name`
- `Period` badge
- `CalculationMethod` (truncated)
- A "Manage targets" link → routes to the existing KPI detail page that
  contains the inline target grid (do not duplicate the grid here).

**Important:** the inline target grid was added in earlier work and now
includes the four CR-001-G fields. Do not re-implement it; deep-link to it.

### 6.4 Export to Excel and Print to PDF

Use the existing `ExportService` / print helper. Output should include the
plan header, the objectives list, and the KPIs grouped by objective. Mirror
the column layout of the Workspaces export.

---

## 7. Plan create / edit dialog

A right-side offcanvas (the project's standard for create/edit per
`CLAUDE.md`):

- Fields: `Name` (text), `StartYear` (number, min 2000), `DurationYears`
  (number, 1–10), `OrgUnitId` (read-only when launched from a specific
  Strategy page; selectable for Admin from `/strategy`).
- Save calls `StrategicPlanService.create()` / `.update()`.
- Show server-side 409 errors as inline "An active plan already exists for
  this org unit." messages — link to the existing plan.

---

## 8. Theme

The project enforces dark + light mode (per `CLAUDE.md` §"Conventions"). For
every new component:

- Use Bootstrap 5 utilities and the theme SCSS variables defined in
  `styles.scss`.
- Verify both modes manually before reporting done.
- Follow the Fluent / Palantir aesthetic — flat surfaces, subtle borders,
  no gradients, restrained colour use.

---

## 9. Header navigation

Add a top-level nav item "Strategy" (next to Workspaces / Projects) in the
header component. Visibility:

```html
<a *hasPermission="'StrategicPlans.View'" routerLink="/strategy">Strategy</a>
```

---

## 10. Loading and error states

- Skeleton loading on initial fetch (match existing skeletons).
- Error state with a "Retry" button.
- Empty state when no plan exists (handled in §6.1).

---

## 11. Tests

- A few component tests for permission gating (using TestBed):
  - Strategy Editor sees Edit, no Create / Delete.
  - PMO Head sees Create, no Edit / Delete (read-only).
  - Admin sees everything.
- An e2e-style test using the existing testing harness if the project has
  one — visit the Strategy page as each role, snapshot the visible buttons.

---

## 12. Acceptance criteria

1. `/strategy` route resolves correctly for each role.
2. The Strategy Editor lands directly on their unit's page after one click.
3. Creating a plan via the UI persists, fires the notification (per CR-001-C),
   and reloads the page showing the new plan.
4. Editing the plan and saving updates the header card immediately (no full
   page reload).
5. Both dark and light themes render cleanly across the page.
6. Export to Excel produces a workbook matching the on-screen content.
7. Print to PDF preserves layout (header, objectives, KPIs grouped).

---

## 13. Documentation updates

- `status.md` — new section **"Strategic Plans"** between sections 5 and 6,
  describing the per-org-unit Strategy page, plan CRUD via offcanvas,
  org-unit picker behaviour, and export/print. Bump `Last updated:`.
- `BACKEND_OVERVIEW.md` — no change (no new endpoints in this prompt).
- `FRONTEND_OVERVIEW.md` — add the `features/strategy/` module under the
  feature module list with its routes and components. Bump component count
  if the doc tracks one. Bump `Last updated:`.

---

## 14. Stop conditions

Stop and ask if:

- The Workspaces page uses a layout primitive (e.g. `<app-page-shell>`) that
  is not yet generic — you may need to extract one before reusing.
- The header nav already has a "Strategy" link pointing to the old flat
  list (`/strategic-objectives`). Confirm whether to deprecate or keep both.
