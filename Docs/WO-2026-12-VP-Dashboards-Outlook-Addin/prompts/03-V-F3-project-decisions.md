# V-F3 — Decisions needed on a project

> **Work order:** WO-2026-12 — Executive Office dashboards and Outlook add-in
> **Package:** V-F3 · **Size:** M · **Depends on:** V-F1
> **Before anything else read:** `Docs/WO-2026-12-VP-Dashboards-Outlook-Addin/prompts/00-README.md` (rules, definitions, stop conditions), then `CLAUDE.md`, `status.md`, `BACKEND_OVERVIEW.md`, `FRONTEND_OVERVIEW.md`.
> Implement **this package only**. Do not start the next one.

## Why
The portfolio screen counts "decisions pending", the attention table shows the "ask", and the project screen lists decisions with the date raised and the impact of a late answer. There is no decision record today.

## Read first
`Features/ProjectRisks` + `ProjectRisksController` (CRUD shape to copy), `IProjectAccessService`, `IWorkflowNotificationHelper`, the project people picker (`app-person-picker`).

## Data
`ProjectDecision`: `Id`, `ProjectId`, `DecisionCode` (DEC-NNN per project, same generator pattern as `RiskCode`), `Title` (≤ 255), `Description` (≤ 4000), `ImpactIfLate` (≤ 500), `NeededByDate` (`DateTime?`), `MilestoneId` (`Guid?`, a milestone of the same project that waits on it), `DecisionOwnerUserId` (who must decide; any active user), `Status` (Pending / Decided / Withdrawn), `RaisedByUserId`, `RaisedAt`, `DecisionText` (≤ 4000), `DecidedByUserId`, `DecidedAt`, audit, `IsDeleted`. Private-workspace query filter like `ProjectRisk`.

## Rules
- Raise, edit, withdraw: users who may manage the project. Read: everyone who may see the project.
- **Decide** (`POST /{decisionId}/decide`, text required): the decision owner, the project's sponsors, Admin. The decision owner gains read access to that project while the decision is pending, through the same mechanism P-2 used for project people (do not open the rest of the workspace). If that cannot be done without touching the private-workspace filters in a new way, stop and ask.
- A decided or withdrawn decision is read-only. Decided decisions keep their history.
- Notifications: to the decision owner when raised and when the needed-by date passes (the reminder runs in the job added by V-F7; here only the template and the method); to the raiser and project managers when decided. E-mail templates `Project.DecisionRaised`, `Project.DecisionDecided`.
- New permission area `ProjectDecisions` (View / Create / Edit / Delete / Decide), next free area GUID after the ones in `PermissionCatalogSeeder`; default grants mirror the `Risks` area, plus `Decide` for Admin, PMO Head and Project Sponsor.

## API and UI
- `/api/projects/{id}/decisions` (list with status filter, get, create, update, withdraw, decide, delete, restore) and `GET /api/decisions/mine` (pending decisions where the caller is the decision owner).
- A "Decisions" tab on the project page with the pending count on the tab; a form drawer; a "Decide" dialog. My Approvals page: a "Decisions" tab listing `mine`.

## Acceptance
- A decision owner outside a private workspace sees that project and the decision only, and loses the access when the decision is decided or withdrawn (integration test).
- A team member cannot raise or edit; a sponsor can decide.
- Counts returned by the stats endpoint equal the rows of the list for the same viewer.

## Done when
- The acceptance points above hold, and the common acceptance criteria in `00-README.md` §5 hold.
- `dotnet build`, `dotnet test`, `ng build` pass with no new warnings.
- `status.md`, `BACKEND_OVERVIEW.md`, `FRONTEND_OVERVIEW.md` (and the entity / feature lists in `CLAUDE.md`) describe what was actually built.
- You finish with a short report: what was built, what differs from this prompt and why, any open point for the owner.
