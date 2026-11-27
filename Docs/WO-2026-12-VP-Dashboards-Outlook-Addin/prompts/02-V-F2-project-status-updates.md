# V-F2 — Project status updates and the stale rule

> **Work order:** WO-2026-12 — Executive Office dashboards and Outlook add-in
> **Package:** V-F2 · **Size:** M · **Depends on:** V-F1
> **Before anything else read:** `Docs/WO-2026-12-VP-Dashboards-Outlook-Addin/prompts/00-README.md` (rules, definitions, stop conditions), then `CLAUDE.md`, `status.md`, `BACKEND_OVERVIEW.md`, `FRONTEND_OVERVIEW.md`.
> Implement **this package only**. Do not start the next one.

## Why
Screens 2 and 3 show "last update", a stale flag, the latest status update (done / next / blockers), and each attention row needs an "issue" and an "ask". No such record exists; `ActionItemUpdate` is for action items only.

## Read first
`ActionItemUpdate` + `Features/ActionItemUpdates` (draft / submitted pattern to mirror), `IWorkflowNotificationHelper`, `EmailTemplateSeeder`, `IProjectAccessService`, the project page tabs created in P-3 / P-5.

## Data
`ProjectStatusUpdate`: `Id`, `ProjectId`, `ReportDate` (date), `Done` (≤ 4000), `Next` (≤ 4000), `Blockers` (≤ 4000), `HeadlineIssue` (≤ 200), `AskOfSponsor` (≤ 200), `ForecastEndDate` (`DateTime?`, copied to the project on submit when given), `Status` (Draft / Submitted), `SubmittedAt`, `AuthorUserId`, audit, `IsDeleted`. Query filter consistent with projects in private workspaces.

## Rules
- Draft: visible to and editable by its author only; may be deleted. Submit is one-way; a submitted update is read-only for everyone (409).
- Write: users who may manage the project. Read: everyone who may see the project.
- At most one draft per author per project.
- **Stale** as in README §4, threshold from `Portfolio:StaleAfterDays` (default 14) in `appsettings.json`, bound to an options class. Expose `lastStatusUpdateAt`, `daysSinceStatusUpdate` and `isStale` on the project response.
- **Request a status update**: `POST /api/projects/{id}/status-updates/request`. Allowed for the project's sponsors and managers, Admin, PMO Head, and any user who sees the project through a dashboard assigned to them (resolve with the D-1 service). Sends in-app + e-mail to the lead and additional project managers; never more than one request per project in 24 hours (409 with the time of the last one). New e-mail template `Project.StatusUpdateRequested`, every value HTML-encoded.
- Notify sponsors when an update is submitted (in-app only).

## API
`/api/projects/{id}/status-updates`: list (submitted + my draft), get latest, create draft, update draft, `POST /{updateId}/submit`, delete draft, and `/request` above.

## UI
A "Status updates" tab on the project page: the latest update on top, history below, a compose form (three text areas, headline issue, ask, optional new forecast finish) with Save draft / Submit and the same confirm text as action updates. A stale badge next to the project title.

## Acceptance
- Another user cannot see a draft; nobody can change a submitted update.
- Submitting with a forecast date updates the project's forecast through the V-F1 rule (and is refused on a non-working day).
- A second request inside 24 hours returns 409 and sends nothing.
- A viewer of a private-workspace project reads its updates; a non-member gets 404.

## Done when
- The acceptance points above hold, and the common acceptance criteria in `00-README.md` §5 hold.
- `dotnet build`, `dotnet test`, `ng build` pass with no new warnings.
- `status.md`, `BACKEND_OVERVIEW.md`, `FRONTEND_OVERVIEW.md` (and the entity / feature lists in `CLAUDE.md`) describe what was actually built.
- You finish with a short report: what was built, what differs from this prompt and why, any open point for the owner.
