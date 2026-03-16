namespace ActionTracker.Application.Features.ActionItems.DTOs;

/// <summary>
/// Aggregate statistics for action items assigned to the currently authenticated user.
/// </summary>
public class ActionItemMyStatsDto
{
    /// <summary>Total non-deleted action items assigned to the user.</summary>
    public int TotalCount { get; set; }

    /// <summary>Action items with <c>Critical</c> priority.</summary>
    public int CriticalCount { get; set; }

    /// <summary>Action items with <c>InProgress</c> status.</summary>
    public int InProgressCount { get; set; }

    /// <summary>Action items with <c>Done</c> status.</summary>
    public int CompletedCount { get; set; }

    /// <summary>Action items with <c>Overdue</c> status.</summary>
    public int OverdueCount { get; set; }

    /// <summary>Percentage of all assigned items that are Done (0–100, 1 decimal place).</summary>
    public decimal CompletionRate { get; set; }

    /// <summary>
    /// Of all Done items, the percentage completed on or before the due date
    /// (0–100, 1 decimal place).
    /// </summary>
    public decimal OnTimeCompletionRate { get; set; }
}
