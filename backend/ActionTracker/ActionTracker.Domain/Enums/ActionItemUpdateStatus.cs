namespace ActionTracker.Domain.Enums;

/// <summary>
/// Life cycle of an action update (WO-2026-11 A-3). One-way: a draft can be
/// submitted, a submitted update never goes back to draft.
/// </summary>
public enum ActionItemUpdateStatus
{
    /// <summary>Visible and editable by its author only; can be deleted.</summary>
    Draft = 0,

    /// <summary>Visible to everyone who can see the action item; read-only for every role.</summary>
    Submitted = 1,
}
