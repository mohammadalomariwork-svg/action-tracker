# B-WF-09 — In-App Notification Infrastructure for Workflow Events

## Context

The system already has `AppNotification` entity, `INotificationService`, `NotificationsController`, and a SignalR hub for real-time push. Notifications have `Type` (entity type like "ActionItem", "Project") and `ActionType` (what happened, like "Created", "Updated"). We need to define new `ActionType` values for workflow events and ensure the notification service creates properly structured records that the frontend can route and display correctly.

Check the existing notification creation pattern — look at how notifications are created elsewhere (e.g., when an action item is assigned or a project is updated) and replicate that exact pattern.

## Pre-requisite

- B-WF-05 completed (workflow service exists but notification calls may be generic)

## Instructions

### 1. Define workflow notification action types

Find where notification `ActionType` strings are defined. This could be:
- An enum in the Domain layer (e.g., `NotificationActionType`)
- String constants in a constants file
- Inline strings in the services

Add the following new action type values (use whatever pattern exists):

| ActionType                | When Created                                           |
|---------------------------|--------------------------------------------------------|
| `DateChangeRequested`     | Someone submits a date change request                  |
| `DateChangeApproved`      | A date change request is approved                      |
| `DateChangeRejected`      | A date change request is rejected                      |
| `StatusChangeRequested`   | Someone submits a status change request                |
| `StatusChangeApproved`    | A status change request is approved                    |
| `StatusChangeRejected`    | A status change request is rejected                    |
| `ActionItemEscalated`     | An action item is escalated                            |
| `EscalationDirectionGiven`| Creator or manager gives direction on escalated item   |

### 2. Create a dedicated workflow notification helper

**File:** `ActionTracker.Infrastructure/Services/WorkflowNotificationHelper.cs` (new)

This is a helper class (injected as scoped) that encapsulates all notification creation logic for workflow events. This keeps the main `ActionItemWorkflowService` clean and makes the notification logic reusable and testable.

```csharp
public interface IWorkflowNotificationHelper
{
    /// <summary>
    /// Notify the action item creator and direct manager(s) of assignees
    /// that a date change has been requested.
    /// </summary>
    Task NotifyDateChangeRequestedAsync(ActionItem actionItem, ActionItemWorkflowRequest request);

    /// <summary>
    /// Notify the requester that their date change was approved or rejected.
    /// </summary>
    Task NotifyDateChangeReviewedAsync(ActionItem actionItem, ActionItemWorkflowRequest request);

    /// <summary>
    /// Notify the action item creator and direct manager(s) of assignees
    /// that a status change has been requested.
    /// </summary>
    Task NotifyStatusChangeRequestedAsync(ActionItem actionItem, ActionItemWorkflowRequest request);

    /// <summary>
    /// Notify the requester that their status change was approved or rejected.
    /// </summary>
    Task NotifyStatusChangeReviewedAsync(ActionItem actionItem, ActionItemWorkflowRequest request);

    /// <summary>
    /// Notify the creator and direct manager(s) of assignees that the action item
    /// has been escalated.
    /// </summary>
    Task NotifyEscalationAsync(ActionItem actionItem, string escalatedByUserId, string reason);

    /// <summary>
    /// Notify all assignees that direction has been given on their escalated item.
    /// </summary>
    Task NotifyDirectionGivenAsync(ActionItem actionItem, string directorUserId, string directionText);
}
```

### 3. Implementation Details

For each notification method:

**A. Resolve recipients:**
- **Creator:** `actionItem.CreatedByUserId` — always a recipient for requests on their items
- **Direct manager(s):** For each assignee in `actionItem.Assignees`:
  1. Get the assignee's `ApplicationUser` to find their email
  2. Query `KuEmployeeInfo` where employee email matches
  3. From the matched record, get the manager's email/name
  4. Query `ApplicationUser` where email matches the manager's email
  5. If the manager has an account in the system, they get an in-app notification
  6. If the manager does NOT have an account, they only get an email (no in-app notification)
- **Requester:** For review notifications, the requester is `request.RequestedByUserId`
- **Assignees:** For direction-given notifications, all users in `actionItem.Assignees`
- **Deduplicate:** If the creator is also an assignee's manager, they get ONE notification, not two

**B. Create `AppNotification` record for each recipient:**

Follow the exact field pattern from existing notification creation. Each notification should have:

```csharp
new AppNotification
{
    Id = Guid.NewGuid(),
    UserId = recipientUserId,
    Title = "...",            // concise title
    Message = "...",          // descriptive message
    Type = "ActionItem",      // entity type — always "ActionItem" for workflow
    ActionType = "...",       // one of the new action types from step 1
    RelatedEntityType = "ActionItem",
    RelatedEntityId = actionItem.Id,
    RelatedEntityCode = actionItem.ActionId,  // e.g., "ACT-042"
    Url = $"/actions/{actionItem.Id}/view",   // frontend deep link path
    IsRead = false,
    CreatedAt = DateTime.UtcNow
}
```

