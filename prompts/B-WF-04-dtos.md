# B-WF-04 — DTOs for Action Item Workflow

## Context

The project uses DTOs in the `ActionTracker.Application` layer. DTOs are typically organized in folders per feature (e.g., `ActionTracker.Application/DTOs/` or `ActionTracker.Application/Features/ActionItems/DTOs/`). Check the existing structure and place new DTOs in the same pattern.

## Pre-requisite

- B-WF-01 completed (domain entities exist)

## Instructions

### 1. Create Request DTOs

**File:** Place in the same folder pattern as existing ActionItem DTOs — likely `ActionTracker.Application/DTOs/Workflow/` (new folder) or alongside existing action item DTOs.

#### `CreateDateChangeRequestDto`

| Field            | Type        | Validation                         |
|------------------|-------------|-------------------------------------|
| `ActionItemId`   | `Guid`      | Required                           |
| `NewStartDate`   | `DateTime?` | At least one of start/due required |
| `NewDueDate`     | `DateTime?` | At least one of start/due required |
| `Reason`         | `string`    | Required, max 2000 chars           |

#### `CreateStatusChangeRequestDto`

| Field           | Type           | Validation                                                |
|-----------------|----------------|-----------------------------------------------------------|
| `ActionItemId`  | `Guid`         | Required                                                  |
| `NewStatus`     | `ActionStatus`  | Required. Must be one of: Completed, OnHold, Deferred, Cancelled |
| `Reason`        | `string`       | Required, max 2000 chars                                  |

#### `ReviewWorkflowRequestDto`

| Field           | Type                     | Validation             |
|-----------------|--------------------------|-------------------------|
| `IsApproved`    | `bool`                   | Required               |
| `ReviewComment` | `string?`                | Optional, max 2000     |

#### `WorkflowDirectionDto`

| Field           | Type     | Validation             |
|-----------------|----------|-------------------------|
| `ActionItemId`  | `Guid`   | Required               |
| `DirectionText` | `string` | Required, max 2000     |

This is used by the creator or direct manager to give direction on an escalated item — it creates a high-importance comment.

### 2. Create Response DTOs

#### `WorkflowRequestResponseDto`

| Field                      | Type        | Description                                |
|----------------------------|-------------|---------------------------------------------|
| `Id`                       | `Guid`      | Request ID                                 |
| `ActionItemId`             | `Guid`      | Parent action item                         |
| `ActionItemCode`           | `string`    | e.g., "ACT-042"                            |
| `ActionItemTitle`          | `string`    | Action item title                          |
| `RequestType`              | `string`    | "DateChangeRequest" or "StatusChangeRequest"|
| `Status`                   | `string`    | "Pending", "Approved", "Rejected"          |
| `RequestedByUserId`        | `string`    | Requester ID                               |
| `RequestedByDisplayName`   | `string`    | Requester name                             |
| `RequestedNewStartDate`    | `DateTime?` | Proposed start date                        |
| `RequestedNewDueDate`      | `DateTime?` | Proposed due date                          |
| `RequestedNewStatus`       | `string?`   | Proposed status as string                  |
| `CurrentStartDate`         | `DateTime?` | Original start date                        |
| `CurrentDueDate`           | `DateTime?` | Original due date                          |
| `CurrentStatus`            | `string?`   | Original status as string                  |
| `Reason`                   | `string`    | Requester justification                    |
| `ReviewedByUserId`         | `string?`   | Reviewer ID                                |
| `ReviewedByDisplayName`    | `string?`   | Reviewer name                              |
| `ReviewComment`            | `string?`   | Reviewer note                              |
| `ReviewedAt`               | `DateTime?` | Review timestamp                           |
| `CreatedAt`                | `DateTime`  | Request creation timestamp                 |

#### `WorkflowRequestSummaryDto`

| Field                  | Type  | Description                  |
|------------------------|-------|-------------------------------|
| `PendingDateChanges`   | `int` | Count of pending date changes |
| `PendingStatusChanges` | `int` | Count of pending status changes|
| `TotalPending`         | `int` | Total pending requests         |

### 3. Create FluentValidation Validators

Create validators for `CreateDateChangeRequestDto`, `CreateStatusChangeRequestDto`, `ReviewWorkflowRequestDto`, and `WorkflowDirectionDto` using the same FluentValidation pattern as existing validators in the project.

Key validation rules:
- `CreateDateChangeRequestDto`: At least one of `NewStartDate` or `NewDueDate` must be provided. If `NewDueDate` is provided and `NewStartDate` is provided, `NewDueDate` must be after `NewStartDate`.
- `CreateStatusChangeRequestDto`: `NewStatus` must be `Completed`, `OnHold`, `Deferred`, or `Cancelled` (not `ToDo`, `InProgress`, or `Overdue`).
- `ReviewWorkflowRequestDto`: `ReviewComment` required if `IsApproved` is `false` (rejection requires explanation).

### 4. Register validators in DI

If validators are auto-registered via assembly scanning in `Program.cs` or a DI extension, no action needed. If manually registered, add the new validators to the DI container.

## Validation

- `dotnet build` passes
- DTOs follow the same namespace and folder pattern as existing ones
- Validators follow the same FluentValidation registration pattern
