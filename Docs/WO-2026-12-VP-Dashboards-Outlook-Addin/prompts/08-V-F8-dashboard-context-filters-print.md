# V-F8 — Dashboard context: filter bar, parameters, cross-dashboard drill, print

> **Work order:** WO-2026-12 — Executive Office dashboards and Outlook add-in
> **Package:** V-F8 · **Size:** L · **Depends on:** WO-2026-11 D-3, D-4, D-5; V-F6
> **Before anything else read:** `Docs/WO-2026-12-VP-Dashboards-Outlook-Addin/prompts/00-README.md` (rules, definitions, stop conditions), then `CLAUDE.md`, `status.md`, `BACKEND_OVERVIEW.md`, `FRONTEND_OVERVIEW.md`.
> Implement **this package only**. Do not start the next one.

## Why
The mockup's screens share a filter bar (department tabs, Type, RAG, Period, "Data as of"), open one another (overview → department → project), carry a breadcrumb and export to PDF. The D-3 renderer shows a grid of widgets with a workspace selector; it has no shared filters, no parameters and no navigation between dashboards.

## Read first
The D-3 `DashboardViewComponent`, the D-2 widget contract and registry, the D-5 drill-down stack, the D-4 builder, `_dash-page.scss`, the existing print style used for the milestones "Print to PDF".

## Build
1. **Dashboard context** — a signal-based `DashboardContextService` provided by the renderer: `{ dashboardId, workspaceIds, departmentId, projectId, projectType, rag, period, asOf }`. It is mirrored in the URL query string (`?dept=&project=&type=&rag=&period=`) so a filtered view can be bookmarked, and sent with every widget data request as the V-F6 `PortfolioScope`. Extend the widget contract so a widget receives the context next to `{ config, workspaceIds }`; existing widgets ignore it and keep working unchanged.
2. **Filter bar** — a dashboard setting chosen in the builder (which controls to show: department tabs, type, RAG, period). Department tabs list "All departments" + the direct children of the dashboard's root org unit that contain at least one visible project. Shows "Data as of <time>" and a refresh button.
3. **Parameterised dashboards** — a dashboard may declare that it **requires** a department or a project. Opened without it, it shows a picker (departments / projects in scope) instead of empty widgets. Add the declaration to the D-1 entity (additive) and to the builder's Details step.
4. **Cross-dashboard drill** — a new drill target "open dashboard": `{ dashboardKey or id, params }`. Each dashboard may name, in its settings, the dashboard to open for a department and for a project ("drill targets"). When no target is set, a department click filters the current dashboard and a project click opens the project page — both always work.
5. **Breadcrumb** — "<dashboard> › <department> › <project>" built from the navigation, each step clickable.
6. **Print / PDF** — a "Print" action on the renderer and a print style sheet: A4 landscape by default, per-dashboard setting for A3 landscape; nav, filter controls and buttons hidden; a header line with dashboard name, filters and "Data as of"; widgets do not split across pages; charts keep their size. No PDF library.
7. **Row click → page filter** — a widget may emit a "set context" event (for example the strategic / operational table sets `projectType`).

## Rules
- A parameter naming a department or project the viewer may not see behaves exactly like one that does not exist (picker shown, no name leaked).
- Filters narrow; they never widen the dashboard's scope.

## Acceptance
- Reloading or sharing the URL restores the same filters; another user opening it sees only what they may see.
- The four existing system dashboards from D-3 look and count as before.
- Keyboard: tabs, filters and breadcrumb are reachable and announced; print preview of a dashboard with six widgets fits the page without cut-off content, in light theme regardless of the user's theme.

## Done when
- The acceptance points above hold, and the common acceptance criteria in `00-README.md` §5 hold.
- `dotnet build`, `dotnet test`, `ng build` pass with no new warnings.
- `status.md`, `BACKEND_OVERVIEW.md`, `FRONTEND_OVERVIEW.md` (and the entity / feature lists in `CLAUDE.md`) describe what was actually built.
- You finish with a short report: what was built, what differs from this prompt and why, any open point for the owner.
