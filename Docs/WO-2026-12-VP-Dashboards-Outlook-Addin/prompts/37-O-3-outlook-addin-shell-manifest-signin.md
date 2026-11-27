# O-3 — Add-in shell, manifest and sign-in

> **Work order:** WO-2026-12 — Executive Office dashboards and Outlook add-in
> **Package:** O-3 · **Size:** L · **Depends on:** O-1
> **Before anything else read:** `Docs/WO-2026-12-VP-Dashboards-Outlook-Addin/prompts/00-README.md` (rules, definitions, stop conditions), then `CLAUDE.md`, `status.md`, `BACKEND_OVERVIEW.md`, `FRONTEND_OVERVIEW.md`.
> Implement **this package only**. Do not start the next one.

## Why
The user clicks the add-in button on an e-mail, a task pane opens, and the first thing it does is make sure the user is signed in to KUmmand Center.

## Read first
`app.routes.ts`, `app.config.ts` and `core/auth/msal.config.ts` (how MSAL is initialised and the redirect handled), `core/services/auth.service.ts` (`azure-login` exchange, token storage, refresh), `auth.interceptor.ts`, the login component, `angular.json`. **Then check Microsoft's current documentation** for Outlook add-ins: manifest formats, Nested App Authentication (NAA) support by Outlook client, and the dialog API fallback. If the guidance today differs from the defaults below, stop and report before choosing.

## Build
1. **Where it lives** — inside the existing Angular app as a lazy route group `/outlook-addin`, rendered **without** the main shell (no side nav, no top bar), designed for a 320–450 px wide pane. Office.js is loaded from Microsoft's CDN **only** on these routes, before any Office API is used (`Office.onReady`); normal users of the web app never download it. If the Angular bootstrap cannot guarantee that order, add a second HTML entry (`taskpane.html`) to the same build rather than a second project.
2. **Manifest** — default: the add-in-only XML manifest (widest client support), in `frontend/outlook-addin-manifest/` with one file per environment generated from a template (base URL as the only variable): message **read** surface, one ribbon button "KUmmand Center" that opens the task pane, pinnable task pane, permission `ReadItem`, requirement set Mailbox 1.8 (attachment content), icons in the required sizes, support URL. No compose surface, no event-based activation, no mobile form factor.
3. **Sign-in, in this order**
   a. A valid KUmmand Center session already in the pane's storage → continue.
   b. **Entra single sign-on** through NAA with the installed `@azure/msal-browser` (nestable public client), silent first, same scope the web app requests, then exchange at `/api/auth/azure-login` exactly as `AuthService` does today.
   c. If NAA is unavailable or silent sign-in fails: a "Sign in with Microsoft" button that opens the Office **dialog** on a page of this app (`/outlook-addin/auth-dialog`), runs the normal MSAL flow there and returns the result with `messageParent`. Only the Entra token crosses to the pane, never through a URL.
   d. "Use a KUmmand Center account" link → e-mail and password form calling the existing local login, shown only when local login is enabled in the web app.
   - After sign-in call `GET /api/outlook-addin/context`. A 403 shows "You do not have access to the Outlook add-in" with the administrator hint; it is not an error screen.
   - Sign-out link in the pane footer; token refresh through the existing interceptor.
4. **Guard** for all pane routes; a small `AddinSessionService` wrapping the existing `AuthService` rather than a second auth implementation.
5. **Theme** — follow Office's theme (light / dark) using the app's theme tokens.
6. **For the administrator (document, do not perform)**: the changes needed on the Entra app registration `AzureAd:ClientId` for NAA and the dialog (redirect URIs), written to `Docs/WO-2026-12-VP-Dashboards-Outlook-Addin/Outlook-Addin-Deployment.md`.

## Acceptance
- Sideloaded in Outlook on the web and in new Outlook for Windows: clicking the button opens the pane; a signed-in Microsoft 365 user reaches the home screen without typing a password; a user without the permission sees the access message.
- With NAA forced off, the dialog path signs in.
- The web app's bundle for `/dashboard` does not include Office.js or the pane's code (check the build output).
- No token appears in any URL, log line or `console` output.

## Done when
- The acceptance points above hold, and the common acceptance criteria in `00-README.md` §5 hold.
- `dotnet build`, `dotnet test`, `ng build` pass with no new warnings.
- `status.md`, `BACKEND_OVERVIEW.md`, `FRONTEND_OVERVIEW.md` (and the entity / feature lists in `CLAUDE.md`) describe what was actually built.
- You finish with a short report: what was built, what differs from this prompt and why, any open point for the owner.
