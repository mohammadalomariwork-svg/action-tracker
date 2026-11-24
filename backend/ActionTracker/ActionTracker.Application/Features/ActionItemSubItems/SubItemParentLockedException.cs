namespace ActionTracker.Application.Features.ActionItemSubItems;

/// <summary>
/// A sub-item write on a Done or Cancelled parent (WO-2026-11 A-2) — 409.
/// A type of its own so the controller does not turn unrelated
/// <see cref="InvalidOperationException"/>s (EF Core, for one) into 409s.
/// </summary>
public sealed class SubItemParentLockedException : InvalidOperationException
{
    public SubItemParentLockedException(string message) : base(message) { }
}
