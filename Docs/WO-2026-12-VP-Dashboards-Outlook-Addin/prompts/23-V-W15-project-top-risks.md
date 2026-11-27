# V-W15 — Widget `project.top-risks` — Top risks and issues

> **Work order:** WO-2026-12 — Executive Office dashboards and Outlook add-in
> **Package:** V-W15 · **Size:** S · **Depends on:** WO-2026-11 D-1 to D-5; V-F6, V-F8
> **Before anything else read:** `Docs/WO-2026-12-VP-Dashboards-Outlook-Addin/prompts/00-README.md` (rules, definitions, stop conditions), then `CLAUDE.md`, `status.md`, `BACKEND_OVERVIEW.md`, `FRONTEND_OVERVIEW.md`.
> Implement **this package only**. Do not start the next one.

## Widget
| | |
|---|---|
| Catalogue key | `project.top-risks` |
| Category | Project |
| Mockup | S3 Project drill-down — "Top risks and issues" |
| Default size | 1 column |
| Needs | a project in the context |
| Required permission | `Risks.View` |

## What it shows
Open risks and issues, highest score first: title, kind (risk / issue), impact band, owner, mitigation (first 120 characters).

## Data (server, from the V-F6 row set unless stated)
`ProjectRisk` open rows with rating and the new `Kind` (V-F1). If WO-2026-11 already added `risks.top-open`, extend that provider with a project filter and the kind column instead of writing a second one, and register this key as a preset of it.

## Settings (builder drawer)
Top N (default 3); kinds to include.

## Interaction
Row → risk detail.

## Build
- **Backend**: a data provider registered in the widget registry under the key above, with its category, required permission and settings schema (validated when the dashboard is saved). It takes the `PortfolioScope` from the dashboard context and returns a DTO made for this widget. Reuse `IPortfolioAnalyticsService`; add a query only for what the row set does not hold.
- **Frontend**: a standalone component under `features/dashboard/widgets/`, registered in the frontend registry with its default size and settings form. Use `DashPanel`, `DashStat`, `RankedBars`, `DashLegend`, `chart-theme.ts` and the theme tokens; RAG colours come from one shared place (add `rag-colors` tokens once, in the first widget that needs them, and reuse). Loading, empty, "not configured" and error-with-retry states.
- **Tests**: provider unit tests against `PortfolioTestData` (expected figures; the hidden private project never counted; each setting); a component spec for rendering, the empty state and the emitted drill-down.

## Acceptance
- No second counting rule for risk rating exists after this package.
- Works alone on a new dashboard, with and without a department in the context, in dark and light, at mobile width and by keyboard.
- The same figure shown by another widget of this series is identical for the same viewer and filters.

## Done when
- The acceptance points above hold, and the common acceptance criteria in `00-README.md` §5 hold.
- `dotnet build`, `dotnet test`, `ng build` pass with no new warnings.
- `status.md`, `BACKEND_OVERVIEW.md`, `FRONTEND_OVERVIEW.md` (and the entity / feature lists in `CLAUDE.md`) describe what was actually built.
- You finish with a short report: what was built, what differs from this prompt and why, any open point for the owner.
