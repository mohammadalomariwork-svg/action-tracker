# V-W08 — Widget `portfolio.baseline-vs-forecast` — Baseline vs forecast mini-roadmap

> **Work order:** WO-2026-12 — Executive Office dashboards and Outlook add-in
> **Package:** V-W08 · **Size:** M · **Depends on:** WO-2026-11 D-1 to D-5; V-F6, V-F8
> **Before anything else read:** `Docs/WO-2026-12-VP-Dashboards-Outlook-Addin/prompts/00-README.md` (rules, definitions, stop conditions), then `CLAUDE.md`, `status.md`, `BACKEND_OVERVIEW.md`, `FRONTEND_OVERVIEW.md`.
> Implement **this package only**. Do not start the next one.

## Widget
| | |
|---|---|
| Catalogue key | `portfolio.baseline-vs-forecast` |
| Category | Portfolio |
| Mockup | S2 Department view — "Roadmap: baseline vs forecast" |
| Default size | 2 columns |
| Needs | — |
| Required permission | `Projects.View` |

## What it shows
One line per project over a month scale: a grey bar for baseline start → finish, a bar for start → forecast finish, the part beyond baseline in the warning colour, a today line. Legend below.

## Data (server, from the V-F6 row set unless stated)
Baseline and forecast dates per project from the row set. Range: from the earliest start to the latest finish in scope, capped by the setting.

## Settings (builder drawer)
Months back and ahead (default 1 back, 7 ahead); top N projects (default 10, worst variance first).

## Interaction
Bar → project drill target. Tooltip (hover and focus): dates and variance in working days. Reuse the geometry helper of the project Gantt (`gantt-layout.util.ts` from P-3); do not write a second date-to-pixel routine.

## Build
- **Backend**: a data provider registered in the widget registry under the key above, with its category, required permission and settings schema (validated when the dashboard is saved). It takes the `PortfolioScope` from the dashboard context and returns a DTO made for this widget. Reuse `IPortfolioAnalyticsService`; add a query only for what the row set does not hold.
- **Frontend**: a standalone component under `features/dashboard/widgets/`, registered in the frontend registry with its default size and settings form. Use `DashPanel`, `DashStat`, `RankedBars`, `DashLegend`, `chart-theme.ts` and the theme tokens; RAG colours come from one shared place (add `rag-colors` tokens once, in the first widget that needs them, and reuse). Loading, empty, "not configured" and error-with-retry states.
- **Tests**: provider unit tests against `PortfolioTestData` (expected figures; the hidden private project never counted; each setting); a component spec for rendering, the empty state and the emitted drill-down.

## Acceptance
- A project without a baseline shows only the forecast bar and the text "no baseline".
- Every bar has a text alternative (name, dates, variance).
- Works alone on a new dashboard, with and without a department in the context, in dark and light, at mobile width and by keyboard.
- The same figure shown by another widget of this series is identical for the same viewer and filters.

## Done when
- The acceptance points above hold, and the common acceptance criteria in `00-README.md` §5 hold.
- `dotnet build`, `dotnet test`, `ng build` pass with no new warnings.
- `status.md`, `BACKEND_OVERVIEW.md`, `FRONTEND_OVERVIEW.md` (and the entity / feature lists in `CLAUDE.md`) describe what was actually built.
- You finish with a short report: what was built, what differs from this prompt and why, any open point for the owner.
