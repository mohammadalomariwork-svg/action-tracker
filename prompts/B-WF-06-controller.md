# B-WF-06 — Action Item Workflow Controller

## Context

We need a new controller to expose the workflow service. Follow the same patterns as existing controllers (`ActionItemsController`, `NotificationsController`): base route, authorization policies, standard response patterns, async all the way.

## Pre-requisite

- B-WF-05 completed (service exists and is registered in DI)

## Instructions

### 1. Create `ActionItemWorkflowController`

**File:** `ActionTracker.API/Controllers/ActionItemWorkflowController.cs` (new)

**Route base:** `/api/action-items/workflow`

**Constructor:** Inject `IActionItemWorkflowService` and `UserManager<ApplicationUser>` (to get current user ID from claims).

**Get current user pattern:** Follow the same pattern as existing controllers — typically `User.FindFirstValue(ClaimTypes.NameIdentifier)` or checking for `oid` claim for Azure AD users. Look at how `ActionItemsController` resolves the current user ID and replicate that exactly.

### 2. Endpoints

| Method | Route                          | Auth Policy           | Description                                |
|--------|--------------------------------|-----------------------|--------------------------------------------|
| POST   | `/date-change-request`         | `ActionItems.Edit`    | Create a date change request               |
| POST   | `/status-change-request`       | `ActionItems.Edit`    | Create a status change request             |
| PUT    | `/requests/{requestId}/review` | `ActionItems.Approve` | Approve or reject a workflow request       |
| GET    | `/pending-reviews`             | `ActionItems.View`    | Get pending requests for current user      |
| GET    | `/my-requests`                 | `ActionItems.View`    | Get requests created by current user       |
| GET    | `/action-item/{actionItemId}`  | `ActionItems.View`    | Get all requests for a specific action item|
| GET    | `/pending-summary`             | `ActionItems.View`    | Get pending request counts for badge       |
| POST   | `/escalate`                    | `ActionItems.Edit`    | Trigger escalation workflow                |
| POST   | `/give-direction`              | `ActionItems.Edit`    | Give direction on escalated item           |
| GET    | `/can-review/{actionItemId}`   | `ActionItems.View`    | Check if current user can review           |

### 3. Endpoint Details

#### `POST /date-change-request`
- Body: `CreateDateChangeRequestDto`
- Resolve current user ID from claims
- Call `_workflowService.CreateDateChangeRequestAsync(dto, userId)`
- Return `201 Created` with the response DTO

#### `POST /status-change-request`
- Body: `CreateStatusChangeRequestDto`
- Resolve current user ID
- Call `_workflowService.CreateStatusChangeRequestAsync(dto, userId)`
- Return `201 Created`

#### `PUT /requests/{requestId}/review`
- Route param: `requestId` (Guid)
- Body: `ReviewWorkflowRequestDto`
- Resolve current user ID
- Call `_workflowService.ReviewRequestAsync(requestId, dto, userId)`
- Return `200 OK` with updated response DTO
- If unauthorized reviewer, return `403 Forbidden`

#### `GET /pending-reviews`
- Query params: `page` (default 1), `pageSize` (default 20)
- Resolve current user ID
- Call `_workflowService.GetPendingRequestsForReviewerAsync(userId, page, pageSize)`
- Return `200 OK` with `PagedResult<WorkflowRequestResponseDto>`

#### `GET /my-requests`
- Query params: `page` (default 1), `pageSize` (default 20)
- Resolve current user ID
- Call `_workflowService.GetMyRequestsAsync(userId, page, pageSize)`
- Return `200 OK` with `PagedResult<WorkflowRequestResponseDto>`

#### `GET /action-item/{actionItemId}`
- Route param: `actionItemId` (Guid)
- Call `_workflowService.GetRequestsForActionItemAsync(actionItemId)`
- Return `200 OK` with list

#### `GET /pending-summary`
- Resolve current user ID
- Call `_workflowService.GetPendingSummaryAsync(userId)`
- Return `200 OK` with `WorkflowRequestSummaryDto`

#### `POST /escalate`
- Body: `{ actionItemId: Guid, reason: string }`
- Create a simple DTO or use an inline model
- Resolve current user ID
- Call `_workflowService.HandleEscalationAsync(actionItemId, userId, reason)`
- Return `200 OK`

#### `POST /give-direction`
- Body: `WorkflowDirectionDto`
- Resolve current user ID
- Call `_workflowService.GiveDirectionAsync(dto, userId)`
- Return `200 OK`

#### `GET /can-review/{actionItemId}`
- Route param: `actionItemId` (Guid)
- Resolve current user ID
- Call `_workflowService.CanUserReviewAsync(actionItemId, userId)`
- Return `200 OK` with `{ canReview: bool }`

### 4. Error Handling

Follow the existing controller patterns:
- 404 for not found entities (likely thrown as exceptions by the service, caught by `ExceptionMiddleware`)
- 409 for conflicts (duplicate pending requests)
- 403 for unauthorized reviewer
- 400 for validation errors (handled by FluentValidation middleware)
- All methods async with `CancellationToken` parameter if existing controllers use it

### 5. Swagger Documentation

Add `[ProducesResponseType]` attributes on each endpoint for Swagger, matching existing controller patterns.

## Validation

- `dotnet build` passes
- All endpoints appear in Swagger UI
- Authorization policies are applied
- API routes follow RESTful conventions
