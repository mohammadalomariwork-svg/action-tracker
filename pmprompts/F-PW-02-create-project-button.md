# F-PW-02 — "Create New Project" Button on My Projects Page

## Context

The My Projects page (`/projects/my`, component `MyProjectsComponent`) already shows a list of projects filtered by the current user's role. A route to create a project (`/projects/new`) and the `ProjectFormComponent` already exist. However, there is currently no visible "Create New Project" button on the My Projects page. The button should only appear if the user has the `Projects.Create` permission.

## What to do

### 1. Add a "Create New Project" button to `MyProjectsComponent`

- Place the button in the page header area (next to the page title or in the top-right action area, consistent with how other pages like Workspaces have their create buttons).
- The button should:
  - Display text: "Create New Project" with a `bi-plus-lg` Bootstrap icon
  - Navigate to `/projects/new` on click using `Router.navigate`
  - Be styled with Bootstrap: `btn btn-primary`
  - Be conditionally rendered using the `*hasPermission="'Projects.Create'"` directive

### 2. Handle the `PendingApproval` status in the projects list

The My Projects component likely displays a status badge for each project. Ensure the `StatusBadgeComponent` (or inline badge) handles the new `PendingApproval` status:
- Display text: "Pending Approval"
- Badge color: Use a warning/amber color (e.g., Bootstrap `bg-warning text-dark` or the equivalent theme variable)
- This may already be handled if the status badge component is data-driven, but verify and add if missing

### 3. Verify project form handles `PendingApproval` status

In the existing `ProjectFormComponent`:
- The status dropdown should NOT include `PendingApproval` as a selectable option — this status is set only by the workflow
- If the project being edited is in `PendingApproval` status, the form should show the status as read-only text, not a dropdown
- Verify this behavior and add the guard if it's missing

## Files to modify
- `MyProjectsComponent` template (`.html`) — add the create button
- `StatusBadgeComponent` (if needed) — handle `PendingApproval` status display
- `ProjectFormComponent` (if needed) — prevent `PendingApproval` from being selectable

## Files to create
- None

## Do NOT
- Do not modify the project creation logic or API calls
- Do not change routing (the `/projects/new` route already exists)
- Do not modify other pages
