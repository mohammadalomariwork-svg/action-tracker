namespace ActionTracker.Application.Features.Documents;

/// <summary>
/// An upload or delete refused because the owning record is locked (WO-2026-11
/// A-4) — a Done / Cancelled action item, a sub-item of one, or a submitted
/// action update — 409. A type of its own so the controller does not turn
/// unrelated <see cref="InvalidOperationException"/>s (EF Core, for one) into 409s.
/// </summary>
public sealed class DocumentLockedException : InvalidOperationException
{
    public DocumentLockedException(string message) : base(message) { }
}
