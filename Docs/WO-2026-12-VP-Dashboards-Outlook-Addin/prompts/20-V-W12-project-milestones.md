# V-W12 — Widget `project.milestones` — Project milestones with variance

> **Work order:** WO-2026-12 — Executive Office dashboards and Outlook add-in
> **Package:** V-W12 · **Size:** S · **Depends on:** WO-2026-11 D-1 to D-5; V-F6, V-F8
> **Before anything else read:** `Docs/WO-2026-12-VP-Dashboards-Outlook-Addin/prompts/00-README.md` (rules, definitions, stop conditions), then `CLAUDE.md`, `status.md`, `BACKEND_OVERVIEW.md`, `FRONTEND_OVERVIEW.md`.
> Implement **this package only**. Do not start the next one.

## Widget
| | |
|---|---|
| Catalogue key | `project.milestones` |
| Category | Project |
| Mockup | S3 Project drill-down — "Milestones" |
| Default size | 2 columns |
| Needs | a project in the context |
| Required permission | `Milestones.View` |

## What it shows
Milestone (diamond for gates), baseline due, forecast due, variance (WD), % done, status in words (Done, Done late, Overdue, At risk, Late, On track, Not started).

## Data (server, from the V-F6 row set unless stated)
Live milestones in sequence order with `PortfolioRules.MilestoneForecastStatus`. Planned due is used and labelled "planned" where there is no baseline.

## Settings (builder drawer)
Show only gates and key milestones; show phase column.

## Interaction
Row → project page at that milestone.

## Build
- **Backend**: a data provider registered in the widget registry under the key above, with its category, required permission and settings schema (validated when the dashboard is saved). It takes the `PortfolioScope` from the dashboard context and returns a DTO made for this widget. Reuse `IPortfolioAnalyticsService`; add a query only for what the row set does not hold.
- **Frontend**: a standalone component under `features/dashboard/widgets/`, registered in the frontend registry with its default size and settings form. Use `DashPanel`, `DashStat`, `RankedBars`, `DashLegend`, `chart-theme.ts` and the theme tokens; RAG colours come from one shared place (add `rag-colors` tokens once, in the first widget that needs them, and reuse). Loading, empty, "not configured" and error-with-retry states.
- **Tests**: provider unit tests against `PortfolioTestData` (expected figures; the hidden private project never counted; each setting); a component spec for rendering, the empty state and the emitted drill-down.

## Acceptance
- Statuses match the fixture for a project with one done-late, one overdue and one at-risk milestone.
- Works alone on a new dashboard, with and without a department in the context, in dark and light, at mobile width and by keyboard.
- The same figure shown by another widget of this series is identical for the same viewer and filters.

## Done when
- The acceptance points above hold, and the common acceptance criteria in `00-README.md` §5 hold.
- `dotnet build`, `dotnet test`, `ng build` pass with no new warnings.
- `status.md`, `BACKEND_OVERVIEW.md`, `FRONTEND_OVERVIEW.md` (and the entity / feature lists in `CLAUDE.md`) describe what was actually built.
- You finish with a short report: what was built, what differs from this prompt and why, any open point for the owner.
