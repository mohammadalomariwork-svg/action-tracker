# V-S2 — Roadmap scenario mode (what-if) — optional, last

> **Work order:** WO-2026-12 — Executive Office dashboards and Outlook add-in
> **Package:** V-S2 · **Size:** L · **Depends on:** V-W22, V-W23, V-W24
> **Before anything else read:** `Docs/WO-2026-12-VP-Dashboards-Outlook-Addin/prompts/00-README.md` (rules, definitions, stop conditions), then `CLAUDE.md`, `status.md`, `BACKEND_OVERVIEW.md`, `FRONTEND_OVERVIEW.md`.
> Implement **this package only**. Do not start the next one.

## Why
The roadmap mockup has a "Scenario mode (what-if)" button: move dates to test a plan without changing the live plans.

## Scope (owner default: nothing is saved)
- A toggle on `roadmap.gantt`. While on: a clear banner "Scenario — not saved, live plans are unchanged", and project bars' forecast finish and gate diamonds can be moved (drag with `@angular/cdk/drag-drop`, and by keyboard: select, arrow keys move by a working day, Shift+arrow by a week), snapping to working days of the project's calendar.
- Variance, the load table and the watch-list are recalculated for the scenario by the **server**: `POST /api/dashboards/portfolio/scenario` takes the scope and a list of `{ projectId or milestoneId, newForecastDate }` (max 200) and returns the same DTOs as the three widgets, computed with `PortfolioRules` on the overridden dates. It writes nothing and is not cached. The same access rules apply: an override for a project the caller cannot see is ignored.
- Changed bars show the original position as a ghost outline. "Reset" restores everything. "Print" prints the scenario with the banner. Leaving the page discards it (confirm when there are changes).
- No endpoint in this package changes a project, a milestone or a snapshot.

## Acceptance
- After any scenario session the database is byte-for-byte unchanged (integration test comparing the affected tables before and after).
- A viewer without edit rights can still run a scenario (it is read-only analysis).
- Everything can be done with the keyboard; moves are announced ("D-01 forecast finish moved to 21 December, 6 working days later").

## Done when
- The acceptance points above hold, and the common acceptance criteria in `00-README.md` §5 hold.
- `dotnet build`, `dotnet test`, `ng build` pass with no new warnings.
- `status.md`, `BACKEND_OVERVIEW.md`, `FRONTEND_OVERVIEW.md` (and the entity / feature lists in `CLAUDE.md`) describe what was actually built.
- You finish with a short report: what was built, what differs from this prompt and why, any open point for the owner.
