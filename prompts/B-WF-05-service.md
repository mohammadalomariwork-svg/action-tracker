# B-WF-05 — Action Item Workflow Service

## Context

This is the core business logic for the workflow feature. It orchestrates: creating workflow requests, reviewing (approve/reject) them, applying approved changes to the action item, sending notifications (in-app + email), and resolving the direct manager of the assignee from `KuEmployeeInfo`.

Existing services to inject and use:
- `INotificationService` — creates in-app `AppNotification` records
- `IEmailTemplateService` — retrieves email templates by `TemplateKey`, logs delivery
- `ApplicationDbContext` — for direct EF queries
- `IOrgUnitScopeResolver` — if org-unit scoping is needed
- SMTP sending — check how existing email sending works. There may be an `IEmailSender` or the SMTP logic might be in the email template service. Use whatever pattern exists for actually sending emails via MailKit.

Key existing tables referenced:
- `KuEmployeeInfo` — has columns for employee info including manager details. Check the entity for exact column names; the key columns are the employee's email, the direct manager's email/name. The manager lookup will be: find the `KuEmployeeInfo` row where the employee's email matches the assignee's email, then read the manager fields from that row.

## Pre-requisite

- B-WF-01 through B-WF-04 completed

## Instructions

### 1. Create `IActionItemWorkflowService` interface

**File:** `ActionTracker.Application/Interfaces/IActionItemWorkflowService.cs` (new)

```csharp
public interface IActionItemWorkflowService
{
    // Date change requests
    Task<WorkflowRequestResponseDto> CreateDateChangeRequestAsync(
        CreateDateChangeRequestDto dto, string requestedByUserId);
    
    // Status change requests
    Task<WorkflowRequestResponseDto> CreateStatusChangeRequestAsync(
        CreateStatusChangeRequestDto dto, string requestedByUserId);
    
    // Review (approve/reject) any workflow request
    Task<WorkflowRequestResponseDto> ReviewRequestAsync(
        Guid requestId, ReviewWorkflowRequestDto dto, string reviewerUserId);
    
    // Get requests pending review for the current user
    // (user is either the action item creator OR the direct manager of an assignee)
    Task<PagedResult<WorkflowRequestResponseDto>> GetPendingRequestsForReviewerAsync(
        string reviewerUserId, int page, int pageSize);
    
    // Get requests created by the current user
    Task<PagedResult<WorkflowRequestResponseDto>> GetMyRequestsAsync(
        string userId, int page, int pageSize);
    
    // Get all requests for a specific action item
    Task<List<WorkflowRequestResponseDto>> GetRequestsForActionItemAsync(Guid actionItemId);
    
    // Summary counts for the current user (for badge)
    Task<WorkflowRequestSummaryDto> GetPendingSummaryAsync(string userId);
    
    // Escalation workflow: send notifications to creator + direct manager
    Task HandleEscalationAsync(Guid actionItemId, string escalatedByUserId, string reason);
    
    // Give direction on escalated item (adds high-importance comment)
    Task GiveDirectionAsync(WorkflowDirectionDto dto, string directorUserId);
    
    // Check if the given user can review requests for a specific action item
    Task<bool> CanUserReviewAsync(Guid actionItemId, string userId);
    
    // Check if there are pending requests blocking an action item
    Task<bool> HasPendingRequestsAsync(Guid actionItemId);
}
```

### 2. Create `ActionItemWorkflowService` implementation

**File:** `ActionTracker.Infrastructure/Services/ActionItemWorkflowService.cs` (new)

#### Constructor Dependencies:
- `ApplicationDbContext _context`
- `INotificationService _notificationService`
- `IEmailTemplateService _emailTemplateService`
- SMTP sender (whatever exists — `IEmailSender`, `SmtpService`, etc.)
- `IConfiguration _configuration` (for `App:FrontendBaseUrl`)
- `ILogger<ActionItemWorkflowService>`
- `UserManager<ApplicationUser>` (to resolve user details)

#### Key Implementation Details:

**A. `CreateDateChangeRequestAsync`:**
1. Load the `ActionItem` by ID (include `Assignees`). Throw 404 if not found or deleted.
2. Verify the action item is NOT in `Completed`, `Cancelled`, or `Deferred` status.
3. Check there is no existing `Pending` DateChangeRequest for this action item. If one exists, return a 409 Conflict (only one pending request at a time).
4. Create `ActionItemWorkflowRequest` with:
   - `RequestType = DateChangeRequest`
   - `Status = Pending`
   - Snapshot `CurrentStartDate`, `CurrentDueDate` from the action item
   - Set requester info (resolve display name from `UserManager`)
5. Save to DB.
6. **Notify the action item creator:**
   - Create an in-app `AppNotification` for `CreatedByUserId` with type `ActionItem`, action type `DateChangeRequested`
   - Send email using template `ActionItem.DateChangeRequested`
7. **Also notify direct manager of each assignee:**
   - For each assignee in `ActionItemAssignee`, look up their `KuEmployeeInfo` by matching email
   - Get the manager's email from the `KuEmployeeInfo` record
   - Look up the manager's `ApplicationUser` by email (if they exist in the system)
   - Send in-app notification + email to the manager
