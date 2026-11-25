namespace ActionTracker.Application.Features.ActionItemUpdates;

/// <summary>
/// An edit, submit or delete of a submitted action update (WO-2026-11 A-3,
/// Q11) — 409 for every role, Admin included. A type of its own so the
/// controller does not turn unrelated <see cref="InvalidOperationException"/>s
/// (EF Core, for one) into 409s.
/// </summary>
public sealed class ActionItemUpdateLockedException : InvalidOperationException
{
    public const string SubmittedMessage =
        "This action update has been submitted. A submitted update cannot be changed or deleted.";

    public ActionItemUpdateLockedException() : base(SubmittedMessage) { }
}
