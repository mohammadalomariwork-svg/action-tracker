# B-WF-08 — Hook Workflow into Existing ActionItemService

## Context

The existing `IActionItemService` handles CRUD for action items. We need to modify it to:
1. **Block direct date changes** on existing (non-new) standalone action items — force users to go through the workflow
2. **Block direct status changes** to terminal statuses (Completed, OnHold, Deferred, Cancelled) on standalone action items — force them through the workflow
3. **Trigger escalation workflow** when an action item is escalated

This is the most sensitive prompt because it modifies existing code. Be very careful to not break existing functionality.

## Pre-requisite

- B-WF-05 completed (workflow service is available)

## Instructions

### 1. Inject `IActionItemWorkflowService` into the existing ActionItem service implementation

**File:** The existing action item service implementation (likely `ActionTracker.Infrastructure/Services/ActionItemService.cs`)

Add `IActionItemWorkflowService _workflowService` to the constructor.

### 2. Modify the Update method

Find the existing `Update` method (likely `UpdateActionItemAsync` or similar). Add the following checks **only for standalone action items** (`IsStandalone == true`):

**Date freeze check:**
```
IF the action item already exists in the database (this is an update, not create)
AND the action item is standalone
AND (the StartDate or DueDate in the update DTO differs from the current values in the database)
THEN:
  - Do NOT apply the date change
  - Return an error response indicating dates are locked
  - The error message should say: "Dates are locked. Please submit a date change request."
  - HTTP 422 Unprocessable Entity or a business rule violation
```

**Status change approval check:**
```
IF the action item is standalone
AND the new status is one of: Completed, OnHold, Deferred, Cancelled
AND the current status is one of: ToDo, InProgress, Overdue
THEN:
  - Do NOT apply the status change
  - Return an error response indicating approval is required
  - The error message should say: "Status change to {NewStatus} requires approval. Please submit a status change request."
  - HTTP 422 Unprocessable Entity
```

**Important exceptions — do NOT block:**
- Creating a new action item (no workflow on creation)
- Action items that are NOT standalone (project/milestone-linked items have their own lifecycle)
- Status changes that don't go to terminal statuses (e.g., ToDo → InProgress is fine)
- Admin users should also go through the workflow — no bypass for admins
- The workflow service itself needs to apply changes when a request is approved — add an internal method or flag that bypasses the check:
  - Add an `ApplyApprovedDateChangeAsync(Guid actionItemId, DateTime? newStartDate, DateTime? newDueDate)` method to the action item service interface
  - Add an `ApplyApprovedStatusChangeAsync(Guid actionItemId, ActionStatus newStatus)` method
  - These are called by the workflow service after approval and skip the freeze checks

### 3. Modify the existing status update endpoint/method

Find the existing `PATCH /{id}/status` handler. Apply the same status change approval check for standalone items.

### 4. Modify the escalation flow

Find where `IsEscalated` is set to `true` in the existing code. After setting the flag, call:
```csharp
await _workflowService.HandleEscalationAsync(actionItem.Id, currentUserId, escalationReason);
```

If the existing escalation flow doesn't have a `reason` field, check the `ActionItemEscalation` entity for what fields it has and pass the appropriate data.

If there's no explicit escalation endpoint and it's just a field on the update DTO, then add the workflow call in the update method when `IsEscalated` changes from `false` to `true`.

### 5. Add bypass methods to `IActionItemService`

**File:** `ActionTracker.Application/Interfaces/IActionItemService.cs` (exists)

Add:
```csharp
/// <summary>
/// Apply a date change after workflow approval. Bypasses date freeze.
/// </summary>
Task ApplyApprovedDateChangeAsync(Guid actionItemId, DateTime? newStartDate, DateTime? newDueDate);

/// <summary>
/// Apply a status change after workflow approval. Bypasses approval requirement.
/// </summary>
Task ApplyApprovedStatusChangeAsync(Guid actionItemId, ActionStatus newStatus);
```

Implement these in the service:
- `ApplyApprovedDateChangeAsync`: Load the action item, update the dates directly, save
- `ApplyApprovedStatusChangeAsync`: Load the action item, update the status, if Completed set Progress=100, save

### 6. Update the workflow service to use bypass methods

In `ActionItemWorkflowService.ReviewRequestAsync`, when a request is approved, call these bypass methods instead of modifying the action item directly through the DbContext. This ensures all existing business logic (timestamps, progress clamping, etc.) is applied consistently.

## Critical Warnings

- **DO NOT change the ActionItem entity structure** — only modify the service layer logic
- **DO NOT break project/milestone-linked action items** — all checks must have `if (actionItem.IsStandalone)` guards
- **DO NOT remove the existing status update flow** — only add the approval gate for specific transitions on standalone items
- **Test that creating a new standalone action item still works** — the date freeze only applies to updates
- **Test that updating a non-standalone action item's dates still works** — no workflow on project-linked items

## Validation

- Creating a new standalone action item: works normally, dates accepted
- Updating a standalone action item's dates: blocked with 422 error
- Changing standalone action item status to InProgress: works normally
- Changing standalone action item status to Completed: blocked with 422 error
- Updating a project-linked action item's dates: works normally (no workflow)
- Escalating a standalone action item: sends notifications to creator + manager
- `dotnet build` passes
- All existing ActionItem tests pass
