# V-F6 — Portfolio calculation engine and scope

> **Work order:** WO-2026-12 — Executive Office dashboards and Outlook add-in
> **Package:** V-F6 · **Size:** L · **Depends on:** V-F1 to V-F5, WO-2026-11 D-2
> **Before anything else read:** `Docs/WO-2026-12-VP-Dashboards-Outlook-Addin/prompts/00-README.md` (rules, definitions, stop conditions), then `CLAUDE.md`, `status.md`, `BACKEND_OVERVIEW.md`, `FRONTEND_OVERVIEW.md`.
> Implement **this package only**. Do not start the next one.

## Why
Every widget in this series reads the same figures: RAG, % complete against plan, variance in working days, milestone forecast status, department roll-ups. They must come from one place so that two widgets never disagree.

## Read first
`DashboardRules.cs` (style: pure functions + matching database predicates), `DashboardAnalyticsService`, `DashboardAnalyticsServiceTests`, `IWorkCalendarService`, `IWorkspaceAccessResolver`, `IProjectAccessService`, the D-1 dashboard entities and the D-2 widget data provider contract, `OrgUnit` (hierarchy helpers already used by org-unit scoping).

## Build
1. **`PortfolioRules`** (static, in `DashboardRules.cs` or a sibling file in the same folder): every definition in README §4 as a pure function with unit tests — `EffectiveForecastFinish`, `VarianceWorkingDays`, `PlannedPercent(asOf)`, `Rag`, `MilestoneForecastStatus`, `IsStale`, `Trend`, dependency `Slack`. Thresholds come in as parameters from an options class `PortfolioOptions` (`RedAboveWorkingDays` 10, `StaleAfterDays` 14, `DecisionOldAfterDays` 10, `MilestoneLoadThreshold` 8) bound to the `Portfolio` section of `appsettings.json`.
2. **`PortfolioScope`**: dashboard id (to read its workspaces, root org unit and office group), optional department org unit id, optional project id, project type (All / Strategic / Operational), RAG (All / Red / Amber / Green), period (ThisMonth / ThisQuarter / ThisYear / Custom from–to), `asOf`. A resolver turns it into the set of visible project ids for the caller, applying README §4 "Portfolio scope" and "Department".
3. **Dashboard settings**: add to the D-1 `Dashboard` entity (additive) `RootOrgUnitId` (`Guid?`) and `OfficeGroupId` (`Guid?`), exposed in the admin API and in the builder's Details step (org-unit picker, group picker). Both optional; widgets that need one show a clear "not configured" state when it is missing.
4. **`IPortfolioAnalyticsService`** with one method that loads, for a scope, the **portfolio row set**: per project — id, code, name, type, department, lead PM, status, RAG, % complete, planned %, baseline and forecast finish, variance, next open milestone (name, due, forecast status), counts of milestones (total, done, late, at risk, due in 30 days, due in the period, done on time in the period), open and overdue action items, open office actions, open risks by rating and kind, pending decisions (count, oldest age), last status update (date, headline issue, ask), stale flag. One round of queries per call, no N+1; cache per user + scope for 60 seconds (`IMemoryCache`), invalidated by nothing (short TTL only).
5. **Department roll-up** from the row set: head, strategic / operational counts, RAG mix, mean % complete and planned %, milestones next 30 days, overdue milestones, office actions open and overdue, trend against the previous snapshot (returns "none" until V-F7 has two snapshots).
6. **Test fixture** `PortfolioTestData` in `ActionTracker.Tests`: three departments under one root org unit, about 14 projects shaped like the mockup (some late, some stale, gates, a dependency, a private workspace with one project the test user cannot see), with the expected figures written down in the fixture. Every widget package asserts against it.

## Rules
- `ProjectRules` and `ActionItemRules` members that exist today are not edited. RAG *uses* the existing health as an input.
- Working-day maths uses each project's own workspace calendar.
- Nothing here is a controller for one widget; widgets get their data through the D-2 provider contract and call this service.

## Acceptance
- Unit tests cover each rule at its boundaries (variance 0, 1, 10, 11; a project with no milestones; a completed project; an On Hold project).
- The row set for the fixture equals the expected figures; the hidden private project appears in no count.
- A scope with a department the dashboard's root does not contain returns nothing, not an error.
- Two calls inside 60 seconds hit the database once.

## Done when
- The acceptance points above hold, and the common acceptance criteria in `00-README.md` §5 hold.
- `dotnet build`, `dotnet test`, `ng build` pass with no new warnings.
- `status.md`, `BACKEND_OVERVIEW.md`, `FRONTEND_OVERVIEW.md` (and the entity / feature lists in `CLAUDE.md`) describe what was actually built.
- You finish with a short report: what was built, what differs from this prompt and why, any open point for the owner.
