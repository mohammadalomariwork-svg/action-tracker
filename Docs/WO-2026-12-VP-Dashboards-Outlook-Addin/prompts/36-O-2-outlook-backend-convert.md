# O-2 — Add-in API: convert an e-mail into an action item or a project

> **Work order:** WO-2026-12 — Executive Office dashboards and Outlook add-in
> **Package:** O-2 · **Size:** M · **Depends on:** O-1, V-F5
> **Before anything else read:** `Docs/WO-2026-12-VP-Dashboards-Outlook-Addin/prompts/00-README.md` (rules, definitions, stop conditions), then `CLAUDE.md`, `status.md`, `BACKEND_OVERVIEW.md`, `FRONTEND_OVERVIEW.md`.
> Implement **this package only**. Do not start the next one.

## Why
One request must create the record from the e-mail, attach the chosen files and remember the link, with every existing rule still applied.

## Read first
`ActionItemService.CreateAsync`, `ProjectService.CreateAsync`, `ProjectWorkflowService.SendProjectCreatedNotificationsAsync`, `DocumentService` (limits, allowed extensions, `EnsureScopedWriteAsync`), `ActionItemCreateDto`, the project create DTO and their validators.

## API (multipart/form-data: a JSON part `payload` + file parts)
- `POST /api/outlook-addin/action-items` — payload: the fields of the existing action-item create DTO (workspace, optional project and milestone, title, description, priority, assignees, planned start, due date, tags) + `email` { internetMessageId, conversationId, subject, senderName, senderEmail, receivedAt } + `attachEmailBody` (bool).
- `POST /api/outlook-addin/projects` — payload: the fields of the existing project create DTO (workspace, name, description, type, lead PM, sponsors, planned dates, priority, internal classification, strategic objective when Strategic) + the same `email` block.
- "Add an action item to an existing project" is the first endpoint with `projectId` set.

## Rules
- Build the existing create DTO and call the **existing service method**. Do not copy validation. Everything it enforces applies: permissions, ownership rule, calendar rule, milestone date window, project-status rule, notifications.
- Action items get `Source = Email`, `SourceReference = "<sender name>, <received date>"`, `SourceDate = receivedAt`. Projects have no source field; the link row carries the origin.
- The e-mail body arrives as plain text made by the add-in. Server side: strip any remaining markup, normalise line breaks, cut to the description's maximum length with "… (truncated, full e-mail attached)" and force `attachEmailBody` in that case. Never store or render e-mail HTML.
- `attachEmailBody`: store the full text (headers From / To / Cc / Date / Subject + body) as a `.txt` document on the record.
- Files go through `DocumentService` with its limits. **Partial success**: the record is created first; each file is then attached; the response lists every file as attached or skipped with the reason (too large, type not allowed, limit reached). A refused file never rolls back the record.
- Write one `EmailLink` per created record.
- Duplicates are allowed (one e-mail may produce several actions) but the response of `GET /links` lets the UI warn first.
- Sender and recipient addresses are data: they are matched to users only through `targets/assignees`; nobody is auto-assigned.

## Acceptance
- An action item created here is indistinguishable from one created in the web app, apart from source and link (same notifications, same audit fields).
- A request for a project that left Draft gets the same refusal and message as the web app.
- A 12 MB file is skipped with a reason and the record exists with the other files.
- Integration tests: happy path for each endpoint, refused workspace, refused project, private-workspace link visibility, body truncation.

## Done when
- The acceptance points above hold, and the common acceptance criteria in `00-README.md` §5 hold.
- `dotnet build`, `dotnet test`, `ng build` pass with no new warnings.
- `status.md`, `BACKEND_OVERVIEW.md`, `FRONTEND_OVERVIEW.md` (and the entity / feature lists in `CLAUDE.md`) describe what was actually built.
- You finish with a short report: what was built, what differs from this prompt and why, any open point for the owner.
