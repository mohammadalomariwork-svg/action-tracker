# V-W11 — Widget `project.stats` — Project KPI tiles

> **Work order:** WO-2026-12 — Executive Office dashboards and Outlook add-in
> **Package:** V-W11 · **Size:** S · **Depends on:** WO-2026-11 D-1 to D-5; V-F6, V-F8
> **Before anything else read:** `Docs/WO-2026-12-VP-Dashboards-Outlook-Addin/prompts/00-README.md` (rules, definitions, stop conditions), then `CLAUDE.md`, `status.md`, `BACKEND_OVERVIEW.md`, `FRONTEND_OVERVIEW.md`.
> Implement **this package only**. Do not start the next one.

## Widget
| | |
|---|---|
| Catalogue key | `project.stats` |
| Category | Project |
| Mockup | S3 Project drill-down — "KPI tile row" |
| Default size | full width |
| Needs | a project in the context |
| Required permission | `Projects.View` |

## What it shows
Six tiles: % complete vs plan ("planned P% · N pts behind"); last status update ("N days ago · flagged stale"); forecast finish ("+N wd vs baseline <date>"); milestones ("X of Y" done, "N late, N at risk"); open actions ("N overdue"); risks / issues ("R / I", "N high").

## Data (server, from the V-F6 row set unless stated)
The project's row from the V-F6 row set.

## Settings (builder drawer)
Tiles to show and order.

## Interaction
Milestones → project milestones; open actions → action items of the project; risks / issues → risk register filtered. Tiles needing a permission the viewer lacks are hidden.

## Build
- **Backend**: a data provider registered in the widget registry under the key above, with its category, required permission and settings schema (validated when the dashboard is saved). It takes the `PortfolioScope` from the dashboard context and returns a DTO made for this widget. Reuse `IPortfolioAnalyticsService`; add a query only for what the row set does not hold.
- **Frontend**: a standalone component under `features/dashboard/widgets/`, registered in the frontend registry with its default size and settings form. Use `DashPanel`, `DashStat`, `RankedBars`, `DashLegend`, `chart-theme.ts` and the theme tokens; RAG colours come from one shared place (add `rag-colors` tokens once, in the first widget that needs them, and reuse). Loading, empty, "not configured" and error-with-retry states.
- **Tests**: provider unit tests against `PortfolioTestData` (expected figures; the hidden private project never counted; each setting); a component spec for rendering, the empty state and the emitted drill-down.

## Acceptance
- Values equal the same project's row in V-W07.
- Works alone on a new dashboard, with and without a department in the context, in dark and light, at mobile width and by keyboard.
- The same figure shown by another widget of this series is identical for the same viewer and filters.

## Done when
- The acceptance points above hold, and the common acceptance criteria in `00-README.md` §5 hold.
- `dotnet build`, `dotnet test`, `ng build` pass with no new warnings.
- `status.md`, `BACKEND_OVERVIEW.md`, `FRONTEND_OVERVIEW.md` (and the entity / feature lists in `CLAUDE.md`) describe what was actually built.
- You finish with a short report: what was built, what differs from this prompt and why, any open point for the owner.
