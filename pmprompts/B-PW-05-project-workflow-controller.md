# B-PW-05 — ProjectWorkflowController

## Context

`IProjectWorkflowService` and its implementation are complete from B-PW-04. Now expose the workflow operations through a new API controller.

## What to do

### 1. Create `ProjectWorkflowController` in `ActionTracker.API/Controllers/`

**Route prefix:** `/api/projects/workflow`

**Authorization:** All endpoints require authentication via `[Authorize(Policy = "LocalOrAzureAD")]`. Use the existing pattern where the current user ID is extracted from claims (e.g., `User.FindFirstValue(ClaimTypes.NameIdentifier)` or the equivalent helper used across the codebase).

| Method | Endpoint | Description | Extra Auth |
|--------|----------|-------------|------------|
| `POST` | `/submit` | Submit project for start-approval | `Projects.Edit` policy |
| `PUT` | `/requests/{requestId}/review` | Approve or reject an approval request | `Projects.Approve` policy |
| `GET` | `/project/{projectId}` | Get all approval requests for a project | `Projects.View` policy |
| `GET` | `/pending-reviews` | Get pending requests for current user to review | None (scoped to user) |
| `GET` | `/my-requests` | Get requests submitted by current user | None (scoped to user) |
| `GET` | `/pending-summary` | Get pending count for header badge | None (scoped to user) |
| `GET` | `/can-review/{projectId}` | Check if current user can review a project | None (scoped to user) |

### 2. Endpoint details

#### `POST /submit`
- Accepts `SubmitProjectApprovalRequestDto` in the request body
- Calls `IProjectWorkflowService.SubmitForApprovalAsync(dto, currentUserId)`
- Returns `200 OK` with `ProjectApprovalRequestDto`
- Returns `400 Bad Request` with ProblemDetails if validation fails (project not in Draft, user not PM, etc.)

#### `PUT /requests/{requestId}/review`
- Accepts `ReviewProjectApprovalRequestDto` in the request body
- Overrides `dto.RequestId` with the route parameter `requestId` for consistency
- Calls `IProjectWorkflowService.ReviewApprovalRequestAsync(dto, currentUserId)`
- Returns `200 OK` with `ProjectApprovalRequestDto`
- Returns `400 Bad Request` if validation fails
- Returns `403 Forbidden` if the user is not authorized to review

#### `GET /project/{projectId}`
- Calls `IProjectWorkflowService.GetApprovalRequestsForProjectAsync(projectId)`
- Returns `200 OK` with `List<ProjectApprovalRequestDto>`

#### `GET /pending-reviews`
- Calls `IProjectWorkflowService.GetPendingReviewsAsync(currentUserId)`
- Returns `200 OK` with `List<ProjectApprovalRequestDto>`

#### `GET /my-requests`
- Calls `IProjectWorkflowService.GetMyRequestsAsync(currentUserId)`
- Returns `200 OK` with `List<ProjectApprovalRequestDto>`

#### `GET /pending-summary`
- Calls `IProjectWorkflowService.GetPendingSummaryAsync(currentUserId)`
- Returns `200 OK` with `ProjectApprovalSummaryDto`

#### `GET /can-review/{projectId}`
- Calls `IProjectWorkflowService.CanReviewProjectAsync(projectId, currentUserId)`
- Returns `200 OK` with `{ canReview: true/false }`

### 3. Hook project creation notifications into existing ProjectsController

In the existing `ProjectsController`, after the `POST /` (create project) endpoint successfully creates a project, add a call to:
```
await _projectWorkflowService.SendProjectCreatedNotificationsAsync(createdProject.Id, currentUserId);
```

Inject `IProjectWorkflowService` into `ProjectsController` constructor.

## Files to create
- `ActionTracker.API/Controllers/ProjectWorkflowController.cs`

## Files to modify
- `ActionTracker.API/Controllers/ProjectsController.cs` — inject `IProjectWorkflowService` and call `SendProjectCreatedNotificationsAsync` after project creation

## Do NOT
- Do not modify the project workflow service
- Do not create frontend files
- Do not modify the existing action item workflow controller
