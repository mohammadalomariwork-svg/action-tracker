using ActionTracker.Domain.Entities;

namespace ActionTracker.Application.Features.Workflow.Interfaces;

public interface IWorkflowNotificationHelper
{
    Task NotifyDateChangeRequestedAsync(ActionItem actionItem, ActionItemWorkflowRequest request);
    Task NotifyDateChangeReviewedAsync(ActionItem actionItem, ActionItemWorkflowRequest request);
    Task NotifyStatusChangeRequestedAsync(ActionItem actionItem, ActionItemWorkflowRequest request);
    Task NotifyStatusChangeReviewedAsync(ActionItem actionItem, ActionItemWorkflowRequest request);
    Task NotifyEscalationAsync(ActionItem actionItem, string escalatedByUserId, string reason);
    Task NotifyDirectionGivenAsync(ActionItem actionItem, string directorUserId, string directionText);

    // ── Sub-items (WO-2026-11 A-2) ───────────────────────────────────────────
    // Both load what they need by id, so they can run after the request ended.
    // The actor is never notified; inactive users are skipped.

    /// <summary>
    /// In-app (+ SignalR) and e-mail (<c>ActionItem.SubItemAssigned</c>) to each
    /// user in <paramref name="newAssigneeIds"/> except <paramref name="actorUserId"/>.
    /// </summary>
    Task NotifySubItemAssignedAsync(Guid subItemId, IReadOnlyCollection<string> newAssigneeIds, string actorUserId);

    /// <summary>
    /// In-app (+ SignalR) and e-mail (<c>ActionItem.SubItemCompleted</c>) to the
    /// parent's creator, the parent's assignees and the sub-item's other
    /// assignees, except <paramref name="actorUserId"/>.
    /// </summary>
    Task NotifySubItemCompletedAsync(Guid subItemId, string actorUserId);

    // ── Action updates (WO-2026-11 A-3) ─────────────────────────────────────

    /// <summary>
    /// Sent when an action update is submitted — never for a draft. In-app
    /// (+ SignalR) and e-mail (<c>ActionItem.UpdateSubmitted</c>) to the action
    /// item's creator and assignees, and for a sub-item update also the
    /// sub-item's assignees, except <paramref name="actorUserId"/>; inactive
    /// users are skipped. Loads the update by id, so it can run after the
    /// request ended; a draft, deleted update or deleted parent sends nothing.
    /// </summary>
    Task NotifyActionUpdateSubmittedAsync(Guid updateId, string actorUserId);

    // ── Project people (WO-2026-11 P-2) ──────────────────────────────────────

    /// <summary>
    /// In-app + e-mail (<c>Project.PersonAdded</c>) to each person added to the
    /// project, with the role they were given (wire names from
    /// <c>ProjectAccessRoles</c>), except <paramref name="actorUserId"/>; inactive
    /// users are skipped. Loads the project by id, so it can run after the request
    /// ended; a deleted project sends nothing. Every e-mail value is HTML-encoded.
    /// </summary>
    Task NotifyProjectPeopleAddedAsync(Guid projectId, IReadOnlyCollection<(string UserId, string Role)> added, string? actorUserId);
}
