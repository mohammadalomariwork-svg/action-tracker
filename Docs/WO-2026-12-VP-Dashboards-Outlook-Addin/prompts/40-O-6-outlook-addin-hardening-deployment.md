# O-6 — Add-in hardening, tests and deployment pack

> **Work order:** WO-2026-12 — Executive Office dashboards and Outlook add-in
> **Package:** O-6 · **Size:** M · **Depends on:** O-5
> **Before anything else read:** `Docs/WO-2026-12-VP-Dashboards-Outlook-Addin/prompts/00-README.md` (rules, definitions, stop conditions), then `CLAUDE.md`, `status.md`, `BACKEND_OVERVIEW.md`, `FRONTEND_OVERVIEW.md`.
> Implement **this package only**. Do not start the next one.

## Build
1. **End-to-end check list** executed and recorded in `Docs/WO-2026-12-VP-Dashboards-Outlook-Addin/Outlook-Addin-Test-Record.md`: Outlook on the web, new Outlook for Windows, classic Outlook for Windows, Outlook for Mac — sign-in paths (SSO, dialog, local account), the three conversions, pinned pane, protected message, shared mailbox or delegate access (state what works; if attachments cannot be read there, the pane must say so).
2. **Security review** of the add-in surface, written into the same record: routes frameable only where needed; no e-mail HTML rendered; no token in URL or log; file limits enforced on the server; `OutlookAddin.Use` on every route; rate limit on the two convert endpoints (use the rate limiter the API already has; if there is none, propose one and stop).
3. **Logging** — one structured log line per conversion (user id, record type and id, number of files attached / skipped, client type) with no subject, body or address in it.
4. **Web app** — on the action item and project pages, a small "Created from e-mail" line (sender, date) when an `EmailLink` exists; the Source column / filter from V-F5 already shows "E-mail".
5. **Deployment pack** in `Docs/WO-2026-12-VP-Dashboards-Outlook-Addin/`: `Outlook-Addin-Deployment.md` (Entra app registration changes, production manifest, upload through the Microsoft 365 admin center "Integrated apps", assignment to a pilot group first, how to roll back, how the manifest version is raised) and `Outlook-Addin-User-Guide.md` (one page: open, sign in, three things you can do, what the permissions mean).
6. Permissions matrix UI: confirm the new `OutlookAddin` area appears and can be granted per role and per user.

## Acceptance
- The test record shows each client and path as passed, failed or not supported, with the reason.
- All earlier acceptance points of O-1 to O-5 still pass in one run.
- An administrator can deploy to a pilot group using only the deployment document.

## Done when
- The acceptance points above hold, and the common acceptance criteria in `00-README.md` §5 hold.
- `dotnet build`, `dotnet test`, `ng build` pass with no new warnings.
- `status.md`, `BACKEND_OVERVIEW.md`, `FRONTEND_OVERVIEW.md` (and the entity / feature lists in `CLAUDE.md`) describe what was actually built.
- You finish with a short report: what was built, what differs from this prompt and why, any open point for the owner.
