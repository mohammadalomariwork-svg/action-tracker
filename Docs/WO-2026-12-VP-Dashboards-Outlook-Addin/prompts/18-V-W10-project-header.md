# V-W10 — Widget `project.header` — Project header and actions

> **Work order:** WO-2026-12 — Executive Office dashboards and Outlook add-in
> **Package:** V-W10 · **Size:** M · **Depends on:** WO-2026-11 D-1 to D-5; V-F6, V-F8
> **Before anything else read:** `Docs/WO-2026-12-VP-Dashboards-Outlook-Addin/prompts/00-README.md` (rules, definitions, stop conditions), then `CLAUDE.md`, `status.md`, `BACKEND_OVERVIEW.md`, `FRONTEND_OVERVIEW.md`.
> Implement **this package only**. Do not start the next one.

## Widget
| | |
|---|---|
| Catalogue key | `project.header` |
| Category | Project |
| Mockup | S3 Project drill-down — "Title block" |
| Default size | full width |
| Needs | a project in the context (V-F8) |
| Required permission | `Projects.View` |

## What it shows
Project code and name, RAG chip, type chip, then one line: sponsor(s), lead PM, strategic objective, baseline number (BLn), last status update with "stale: N days" when stale. Buttons: **Open full project plan**, **Request status update**, **Assign action**.

## Data (server, from the V-F6 row set unless stated)
Project by id through the normal project read (so visibility and flags are the server's), plus RAG and stale from V-F6.

## Settings (builder drawer)
Buttons to show.

## Interaction
Open plan → project page (Gantt). Request status update → V-F2 request endpoint, confirm first, show the server's 409 text when one was sent in the last 24 hours; hidden when the server says the viewer may not request. Assign action → the existing action-item create drawer pre-filled with the project (and its default milestone, P-4); offered only where the server would accept it (use the project's `canManage` flag and the current project-status rule — do not re-implement the rule).

## Build
- **Backend**: a data provider registered in the widget registry under the key above, with its category, required permission and settings schema (validated when the dashboard is saved). It takes the `PortfolioScope` from the dashboard context and returns a DTO made for this widget. Reuse `IPortfolioAnalyticsService`; add a query only for what the row set does not hold.
- **Frontend**: a standalone component under `features/dashboard/widgets/`, registered in the frontend registry with its default size and settings form. Use `DashPanel`, `DashStat`, `RankedBars`, `DashLegend`, `chart-theme.ts` and the theme tokens; RAG colours come from one shared place (add `rag-colors` tokens once, in the first widget that needs them, and reuse). Loading, empty, "not configured" and error-with-retry states.
- **Tests**: provider unit tests against `PortfolioTestData` (expected figures; the hidden private project never counted; each setting); a component spec for rendering, the empty state and the emitted drill-down.

## Acceptance
- A viewer without rights sees no button that would end in 403.
- With no project in the context the V-F8 picker is shown.
- Works alone on a new dashboard, with and without a department in the context, in dark and light, at mobile width and by keyboard.
- The same figure shown by another widget of this series is identical for the same viewer and filters.

## Done when
- The acceptance points above hold, and the common acceptance criteria in `00-README.md` §5 hold.
- `dotnet build`, `dotnet test`, `ng build` pass with no new warnings.
- `status.md`, `BACKEND_OVERVIEW.md`, `FRONTEND_OVERVIEW.md` (and the entity / feature lists in `CLAUDE.md`) describe what was actually built.
- You finish with a short report: what was built, what differs from this prompt and why, any open point for the owner.
