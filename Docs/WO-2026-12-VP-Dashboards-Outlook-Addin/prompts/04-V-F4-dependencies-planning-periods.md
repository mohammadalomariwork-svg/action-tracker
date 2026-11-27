# V-F4 — Cross-project dependencies and planning periods (freeze bands)

> **Work order:** WO-2026-12 — Executive Office dashboards and Outlook add-in
> **Package:** V-F4 · **Size:** M · **Depends on:** V-F1
> **Before anything else read:** `Docs/WO-2026-12-VP-Dashboards-Outlook-Addin/prompts/00-README.md` (rules, definitions, stop conditions), then `CLAUDE.md`, `status.md`, `BACKEND_OVERVIEW.md`, `FRONTEND_OVERVIEW.md`.
> Implement **this package only**. Do not start the next one.

## Why
The roadmap draws arrows between projects, shades freeze periods ("Year-end change freeze", "Ramadan") and the watch-list reasons about both. Neither exists. WO-2026-11 excluded dependencies *that drive scheduling*; this package adds **display-only** links.

## Read first
`Features/Calendars` and the Calendars admin page (C-1, C-2), `Milestone.cs`, `IProjectAccessService`, the project page.

## Data
- `ProjectDependency`: `Id`, `PredecessorProjectId`, `PredecessorMilestoneId` (`Guid?`), `SuccessorProjectId`, `SuccessorMilestoneId` (`Guid?`), `Note` (≤ 500), audit, `IsDeleted`. Unique on the four ids among live rows.
- `PlanningPeriod`: `Id`, `Name` (≤ 100), `StartDate`, `EndDate` (`DateOnly`, inclusive), `Kind` (`Freeze = 0`, `Information = 1`), `WorkCalendarId` (`Guid?`; null = applies to every calendar), `IsActive`, audit, `IsDeleted`.

## Rules
- A dependency never moves a date. It is a statement "successor needs predecessor".
- Create / delete: a user who may manage the **successor** project and may see the predecessor. A milestone id must belong to its project. No self-link, no duplicate, no cycle (check the project-level graph; 409 naming the cycle).
- Read: a dependency is returned only when the caller may see **both** ends. Never return a placeholder for a hidden end.
- Planning periods: `EndDate ≥ StartDate`; they do **not** change working-day maths (holidays remain calendar exceptions). Managed with the `Calendars` permissions.
- **Slack** for a dependency = working days from the predecessor's effective forecast date (milestone forecast, else the project's forecast finish) to the successor's date (milestone baseline or planned start, else project start). Negative slack = conflict. Put the function in `PortfolioRules` if V-F6 exists already, otherwise as an internal static helper that V-F6 will move.

## API and UI
- `/api/projects/{id}/dependencies` (list both directions, create, delete); `/api/planning-periods` (list with `from` / `to`, CRUD).
- Project page: a "Dependencies" block (needs / needed by) with a project + milestone picker limited to projects the user can see; arrows on the project Gantt for links inside the visible set.
- Calendars admin page: a "Planning periods" tab.

## Acceptance
- A cycle A → B → A is refused.
- A user who cannot see project B gets no trace of A → B (integration test on a private workspace).
- Deleting a milestone soft-deletes the dependencies that point at it.

## Done when
- The acceptance points above hold, and the common acceptance criteria in `00-README.md` §5 hold.
- `dotnet build`, `dotnet test`, `ng build` pass with no new warnings.
- `status.md`, `BACKEND_OVERVIEW.md`, `FRONTEND_OVERVIEW.md` (and the entity / feature lists in `CLAUDE.md`) describe what was actually built.
- You finish with a short report: what was built, what differs from this prompt and why, any open point for the owner.
