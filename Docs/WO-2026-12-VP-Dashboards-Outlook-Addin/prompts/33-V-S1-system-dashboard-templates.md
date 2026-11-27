# V-S1 — Five system dashboard templates and navigation

> **Work order:** WO-2026-12 — Executive Office dashboards and Outlook add-in
> **Package:** V-S1 · **Size:** M · **Depends on:** V-F8, V-W01 to V-W24
> **Before anything else read:** `Docs/WO-2026-12-VP-Dashboards-Outlook-Addin/prompts/00-README.md` (rules, definitions, stop conditions), then `CLAUDE.md`, `status.md`, `BACKEND_OVERVIEW.md`, `FRONTEND_OVERVIEW.md`.
> Implement **this package only**. Do not start the next one.

## Why
The mockup's five screens are delivered as dashboards assembled from the catalogue, so that an administrator can assign them, clone them and change them like any other dashboard.

## Read first
The D-3 seeder of the four existing system dashboards, the D-4 builder, V-F8.

## Build
Extend the idempotent seeder with five **system templates** (cannot be deleted; can be cloned; **not assigned to anyone and inactive until an administrator sets them up**):

| Template | Requires | Filter bar | Widgets in order |
|---|---|---|---|
| Executive portfolio overview | — | departments, type, RAG, period | `portfolio.stats` (active, health, milestones on time, behind schedule, office actions, decisions) · `portfolio.department-health` · `portfolio.attention` (2 col) + `office-actions.by-department` (1 col) · `portfolio.type-split` (1 col) + `portfolio.upcoming-gates` (2 col) |
| Department view | department | departments, type, RAG, period | `portfolio.stats` (projects, health, % vs plan, milestones 30 d, office actions, high risks) · `portfolio.project-table` · `portfolio.baseline-vs-forecast` (2 col) + `office-actions.list` (1 col) |
| Project drill-down | project | — | `project.header` · `project.stats` · `project.milestones` (2 col) + `project.progress-history` (1 col) · `project.decisions` + `project.top-risks` + `project.latest-status` |
| Office action tracker | — | departments | `office-actions.stats` · `office-actions.tracker` · `office-actions.by-owner` + `office-actions.by-source` + `office-actions.closed-per-week` |
| Portfolio roadmap | — | departments, RAG | `roadmap.gantt` · `roadmap.load-by-month` (2 col) + `roadmap.watch-list` (1 col) |

- Drill targets (V-F8) are pre-wired between the five: overview → department → project; "Open tracker" and "Open roadmap" links. Cloning a template as a set keeps the links pointing at the clones: add a builder action **"Create office dashboard set"** that clones the five, asks once for name prefix, root org unit, office group, workspaces and audience, and wires the clones together.
- Names are neutral ("Executive portfolio overview"), never "VP".
- A short admin guide `Docs/WO-2026-12-VP-Dashboards-Outlook-Addin/Office-Dashboards-Setup-Guide.md`: create the office group, run "Create office dashboard set", assign, check "Preview as".

## Acceptance
- On an upgraded database the five templates exist once, inactive and unassigned; existing users see no change.
- An admin creates a set for one office in one flow; a member of the audience lands on the overview and can walk overview → department → project → back with mouse and keyboard, seeing only permitted data.
- A second set for another office (other root org unit and group) shows different numbers from the same templates.

## Done when
- The acceptance points above hold, and the common acceptance criteria in `00-README.md` §5 hold.
- `dotnet build`, `dotnet test`, `ng build` pass with no new warnings.
- `status.md`, `BACKEND_OVERVIEW.md`, `FRONTEND_OVERVIEW.md` (and the entity / feature lists in `CLAUDE.md`) describe what was actually built.
- You finish with a short report: what was built, what differs from this prompt and why, any open point for the owner.
