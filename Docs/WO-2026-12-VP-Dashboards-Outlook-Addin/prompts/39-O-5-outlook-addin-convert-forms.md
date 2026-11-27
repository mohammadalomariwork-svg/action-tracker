# O-5 — Convert forms: action item, project, add to existing project

> **Work order:** WO-2026-12 — Executive Office dashboards and Outlook add-in
> **Package:** O-5 · **Size:** L · **Depends on:** O-2, O-4
> **Before anything else read:** `Docs/WO-2026-12-VP-Dashboards-Outlook-Addin/prompts/00-README.md` (rules, definitions, stop conditions), then `CLAUDE.md`, `status.md`, `BACKEND_OVERVIEW.md`, `FRONTEND_OVERVIEW.md`.
> Implement **this package only**. Do not start the next one.

## Read first
The web app's action form and project form components, `app-person-picker`, `ProjectClassificationSelectComponent`, the tag chip input, `work-calendar.util.ts` and its validator, the models in `models/`. Reuse validators, models and pickers; build compact pane layouts around them instead of loading the full page forms.

## Screens
1. **Create action item**
   - Workspace (from `targets/workspaces?for=actionItem`; the last used one is remembered per user in the pane's storage).
   - Title = subject without "RE:" / "FW:" prefixes (editable, max length of the field). Description = e-mail body text (editable, counter, truncated note from O-2).
   - Priority, planned start, due date (calendar validator and "use next working day" shortcut), tags.
   - Assignees: person picker over `targets/assignees`; the sender and recipients who are in the pool are offered as one-tap suggestions. Nobody is pre-selected.
   - **Attachments**: a check list of the e-mail's files with sizes; files over the limit or of a refused type are shown disabled with the reason (limits from `context`); "Attach the e-mail text as a file" switch.
2. **Add action item to a project** — step 1: search `targets/projects` (code, name, workspace, status); projects where adding is not allowed are listed disabled with the server's reason. Step 2: milestone (required where P-4 requires it; dates limited to the milestone's window). Step 3: the same fields as screen 1 with the workspace fixed.
3. **Create project** — workspace (`for=project`), name = cleaned subject, description = body, classification (global) and internal classification, lead PM from the workspace's PM list (the user when on it), sponsors, planned start and end, priority; strategic objective when Strategic. The project is created as **Draft**; the pane says so and that approval is submitted from the web app. Attachments as in screen 1.
4. **Result** — the code and title, "Open in KUmmand Center" (opens the browser), per-file results (attached / skipped and why), "Create another from this e-mail", "Back to e-mail".

## Rules
- A duplicate warning when `links` already holds a record of the same type for this e-mail: "An action item was already created from this e-mail (ACT-…). Create another?".
- Server messages (400 / 403 / 409) are shown next to the field they concern where the response names one, otherwise at the top; the form keeps its content.
- Attachment content is fetched from Outlook only for the files the user ticked, at submit time, with a progress line; the Create button is disabled while submitting; a failed submit can be retried without re-entering anything.
- No rule is re-implemented in the pane beyond the validators shared with the web app; the server decides.

## Acceptance
- From one e-mail with three attachments: an action item with two of them, a draft project, and an action item inside an existing draft project are created and appear under "Already in KUmmand Center".
- A user without `Projects.Create` never sees the project path; a user who manages no project where adding is allowed sees the third action with the explanation.
- Component specs for each form (with the fake mail service) covering defaults from the e-mail, validation, partial attachment success and the duplicate warning.

## Done when
- The acceptance points above hold, and the common acceptance criteria in `00-README.md` §5 hold.
- `dotnet build`, `dotnet test`, `ng build` pass with no new warnings.
- `status.md`, `BACKEND_OVERVIEW.md`, `FRONTEND_OVERVIEW.md` (and the entity / feature lists in `CLAUDE.md`) describe what was actually built.
- You finish with a short report: what was built, what differs from this prompt and why, any open point for the owner.
