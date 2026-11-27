namespace ActionTracker.Application.Features.ActionItems;

/// <summary>
/// WO-2026-11 P-2b (B4): direction can only be given on an escalated action item —
/// 409 with the message (<c>ActionItemWorkflowController</c>).
/// </summary>
public sealed class ActionItemNotEscalatedException : InvalidOperationException
{
    public const string DefaultMessage = "Direction can only be given on an escalated action item.";

    public ActionItemNotEscalatedException() : base(DefaultMessage) { }
}
