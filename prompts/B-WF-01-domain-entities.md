# B-WF-01 — Domain Entities & Enums for Action Item Workflow

## Context

The KU Action Tracker already has `ActionItem`, `ActionItemEscalation`, `ActionItemAssignee`, and `ActionStatus` (ToDo, InProgress, OnHold, Overdue, Completed) in the `ActionTracker.Domain` project. We need to add workflow support for:

1. **Date freeze** — after creation, start/due dates are locked; changes require an approval request
2. **Status change approval** — moving from active statuses (InProgress, ToDo) to terminal/hold statuses (Completed, OnHold, Deferred, Cancelled) requires approval from the creator and the assignee's direct manager
3. **Escalation notifications** — escalation triggers notification + email to creator and assignee's direct manager

## Instructions

### 1. Expand the `ActionStatus` enum

**File:** `ActionTracker.Domain/Enums/ActionStatus.cs` (exists)

Add two new values to the **end** of the existing enum (do not renumber existing values):

```
Deferred = 5,
Cancelled = 6
```

The existing values are: `ToDo = 0, InProgress = 1, OnHold = 2, Overdue = 3, Completed = 4`

### 2. Create new enums

**File:** `ActionTracker.Domain/Enums/WorkflowRequestType.cs` (new)

```
DateChangeRequest = 0,
StatusChangeRequest = 1
```

**File:** `ActionTracker.Domain/Enums/WorkflowRequestStatus.cs` (new)

```
Pending = 0,
Approved = 1,
Rejected = 2
```

### 3. Create `ActionItemWorkflowRequest` entity

**File:** `ActionTracker.Domain/Entities/ActionItemWorkflowRequest.cs` (new)

| Field                    | Type                    | Description                                                        |
|--------------------------|-------------------------|--------------------------------------------------------------------|
| `Id`                     | `Guid` (PK)            | `NEWSEQUENTIALID()` default                                        |
| `ActionItemId`           | `Guid` (FK)            | References `ActionItem.Id`                                         |
| `RequestType`            | `WorkflowRequestType`  | DateChangeRequest or StatusChangeRequest                           |
| `Status`                 | `WorkflowRequestStatus`| Pending, Approved, Rejected                                       |
| `RequestedByUserId`      | `string`               | The user who initiated the request (no FK to AspNetUsers)          |
| `RequestedByDisplayName` | `string`               | Denormalized display name of requester                             |
| `RequestedNewStartDate`  | `DateTime?`            | Only for DateChangeRequest — proposed new start date               |
| `RequestedNewDueDate`    | `DateTime?`            | Only for DateChangeRequest — proposed new due date                 |
| `RequestedNewStatus`     | `ActionStatus?`        | Only for StatusChangeRequest — proposed new status                 |
| `CurrentStartDate`       | `DateTime?`            | Snapshot of current start date at request time                     |
| `CurrentDueDate`         | `DateTime?`            | Snapshot of current due date at request time                       |
| `CurrentStatus`          | `ActionStatus?`        | Snapshot of current status at request time                         |
| `Reason`                 | `string`               | Requester's justification                                          |
| `ReviewedByUserId`       | `string?`              | The user who approved/rejected (null while Pending)                |
| `ReviewedByDisplayName`  | `string?`              | Denormalized display name of reviewer                              |
| `ReviewComment`          | `string?`              | Reviewer's note on approval/rejection                              |
| `ReviewedAt`             | `DateTime?`            | When the review happened                                           |
| `CreatedAt`              | `DateTime`             | UTC creation timestamp                                             |
| `IsDeleted`              | `bool`                 | Soft delete                                                        |

**Navigation property:** `ActionItem` (the parent action item). Do NOT add an FK navigation to `ApplicationUser` — follow the project convention of plain string user IDs with denormalized display names.

### 4. Add navigation to `ActionItem` entity

**File:** `ActionTracker.Domain/Entities/ActionItem.cs` (exists)

Add a collection navigation property:

```csharp
public ICollection<ActionItemWorkflowRequest> WorkflowRequests { get; set; } = new List<ActionItemWorkflowRequest>();
```

### 5. Add `AreDatesLocked` computed property to `ActionItem`

**File:** `ActionTracker.Domain/Entities/ActionItem.cs` (exists)

Add a non-mapped helper property:

```csharp
/// <summary>
/// Dates are locked once the action item has been created (Id != Guid.Empty).
/// Changes require a workflow request.
/// </summary>
[NotMapped]
public bool AreDatesLocked => Id != Guid.Empty;
```

## Validation

- The solution should compile with `dotnet build` after this step
- No migration yet — that is B-WF-02
- Existing ActionItem tests should still pass (new enum values are additive)