**Notification titles and messages:**

| ActionType                | Title                                        | Message                                                                   |
|---------------------------|----------------------------------------------|---------------------------------------------------------------------------|
| `DateChangeRequested`     | "Date Change Requested"                      | "{RequesterName} requested a date change on {ActionItemCode}: {Title}"    |
| `DateChangeApproved`      | "Date Change Approved"                       | "Your date change request for {ActionItemCode} has been approved by {ReviewerName}" |
| `DateChangeRejected`      | "Date Change Rejected"                       | "Your date change request for {ActionItemCode} has been rejected by {ReviewerName}" |
| `StatusChangeRequested`   | "Status Change Requested"                    | "{RequesterName} requested to change {ActionItemCode} status to {NewStatus}" |
| `StatusChangeApproved`    | "Status Change Approved"                     | "Your status change request for {ActionItemCode} has been approved by {ReviewerName}" |
| `StatusChangeRejected`    | "Status Change Rejected"                     | "Your status change request for {ActionItemCode} has been rejected by {ReviewerName}" |
| `ActionItemEscalated`     | "Action Item Escalated"                      | "{ActionItemCode}: {Title} has been escalated. Reason: {Reason}"          |
| `EscalationDirectionGiven`| "Direction Received"                         | "{DirectorName} has given direction on escalated item {ActionItemCode}"    |

**C. Send SignalR real-time push:**

After creating each `AppNotification` record, push it via SignalR to the recipient. Check how existing notifications use SignalR — there's likely a hub method like:

```csharp
await _hubContext.Clients.User(recipientUserId).SendAsync("ReceiveNotification", notification);
```

Find the existing SignalR hub (likely `NotificationHub` in `ActionTracker.API/Hubs/`) and the `IHubContext<NotificationHub>` injection pattern. Use the same pattern.

**D. Send email (fire-and-forget):**

After creating the in-app notification, also send the corresponding email template. Wrap in try/catch so email failures don't block the workflow:

```csharp
try
{
    await SendTemplatedEmailAsync(templateKey, recipientEmail, recipientName, placeholders);
}
catch (Exception ex)
{
    _logger.LogError(ex, "Failed to send workflow email {TemplateKey} to {Email}", templateKey, recipientEmail);
}
```

For recipients without a system account (manager not registered), still send the email using their `KuEmployeeInfo` email address.

### 4. Register in DI

Add `IWorkflowNotificationHelper` → `WorkflowNotificationHelper` to the DI container.

### 5. Update `ActionItemWorkflowService` to use the helper

Replace the inline notification logic in B-WF-05's service with calls to the helper:

```csharp
// In CreateDateChangeRequestAsync:
await _notificationHelper.NotifyDateChangeRequestedAsync(actionItem, workflowRequest);

// In ReviewRequestAsync (for date change):
await _notificationHelper.NotifyDateChangeReviewedAsync(actionItem, workflowRequest);

// In CreateStatusChangeRequestAsync:
await _notificationHelper.NotifyStatusChangeRequestedAsync(actionItem, workflowRequest);

// In ReviewRequestAsync (for status change):
await _notificationHelper.NotifyStatusChangeReviewedAsync(actionItem, workflowRequest);

// In HandleEscalationAsync:
await _notificationHelper.NotifyEscalationAsync(actionItem, escalatedByUserId, reason);

// In GiveDirectionAsync:
await _notificationHelper.NotifyDirectionGivenAsync(actionItem, directorUserId, directionText);
```

### 6. Notification URL routing

Set the `Url` field on notifications to enable frontend deep linking:

| ActionType                | Url                                    | Reason                                      |
|---------------------------|----------------------------------------|---------------------------------------------|
| `DateChangeRequested`     | `/approvals`                           | Reviewer should go to approvals page        |
| `StatusChangeRequested`   | `/approvals`                           | Reviewer should go to approvals page        |
| `DateChangeApproved`      | `/actions/{actionItemId}/view`         | Requester sees the updated action item      |
| `DateChangeRejected`      | `/actions/{actionItemId}/view`         | Requester sees the action item              |
| `StatusChangeApproved`    | `/actions/{actionItemId}/view`         | Requester sees the updated status           |
| `StatusChangeRejected`    | `/actions/{actionItemId}/view`         | Requester sees the action item              |
| `ActionItemEscalated`     | `/actions/{actionItemId}/view`         | Creator/manager views the escalated item    |
| `EscalationDirectionGiven`| `/actions/{actionItemId}/view`         | Assignee views the direction comment        |

## Validation

- Each workflow action creates the correct `AppNotification` records for all relevant recipients
- Duplicate recipients are filtered (creator who is also a manager gets one notification)
- SignalR pushes the notification in real-time to online users
- Email is sent but failures are caught and logged
- Managers without system accounts still receive email
- Notification `Url` field enables correct frontend routing
- `dotnet build` passes
- Existing notification functionality is unchanged
