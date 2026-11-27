# V-W22 — Widget `roadmap.gantt` — Portfolio roadmap (Gantt)

> **Work order:** WO-2026-12 — Executive Office dashboards and Outlook add-in
> **Package:** V-W22 · **Size:** L · **Depends on:** WO-2026-11 D-1 to D-5; V-F6, V-F8
> **Before anything else read:** `Docs/WO-2026-12-VP-Dashboards-Outlook-Addin/prompts/00-README.md` (rules, definitions, stop conditions), then `CLAUDE.md`, `status.md`, `BACKEND_OVERVIEW.md`, `FRONTEND_OVERVIEW.md`.
> Implement **this package only**. Do not start the next one.

## Widget
| | |
|---|---|
| Catalogue key | `roadmap.gantt` |
| Category | Roadmap |
| Mockup | S5 Portfolio roadmap — "All projects by department" |
| Default size | full width |
| Needs | root org unit for grouping by department |
| Required permission | `Projects.View` |

## What it shows
A timeline grouped by department (collapsible group rows with "N projects · xG yA zR" and a summary bar). Per project: RAG dot, code, name, S / O badge, % done; a grey baseline line; a bar from start to forecast finish, filled to % complete, coloured by RAG; diamonds for gates and key milestones; arrows for cross-project dependencies; shaded bands for planning periods with their names; a today line. Header scale: quarters over months. Controls: zoom Month / Quarter / Year; group by Department / Type / Workspace / None; show Baseline / Gates / Dependencies / Freeze toggles. Legend line under the chart.

## Data (server, from the V-F6 row set unless stated)
Row set + gate dates + `ProjectDependency` rows where both ends are in the visible set + `PlanningPeriod` rows in range. Default range: 2 months back to 10 months ahead.

## Settings (builder drawer)
Default zoom, grouping and toggles; range; print size A3.

## Interaction
Bar → project drill target. Tooltip on hover and focus. Rows are a keyboard-navigable list; arrows also described in each row's text ("needs D-04 ETL cut-over, 6 wd slack"). Reuse the P-3 Gantt layout helper and row rendering; do not add a Gantt library. "Export roadmap" = V-F8 print at A3. **Bars are not draggable here** (scenario mode is V-S2).

## Build
- **Backend**: a data provider registered in the widget registry under the key above, with its category, required permission and settings schema (validated when the dashboard is saved). It takes the `PortfolioScope` from the dashboard context and returns a DTO made for this widget. Reuse `IPortfolioAnalyticsService`; add a query only for what the row set does not hold.
- **Frontend**: a standalone component under `features/dashboard/widgets/`, registered in the frontend registry with its default size and settings form. Use `DashPanel`, `DashStat`, `RankedBars`, `DashLegend`, `chart-theme.ts` and the theme tokens; RAG colours come from one shared place (add `rag-colors` tokens once, in the first widget that needs them, and reuse). Loading, empty, "not configured" and error-with-retry states.
- **Tests**: provider unit tests against `PortfolioTestData` (expected figures; the hidden private project never counted; each setting); a component spec for rendering, the empty state and the emitted drill-down.

## Acceptance
- 100 projects render and scroll smoothly (virtualise rows if needed, without a new dependency).
- A dependency to a project outside the viewer's access draws nothing and is mentioned nowhere.
- Non-working days and planning periods line up with the same dates on the project Gantt.
- Works alone on a new dashboard, with and without a department in the context, in dark and light, at mobile width and by keyboard.
- The same figure shown by another widget of this series is identical for the same viewer and filters.

## Done when
- The acceptance points above hold, and the common acceptance criteria in `00-README.md` §5 hold.
- `dotnet build`, `dotnet test`, `ng build` pass with no new warnings.
- `status.md`, `BACKEND_OVERVIEW.md`, `FRONTEND_OVERVIEW.md` (and the entity / feature lists in `CLAUDE.md`) describe what was actually built.
- You finish with a short report: what was built, what differs from this prompt and why, any open point for the owner.
