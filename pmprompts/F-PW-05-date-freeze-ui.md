# F-PW-05 — Date Freeze UI Enforcement

## Context

The backend (B-PW-06) already blocks date changes for projects that are not in `Draft` status. This prompt adds the visual enforcement on the frontend so users get clear feedback that dates are locked, rather than submitting and receiving an error.

## What to do

### 1. Project Form (`ProjectFormComponent`)

When editing a project that is NOT in `Draft` status (`PendingApproval`, `Active`, `Suspended`, `Closed`):
- **Planned Start Date** input: make it `readonly` or `disabled`, add a lock icon (`bi-lock-fill`) next to the label
- **Planned End Date** input: make it `readonly` or `disabled`, add a lock icon next to the label
- Add a small info text below the locked fields: "Dates are locked after the project is submitted for approval."
- **Status** dropdown: disable it (status transitions are controlled by workflow now, not manual selection)
- All other fields (name, description, budget, priority, sponsors, etc.) remain editable as normal

When creating a new project (no project ID):
- All fields remain fully editable (no freeze applies)

### 2. Milestone Form (within `MilestoneSectionComponent` or equivalent)

When the parent project is NOT in `Draft` status:
- **Planned Start Date** and **Planned Due Date** inputs: make them `readonly`/`disabled` with lock icons
- **"Add Milestone" button:** hide it entirely
- **"Delete Milestone" button/action:** hide it entirely
- Show a small info banner at the top of the milestones section: "Milestones are locked. The project has been submitted for approval or is active."
- Editing non-date fields on existing milestones (name, sequence, approver, completion %) remains allowed

### 3. Action Item Form (`ActionFormComponent`)

When the action item is linked to a project (has `projectId`) and the parent project is NOT in `Draft` status:
- **Start Date** and **Due Date** inputs: make them `readonly`/`disabled` with lock icons
- Add info text: "Dates are locked because the parent project is no longer in draft."
- The "Add Action Item" button within the milestone detail should be hidden when the project is not in Draft
- All other fields remain editable

When the action item is standalone (no `projectId`):
- The existing action item date freeze workflow applies (separate feature, do not change)

### 4. Pass project status to child components

The project detail page loads the project data. Ensure the project's current status is passed down to:
- Milestone section/form components
- Action item form components (when opened from within a project context)

This can be done via:
- An `@Input()` property like `projectStatus: ProjectStatus`
- Or by having child components look up the project status from a shared service/signal

### 5. Visual consistency

- Use the same lock icon style (`bi-lock-fill` in muted/secondary color) used for the action item date freeze feature
- The lock icon should appear inline with the form label, not replacing it
- Disabled inputs should use the Bootstrap `disabled` attribute styling (grayed out but still showing the value)
- Maintain both dark and light theme compatibility

## Files to modify
- `ProjectFormComponent` template and TypeScript — disable date and status fields when not Draft
- `MilestoneSectionComponent` (or equivalent) template and TypeScript — disable date fields, hide add/delete buttons
- `ActionFormComponent` template and TypeScript — disable date fields when linked to a non-Draft project
- `ProjectDetailComponent` — pass project status to child components if not already done

## Files to create
- None

## Do NOT
- Do not modify backend logic (already handled in B-PW-06)
- Do not change the standalone action item date freeze behavior
- Do not prevent editing of non-date fields
- Do not hide the entire form — only lock the specific date inputs
