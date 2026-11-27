# V-W03 — Widget `portfolio.attention` — Projects needing attention

> **Work order:** WO-2026-12 — Executive Office dashboards and Outlook add-in
> **Package:** V-W03 · **Size:** M · **Depends on:** WO-2026-11 D-1 to D-5; V-F6, V-F8
> **Before anything else read:** `Docs/WO-2026-12-VP-Dashboards-Outlook-Addin/prompts/00-README.md` (rules, definitions, stop conditions), then `CLAUDE.md`, `status.md`, `BACKEND_OVERVIEW.md`, `FRONTEND_OVERVIEW.md`.
> Implement **this package only**. Do not start the next one.

## Widget
| | |
|---|---|
| Catalogue key | `portfolio.attention` |
| Category | Portfolio |
| Mockup | S1 Portfolio overview — "Projects needing VP attention" |
| Default size | 2 columns |
| Needs | — |
| Required permission | `Projects.View` |

## What it shows
Red and amber projects, worst first (red before amber, then variance descending): code and name, department, type, RAG chip (colour + word), **issue**, **ask**, variance in working days. A header link "All red and amber".

## Data (server, from the V-F6 row set unless stated)
Issue = `HeadlineIssue` of the last submitted status update (V-F2); when empty, the title of the highest-scored open risk or issue; when none, the rule that made it red or amber in words ("3 overdue action items", "Past planned end"). Ask = title of the oldest pending decision (V-F3), else `AskOfSponsor` of the last status update, else blank.

## Settings (builder drawer)
Top N (default 5); include amber (default yes); columns.

## Interaction
Row → the project drill target (V-F8), else the project page. Header link → drill-down with the full list. The ask opens the decision when there is one and the viewer has `ProjectDecisions.View`.

## Build
- **Backend**: a data provider registered in the widget registry under the key above, with its category, required permission and settings schema (validated when the dashboard is saved). It takes the `PortfolioScope` from the dashboard context and returns a DTO made for this widget. Reuse `IPortfolioAnalyticsService`; add a query only for what the row set does not hold.
- **Frontend**: a standalone component under `features/dashboard/widgets/`, registered in the frontend registry with its default size and settings form. Use `DashPanel`, `DashStat`, `RankedBars`, `DashLegend`, `chart-theme.ts` and the theme tokens; RAG colours come from one shared place (add `rag-colors` tokens once, in the first widget that needs them, and reuse). Loading, empty, "not configured" and error-with-retry states.
- **Tests**: provider unit tests against `PortfolioTestData` (expected figures; the hidden private project never counted; each setting); a component spec for rendering, the empty state and the emitted drill-down.

## Acceptance
- Order and content match the fixture; a stale red project still shows its fallback issue text.
- Works alone on a new dashboard, with and without a department in the context, in dark and light, at mobile width and by keyboard.
- The same figure shown by another widget of this series is identical for the same viewer and filters.

## Done when
- The acceptance points above hold, and the common acceptance criteria in `00-README.md` §5 hold.
- `dotnet build`, `dotnet test`, `ng build` pass with no new warnings.
- `status.md`, `BACKEND_OVERVIEW.md`, `FRONTEND_OVERVIEW.md` (and the entity / feature lists in `CLAUDE.md`) describe what was actually built.
- You finish with a short report: what was built, what differs from this prompt and why, any open point for the owner.
