# B-PW-04 — IProjectWorkflowService + Implementation

## Context

Entities (`ProjectApprovalRequest`), DTOs, validators, and email templates are in place from B-PW-01 through B-PW-03. Now build the core service that handles:
1. Sending notifications when a project is created
2. Submitting a project for start-approval
3. Reviewing (approving/rejecting) an approval request
4. Querying approval requests
5. Checking whether the current user can review a project

Use the existing `IWorkflowNotificationHelper` as a reference for how notifications (in-app + email + SignalR) are dispatched. Reuse that helper or follow its pattern.

## What to do

### 1. Create `IProjectWorkflowService` in `ActionTracker.Application/Interfaces/`

```csharp
public interface IProjectWorkflowService
{
    // Called after project creation to notify manager + sponsors
    Task SendProjectCreatedNotificationsAsync(Guid projectId, string createdByUserId);

    // Project manager submits project for approval (Draft → PendingApproval)
    Task<ProjectApprovalRequestDto> SubmitForApprovalAsync(SubmitProjectApprovalRequestDto dto, string requestedByUserId);

    // Sponsor or manager approves/rejects
    Task<ProjectApprovalRequestDto> ReviewApprovalRequestAsync(ReviewProjectApprovalRequestDto dto, string reviewerUserId);

    // Get all approval requests for a project
    Task<List<ProjectApprovalRequestDto>> GetApprovalRequestsForProjectAsync(Guid projectId);

    // Get pending approval requests where the current user is a reviewer
    Task<List<ProjectApprovalRequestDto>> GetPendingReviewsAsync(string userId);

    // Get approval requests submitted by the current user
    Task<List<ProjectApprovalRequestDto>> GetMyRequestsAsync(string userId);

    // Summary counts for badge
    Task<ProjectApprovalSummaryDto> GetPendingSummaryAsync(string userId);

    // Check if user can review a specific project
    Task<bool> CanReviewProjectAsync(Guid projectId, string userId);
}
```

### 2. Create `ProjectWorkflowService` in `ActionTracker.Infrastructure/Services/`

#### `SendProjectCreatedNotificationsAsync`
1. Load the project with its Sponsors navigation property.
2. Resolve the project manager's direct line manager from `KuEmployeeInfo` table — look up the row where `Email` matches the project manager's email, then use `SupervisorEmail` and `SupervisorName` to find the manager user. Follow the same pattern used in `WorkflowNotificationHelper` for manager resolution.
3. Build a recipient list: all sponsor user IDs + manager user ID (deduplicated).
4. For each recipient:
   - Create an `AppNotification` with:
     - `Type = "Project"`
     - `ActionType = "ProjectCreated"`
     - `RelatedEntityType = "Project"`
     - `RelatedEntityId = project.Id`
     - `RelatedEntityCode = project.ProjectCode`
     - `Title = "New Project Created"`
     - `Message = "{CreatorName} created project {ProjectCode} — {ProjectName}"`
     - `Url = "/projects/{project.Id}"`
   - Send via SignalR hub (use the existing `IHubContext<NotificationHub>` pattern)
5. Send email using the `ProjectCreated` template to each recipient's email address. Use the existing `IEmailTemplateService` and email sending pattern.

#### `SubmitForApprovalAsync`
1. Load the project. Validate:
   - Project exists and is not deleted
   - Project status is `Draft` (reject if already `PendingApproval`, `Active`, etc.)
   - The requesting user is the project manager (`ProjectManagerUserId`)
   - No other `Pending` approval request exists for this project
2. Create a `ProjectApprovalRequest` record with status `Pending`.
3. Update project status to `PendingApproval`.
4. Resolve reviewers: all sponsor user IDs + requester's direct line manager user ID (deduplicated, same manager resolution as above).
5. For each reviewer, create an `AppNotification` with `ActionType = "ProjectApprovalRequested"` and send via SignalR.
6. Send email using the `ProjectApprovalRequested` template to each reviewer.
7. Save changes and return the mapped DTO.

#### `ReviewApprovalRequestAsync`
1. Load the approval request with its Project. Validate:
   - Request exists and is `Pending`
   - The reviewer is authorized (is a sponsor of the project OR is the project manager's direct line manager)
2. Update the approval request: set `Status`, `ReviewedByUserId`, `ReviewedByDisplayName`, `ReviewComment`, `ReviewedAt`.
3. If **approved**:
   - Set project status to `Active`
   - Set `ActualStartDate` to `DateTime.UtcNow`
   - Set `IsBaselined` to `true`
   - Baseline all project milestones: for each milestone that doesn't already have baseline dates set, copy `PlannedStartDate` → `BaselinePlannedStartDate` and `PlannedDueDate` → `BaselinePlannedDueDate`
   - Reject all other pending approval requests for the same project (set status to `Rejected` with comment "Auto-closed: project approved by another reviewer")
4. If **rejected**:
   - Set project status back to `Draft`
5. Create an `AppNotification` for the project manager with `ActionType = "ProjectApprovalApproved"` or `"ProjectApprovalRejected"`, send via SignalR.
6. Send email using `ProjectApprovalReviewed` template to the project manager.
7. Save and return the mapped DTO.

#### `GetApprovalRequestsForProjectAsync`
- Query `ProjectApprovalRequests` where `ProjectId` matches, ordered by `CreatedAt` descending.
- Map to `ProjectApprovalRequestDto` list.

#### `GetPendingReviewsAsync`
- Resolve which projects the user can review: projects where the user is a sponsor (`ProjectSponsor` table) or is the direct line manager of the project manager.
- Return all `Pending` approval requests for those projects, ordered by `CreatedAt` descending.

#### `GetMyRequestsAsync`
- Query `ProjectApprovalRequests` where `RequestedByUserId` matches, ordered by `CreatedAt` descending.

#### `GetPendingSummaryAsync`
- Count pending approval requests where the current user is a reviewer (same logic as `GetPendingReviewsAsync` but returns only the count).

#### `CanReviewProjectAsync`
- Return `true` if the user is a sponsor of the project OR is the project manager's direct line manager.

### 3. Register in DI

Register `IProjectWorkflowService` → `ProjectWorkflowService` as scoped in the service registration (find the existing DI registration file in `ActionTracker.API` or `ActionTracker.Infrastructure`).

## Files to create
- `ActionTracker.Application/Interfaces/IProjectWorkflowService.cs`
- `ActionTracker.Infrastructure/Services/ProjectWorkflowService.cs`

## Files to modify
- DI registration file — add the new service registration

## Do NOT
- Do not create controllers in this step
- Do not modify existing services yet (date freeze enforcement is in B-PW-06)
- Do not modify the `NotificationHub` — reuse the existing hub context injection pattern