8. Return the mapped `WorkflowRequestResponseDto`.

**B. `CreateStatusChangeRequestAsync`:**
1. Load the `ActionItem` by ID (include `Assignees`).
2. Validate the status transition: only allow requests from `ToDo`, `InProgress`, `OnHold`, `Overdue` to `Completed`, `OnHold`, `Deferred`, `Cancelled`.
3. Check no existing `Pending` StatusChangeRequest for this action item.
4. Create `ActionItemWorkflowRequest` with `RequestType = StatusChangeRequest`, snapshot `CurrentStatus`.
5. **Notify creator + direct manager(s)** (same pattern as date change).
6. Return the mapped DTO.

**C. `ReviewRequestAsync`:**
1. Load the `ActionItemWorkflowRequest` by ID (include `ActionItem` with `Assignees`).
2. Verify the request is `Pending`. If not, return 409.
3. Verify the reviewer is authorized (is the action item's creator OR a direct manager of an assignee — call `CanUserReviewAsync`).
4. Update the request: set `Status` to Approved or Rejected, set reviewer info, set `ReviewedAt = DateTime.UtcNow`.
5. **If Approved and DateChangeRequest:**
   - Update `ActionItem.StartDate` to `RequestedNewStartDate` (if provided)
   - Update `ActionItem.DueDate` to `RequestedNewDueDate` (if provided)
   - Update `ActionItem.UpdatedAt`
6. **If Approved and StatusChangeRequest:**
   - Update `ActionItem.Status` to `RequestedNewStatus`
   - If status is `Completed`, set `ActionItem.Progress = 100`
   - Update `ActionItem.UpdatedAt`
7. Save all changes.
8. **Notify the requester** via in-app notification + email using `ActionItem.DateChangeReviewed` or `ActionItem.StatusChangeReviewed` template.
9. Return the mapped DTO.

**D. `GetPendingRequestsForReviewerAsync`:**
1. Find all `Pending` workflow requests where:
   - The action item's `CreatedByUserId` matches the reviewer, OR
   - The reviewer is a direct manager of any assignee (lookup via `KuEmployeeInfo`)
2. Order by `CreatedAt` descending.
3. Apply pagination.
4. Map to `WorkflowRequestResponseDto`.

**E. `HandleEscalationAsync`:**
1. Load the `ActionItem` (include `Assignees`, `CreatedByUserId`).
2. Set `ActionItem.IsEscalated = true`.
3. Create an `ActionItemEscalation` record (if the entity supports it — check its fields).
4. **Notify the creator:**
   - In-app notification with type `ActionItem`, action `Escalated`
   - Email using `ActionItem.Escalated` template
5. **Notify direct manager of each assignee:**
   - Same KuEmployeeInfo lookup pattern
   - In-app notification + email

**F. `GiveDirectionAsync`:**
1. Load the action item, verify it is escalated.
2. Verify the user is the creator or a direct manager of an assignee.
3. Create a `Comment` (using the polymorphic comment system) with `RelatedEntityType = "ActionItem"`, `IsHighImportance = true`, content = `dto.DirectionText`.
4. **Notify assignees** via in-app notification + email using `ActionItem.DirectionGiven` template.

**G. `CanUserReviewAsync`:**
1. Load the action item's `CreatedByUserId` and `Assignees`.
2. If `userId == CreatedByUserId`, return true.
3. For each assignee, look up `KuEmployeeInfo` and check if the user's email matches the manager's email.
4. Return true if any match, false otherwise.

**Helper: Manager Lookup**
Create a private method `GetDirectManagerUserIdAsync(string assigneeUserId)`:
1. Get the assignee's `ApplicationUser` to find their email.
2. Query `KuEmployeeInfo` where employee email matches.
3. From the matched record, get the manager's email.
4. Query `ApplicationUser` where email matches the manager's email.
5. Return the manager's `Id` (or null if not found in the system).

Also create `GetDirectManagerEmailAsync(string assigneeUserId)` that returns the raw manager email from `KuEmployeeInfo` (for sending email even if the manager doesn't have an account).

### 3. Register in DI

**File:** `ActionTracker.API/Program.cs` or the DI extension method file (wherever services are registered)

Add:
```csharp
services.AddScoped<IActionItemWorkflowService, ActionItemWorkflowService>();
```

### 4. Email sending helper

Create a private method in the service for sending templated emails:

```csharp
private async Task SendTemplatedEmailAsync(
    string templateKey,
    string recipientEmail,
    string recipientName,
    Dictionary<string, string> placeholders)
```

This method should:
1. Call `_emailTemplateService` to get the template by key
2. Replace all `{{Placeholder}}` tokens in the subject and body
3. Send via SMTP (use whatever SMTP pattern exists in the project)
4. Log the delivery via `EmailLog`

## Validation

- `dotnet build` passes
- All async methods
- Proper null checks and 404/409 error handling
- Email sending failures should be caught and logged but NOT prevent the workflow from completing (fire-and-forget pattern with try/catch)
- All notification creation follows the existing `AppNotification` field pattern (Type, ActionType, RelatedEntityType, RelatedEntityId, RelatedEntityCode, Url)
