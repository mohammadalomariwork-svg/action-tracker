# V-W04 — Widget `office-actions.by-department` — Office actions by department with ageing

> **Work order:** WO-2026-12 — Executive Office dashboards and Outlook add-in
> **Package:** V-W04 · **Size:** M · **Depends on:** WO-2026-11 D-1 to D-5; V-F6, V-F8
> **Before anything else read:** `Docs/WO-2026-12-VP-Dashboards-Outlook-Addin/prompts/00-README.md` (rules, definitions, stop conditions), then `CLAUDE.md`, `status.md`, `BACKEND_OVERVIEW.md`, `FRONTEND_OVERVIEW.md`.
> Implement **this package only**. Do not start the next one.

## Widget
| | |
|---|---|
| Catalogue key | `office-actions.by-department` |
| Category | Office actions |
| Mockup | S1 Portfolio overview — "Actions assigned by VP Office" |
| Default size | 1 column |
| Needs | office group; root org unit |
| Required permission | `ActionItems.View` |

## What it shows
A table per department: open, overdue, due this week, on-time closure %; a total row. Below it, four horizontal bars for the age of open office actions: 0–7, 8–30, 31–60, 60+ days, with counts. Header link "Open tracker".

## Data (server, from the V-F6 row set unless stated)
Office scope and rules from V-F5. On-time % over items closed in the last 90 days (setting). "Due this week" uses Monday–Sunday of the viewer's current week in the default calendar's time zone.

## Settings (builder drawer)
On-time window in days (default 90); show ageing bars; dashboard to open for "Open tracker".

## Interaction
Every count and bar drills to the action items behind it (server-paged). Department name sets the department filter.

## Build
- **Backend**: a data provider registered in the widget registry under the key above, with its category, required permission and settings schema (validated when the dashboard is saved). It takes the `PortfolioScope` from the dashboard context and returns a DTO made for this widget. Reuse `IPortfolioAnalyticsService`; add a query only for what the row set does not hold.
- **Frontend**: a standalone component under `features/dashboard/widgets/`, registered in the frontend registry with its default size and settings form. Use `DashPanel`, `DashStat`, `RankedBars`, `DashLegend`, `chart-theme.ts` and the theme tokens; RAG colours come from one shared place (add `rag-colors` tokens once, in the first widget that needs them, and reuse). Loading, empty, "not configured" and error-with-retry states.
- **Tests**: provider unit tests against `PortfolioTestData` (expected figures; the hidden private project never counted; each setting); a component spec for rendering, the empty state and the emitted drill-down.

## Acceptance
- Totals equal the sum of rows plus items with no department (shown as "Other" when any).
- Bars add up to the open total.
- Works alone on a new dashboard, with and without a department in the context, in dark and light, at mobile width and by keyboard.
- The same figure shown by another widget of this series is identical for the same viewer and filters.

## Done when
- The acceptance points above hold, and the common acceptance criteria in `00-README.md` §5 hold.
- `dotnet build`, `dotnet test`, `ng build` pass with no new warnings.
- `status.md`, `BACKEND_OVERVIEW.md`, `FRONTEND_OVERVIEW.md` (and the entity / feature lists in `CLAUDE.md`) describe what was actually built.
- You finish with a short report: what was built, what differs from this prompt and why, any open point for the owner.
