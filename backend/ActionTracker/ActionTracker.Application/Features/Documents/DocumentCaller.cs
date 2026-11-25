namespace ActionTracker.Application.Features.Documents;

/// <summary>
/// Who is calling the documents service. The two permission flags come from the
/// API layer (<c>ActionItems.View</c> / <c>ActionItems.Edit</c> policies) and
/// matter only for the action-item types (WO-2026-11 A-4); the service applies
/// them after the parent-visibility check, so a hidden parent is always 404.
/// </summary>
public sealed record DocumentCaller(string UserId, bool CanViewActionItems, bool CanEditActionItems)
{
    /// <summary>For callers whose own endpoint policy already decides (the KPI-actual routes).</summary>
    public static DocumentCaller WithoutActionItemRights(string userId) => new(userId, false, false);
}
