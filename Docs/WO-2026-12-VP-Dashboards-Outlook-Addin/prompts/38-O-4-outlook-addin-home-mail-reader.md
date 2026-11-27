# O-4 — Task pane home and reading the e-mail

> **Work order:** WO-2026-12 — Executive Office dashboards and Outlook add-in
> **Package:** O-4 · **Size:** M · **Depends on:** O-3
> **Before anything else read:** `Docs/WO-2026-12-VP-Dashboards-Outlook-Addin/prompts/00-README.md` (rules, definitions, stop conditions), then `CLAUDE.md`, `status.md`, `BACKEND_OVERVIEW.md`, `FRONTEND_OVERVIEW.md`.
> Implement **this package only**. Do not start the next one.

## Build
1. **`OutlookMailService`** — the only place that touches Office.js. Promise / signal wrappers for: subject, sender (name, address), To and Cc, received time, `internetMessageId`, `conversationId`, body as **text** (`body.getAsync(Office.CoercionType.Text)`), the attachment list (name, size, content type, `isInline`), and `getAttachmentContentAsync` → `File`. Handles `ItemChanged` so a pinned pane follows the selected e-mail. A fake implementation behind the same interface for unit tests and for running the pane in a normal browser tab during development (`?mock=1`, development builds only).
2. **Home screen**
   - A card with the e-mail: subject, sender, date, number of attachments.
   - **"Already in KUmmand Center"** — results of `GET /links` for this e-mail, each opening the record in the web app in the browser.
   - Three actions, each shown only when `context` allows it: **Create action item**, **Create project**, **Add action item to a project**. When none is allowed: an explanation, no buttons.
3. **States** — loading; no item selected; unsupported item (meeting request, compose mode, a message without an internet message id such as an unsent draft) with a plain explanation; protected (IRM / encrypted) message where the body or attachments cannot be read: the user can still create a record with the subject and their own text, and the pane says the content could not be read; Office API error with retry.
4. Inline images (`isInline`) are left out of the attachment list by default.

## Rules
- The pane reads the e-mail only when the user opened the add-in on it, and sends nothing to the server until the user presses Create.
- Text only: the body is never inserted into the page as HTML.

## Acceptance
- Selecting another e-mail with the pane pinned refreshes the card and the links, and discards an untouched form; a form with changes asks before discarding.
- Unit tests for the service mapping (with the fake) and for each state.
- Usable at 320 px width, by keyboard and with a screen reader (headings, labels, focus moved to the screen title on navigation).

## Done when
- The acceptance points above hold, and the common acceptance criteria in `00-README.md` §5 hold.
- `dotnet build`, `dotnet test`, `ng build` pass with no new warnings.
- `status.md`, `BACKEND_OVERVIEW.md`, `FRONTEND_OVERVIEW.md` (and the entity / feature lists in `CLAUDE.md`) describe what was actually built.
- You finish with a short report: what was built, what differs from this prompt and why, any open point for the owner.
