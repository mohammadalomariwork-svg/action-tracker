# B-PW-06 — Enforce Date Freeze in Existing Services

## Context

After a project is approved (status transitions to `Active`, `IsBaselined = true`), no user should be able to change dates on the project, its milestones, or its linked action items through the API. This prompt adds guard logic to the existing services.

## Business rules

A project's dates are frozen when **any** of these conditions are true:
- `ProjectStatus` is `Active`, `Suspended`, or `Closed`
- `ProjectStatus` is `PendingApproval`

In other words, dates can only be changed when the project is in `Draft` status.

The frozen fields are:
- **Project:** `PlannedStartDate`, `PlannedEndDate`
- **Milestones:** `PlannedStartDate`, `PlannedDueDate`
- **Action Items (linked to the project):** `StartDate`, `DueDate`

## What to do

### 1. Modify `ProjectService` (in Application or Infrastructure layer)

In the **update project** method:
- Before applying changes, load the existing project from DB
- If the project status is NOT `Draft`, check whether `PlannedStartDate` or `PlannedEndDate` in the update DTO differ from the current DB values
- If they differ, throw a `BadRequestException` (or equivalent) with message: `"Project dates cannot be changed after the project has been submitted for approval or activated."`
- Also prevent status changes from `PendingApproval` back to `Draft` through the normal update endpoint — only the workflow review can do that

### 2. Modify `MilestoneService` (in Application or Infrastructure layer)

In the **update milestone** method:
- Load the parent project
- If the project status is NOT `Draft`, check whether `PlannedStartDate` or `PlannedDueDate` in the update DTO differ from the current milestone values
- If they differ, throw a `BadRequestException` with message: `"Milestone dates cannot be changed after the project has been submitted for approval or activated."`

In the **create milestone** method:
- Load the parent project
- If the project status is NOT `Draft`, block creation with message: `"New milestones cannot be added after the project has been submitted for approval or activated."`

In the **delete milestone** method:
- Load the parent project
- If the project status is NOT `Draft`, block deletion with message: `"Milestones cannot be removed after the project has been submitted for approval or activated."`

### 3. Modify `ActionItemService` (in Application or Infrastructure layer)

In the **update action item** method:
- If the action item has a `ProjectId` (not standalone), load the parent project
- If the project status is NOT `Draft`, check whether `StartDate` or `DueDate` differ from the current values
- If they differ, throw a `BadRequestException` with message: `"Action item dates cannot be changed after the parent project has been submitted for approval or activated."`

In the **create action item** method:
- If the action item is being linked to a project and the project status is NOT `Draft`, block creation with message: `"New action items cannot be added to a project after it has been submitted for approval or activated."`

In the **delete action item** method:
- If the action item has a `ProjectId`, load the parent project
- If the project status is NOT `Draft`, block deletion with message: `"Action items cannot be removed from a project after it has been submitted for approval or activated."`

### 4. Helper method (optional but recommended)

Consider adding a private helper method or a shared utility that checks the freeze status to reduce duplication:

```csharp
private async Task EnsureProjectNotFrozenAsync(Guid projectId)
{
    var project = await _context.Projects
        .Where(p => p.Id == projectId && !p.IsDeleted)
        .Select(p => new { p.ProjectStatus })
        .FirstOrDefaultAsync();
    
    if (project != null && project.ProjectStatus != ProjectStatus.Draft)
    {
        throw new BadRequestException("This operation is not allowed after the project has been submitted for approval or activated.");
    }
}
```

## Files to modify
- `ProjectService` implementation file — add date freeze check in update method
- `MilestoneService` implementation file — add date freeze check in create, update, and delete methods
- `ActionItemService` implementation file — add date freeze check in create, update, and delete methods for project-linked items

## Files to create
- None

## Do NOT
- Do not change the action item workflow (date change requests for standalone items are separate and unaffected)
- Do not modify DTOs or entities
- Do not block editing of non-date fields (title, description, status, priority, etc. remain editable)
- Do not affect standalone action items (only project-linked items are subject to freeze)
