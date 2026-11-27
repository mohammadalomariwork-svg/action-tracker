# O-1 — Add-in API: permission, context, targets, e-mail links

> **Work order:** WO-2026-12 — Executive Office dashboards and Outlook add-in
> **Package:** O-1 · **Size:** M · **Depends on:** WO-2026-11 P-2b (ownership rule); V-F5 for `Source`
> **Before anything else read:** `Docs/WO-2026-12-VP-Dashboards-Outlook-Addin/prompts/00-README.md` (rules, definitions, stop conditions), then `CLAUDE.md`, `status.md`, `BACKEND_OVERVIEW.md`, `FRONTEND_OVERVIEW.md`.
> Implement **this package only**. Do not start the next one.

## Why
The add-in must show a user only what they are allowed to do: create an action item, create a project, add an action item to a project they manage. It also has to remember which records were created from which e-mail.

## Read first
`AuthController` (`azure-login`, `refresh-token`), `Program.cs` (the two JWT schemes, CORS `AllowAngularApp`, security headers), `IProjectAccessService` (`EnsureCanCreateInWorkspaceAsync`, `CanManage`, the batch versions), `ActionItemService.CreateAsync` (the "project must be Draft" rule at the three places it appears), `PermissionPolicies.cs`, the seeders.

## Data
`EmailLink`: `Id`, `InternetMessageId` (≤ 500, indexed), `ConversationId` (≤ 500), `Subject` (≤ 500), `SenderName` (≤ 200), `SenderEmail` (≤ 320), `ReceivedAt`, `EntityType` (`ActionItem` / `Project`), `EntityId`, `CreatedByUserId`, `CreatedAt`. A row is returned only when the caller may see the linked record.

## Permission
New area **`OutlookAddin`** with one action `Use` (next free area GUID; all four places). Default grant: every role that holds `ActionItems.Create` today. What a user can do inside the add-in is still decided by `ActionItems.Create`, `Projects.Create` and the ownership rule.

## API — `OutlookAddinController` at `/api/outlook-addin`, every route `[Authorize(Policy = OutlookAddinUse)]`
- `GET /context` → user (name, e-mail), flags `canCreateActionItem`, `canCreateProject`, `canAddToProject`, limits (max file size, max files, allowed extensions — read from `DocumentService`, not copied), the web app's base URL for "Open in KUmmand Center" links.
- `GET /targets/workspaces?for=actionItem|project` → workspaces where the server would accept that create for this caller (same checks the create would run).
- `GET /targets/projects?search=&page=` → projects where the caller may add an action item **today**: manage right on the project **and** the current project-status rule. Return `canAdd` and, when false, the server's reason, so the UI can list a project greyed out with the explanation. Do not change the rule here.
- `GET /targets/projects/{id}/milestones` → live milestones with their date windows (for the P-4 date rule).
- `GET /targets/assignees?workspaceId=&projectId=&emails=` → the assignable pool the action form uses, plus which of the given e-mail addresses (sender, recipients) are in that pool, for suggestions.
- `GET /links?internetMessageId=` → records already created from this e-mail that the caller may see (code, title, type, status, URL).

## Sign-in
- No new token type. The add-in obtains an Entra ID access token and exchanges it at the existing `POST /api/auth/azure-login`; refresh through the existing endpoint. Local accounts use the existing `POST /api/auth/login`.
- The add-in is served by the same site as the web app (see O-3), so **no CORS change**. If `Program.cs` or the hosting adds `X-Frame-Options` or a `frame-ancestors` policy, the add-in routes must be frameable by Outlook on the web; change the header **for those routes only** and list the exact origins allowed. If no such header exists, add none.

## Acceptance
- A user without `OutlookAddin.Use` gets 403 on every route.
- `targets/workspaces` and `targets/projects` never list a place where the following create would be refused (integration test: for each listed target, the create succeeds; for a non-listed one, it is refused).
- `links` does not reveal a record in a private workspace to a non-member who received the same e-mail.

## Done when
- The acceptance points above hold, and the common acceptance criteria in `00-README.md` §5 hold.
- `dotnet build`, `dotnet test`, `ng build` pass with no new warnings.
- `status.md`, `BACKEND_OVERVIEW.md`, `FRONTEND_OVERVIEW.md` (and the entity / feature lists in `CLAUDE.md`) describe what was actually built.
- You finish with a short report: what was built, what differs from this prompt and why, any open point for the owner.
