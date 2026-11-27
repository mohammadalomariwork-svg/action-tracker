# V-W01 — Widget `portfolio.stats` — Portfolio KPI tiles

> **Work order:** WO-2026-12 — Executive Office dashboards and Outlook add-in
> **Package:** V-W01 · **Size:** M · **Depends on:** WO-2026-11 D-1 to D-5; V-F6, V-F8
> **Before anything else read:** `Docs/WO-2026-12-VP-Dashboards-Outlook-Addin/prompts/00-README.md` (rules, definitions, stop conditions), then `CLAUDE.md`, `status.md`, `BACKEND_OVERVIEW.md`, `FRONTEND_OVERVIEW.md`.
> Implement **this package only**. Do not start the next one.

## Widget
| | |
|---|---|
| Catalogue key | `portfolio.stats` |
| Category | Portfolio |
| Mockup | S1 Portfolio overview and S2 Department view — "KPI tile row" |
| Default size | full width (3 columns), one row of tiles |
| Needs | dashboard root org unit; office group for the office-actions tile |
| Required permission | `Projects.View` |

## What it shows
A row of stat tiles, each selectable in settings:
- **Active projects** — count, sub-line "N strategic · N operational".
- **Portfolio health** — three numbers (green, amber, red) and a proportional bar.
- **Milestones on time** — percentage, sub-line "X of Y due this <period>".
- **Behind schedule** — projects whose forecast finish is after baseline.
- **% complete vs plan** — mean % complete, sub-line "planned P% · N pts behind / ahead".
- **Milestones next 30 days** — count, sub-line "N overdue".
- **Office actions open** — count, sub-line "N overdue · N due this week".
- **Decisions pending** — count, sub-line "N older than <threshold> days".
- **Open risks (high)** — open risks rated High or Critical, sub-line "N older than 30 days".

## Data (server, from the V-F6 row set unless stated)
Counts over the scope's projects with a RAG (Active and On Hold). "Milestones on time": of live milestones whose baseline due date is in the period, the share that are Done on or before baseline, or not yet due and forecast on or before baseline. Office-actions tile: open items in the office scope (V-F5) whose project is in scope or which have no project and whose department is in scope.

## Settings (builder drawer)
Tiles to show and their order; tile label overrides; period default.

## Interaction
Each tile drills to its list: projects (→ project), milestones (→ project page at the milestone), action items, decisions, risks. A tile whose data the viewer lacks permission for (`ActionItems.View`, `ProjectDecisions.View`, `Risks.View`) is hidden, not shown as zero.

## Build
- **Backend**: a data provider registered in the widget registry under the key above, with its category, required permission and settings schema (validated when the dashboard is saved). It takes the `PortfolioScope` from the dashboard context and returns a DTO made for this widget. Reuse `IPortfolioAnalyticsService`; add a query only for what the row set does not hold.
- **Frontend**: a standalone component under `features/dashboard/widgets/`, registered in the frontend registry with its default size and settings form. Use `DashPanel`, `DashStat`, `RankedBars`, `DashLegend`, `chart-theme.ts` and the theme tokens; RAG colours come from one shared place (add `rag-colors` tokens once, in the first widget that needs them, and reuse). Loading, empty, "not configured" and error-with-retry states.
- **Tests**: provider unit tests against `PortfolioTestData` (expected figures; the hidden private project never counted; each setting); a component spec for rendering, the empty state and the emitted drill-down.

## Acceptance
- Fixture: tile values equal the expected figures for "all departments" and for one department.
- Health numbers add up to the active-projects count.
- Works alone on a new dashboard, with and without a department in the context, in dark and light, at mobile width and by keyboard.
- The same figure shown by another widget of this series is identical for the same viewer and filters.

## Done when
- The acceptance points above hold, and the common acceptance criteria in `00-README.md` §5 hold.
- `dotnet build`, `dotnet test`, `ng build` pass with no new warnings.
- `status.md`, `BACKEND_OVERVIEW.md`, `FRONTEND_OVERVIEW.md` (and the entity / feature lists in `CLAUDE.md`) describe what was actually built.
- You finish with a short report: what was built, what differs from this prompt and why, any open point for the owner.
