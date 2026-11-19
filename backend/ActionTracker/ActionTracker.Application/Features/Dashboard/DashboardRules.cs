using System.Linq.Expressions;
using ActionTracker.Domain.Entities;
using ActionTracker.Domain.Enums;

namespace ActionTracker.Application.Features.Dashboard;

/// <summary>
/// The definitions the dashboards count by ("open", "overdue", the due and age
/// windows). Each rule exists twice on purpose: as a plain function for
/// aggregating rows already in memory, and as an expression for filtering the
/// action-item list in the database. A chart segment and the list it drills
/// down to must agree, so change both together —
/// <c>DashboardAnalyticsServiceTests</c> asserts that they do.
/// </summary>
public static class ActionItemRules
{
    // ── Due windows (open items only) ────────────────────────────────────────
    public const string DueOverdue = "overdue";
    public const string DueNext7   = "next7";
    public const string DueNext30  = "next30";
    public const string DueLater   = "later";

    public static readonly (string Key, string Label)[] DueBuckets =
    {
        (DueOverdue, "Overdue"),
        (DueNext7,   "Due in 7 days"),
        (DueNext30,  "Due in 8–30 days"),
        (DueLater,   "Later"),
    };

    // ── Age windows (open items only, by days since creation) ────────────────
    public const string Age0To7   = "0-7";
    public const string Age8To30  = "8-30";
    public const string Age31To90 = "31-90";
    public const string Age90Plus = "90+";

    public static readonly (string Key, string Label)[] AgeBuckets =
    {
        (Age0To7,   "0–7 days"),
        (Age8To30,  "8–30 days"),
        (Age31To90, "31–90 days"),
        (Age90Plus, "Over 90 days"),
    };

    /// <summary>Still needs work. Done, Cancelled and Deferred items are not open.</summary>
    public static bool IsOpen(ActionStatus status) =>
        status == ActionStatus.ToDo || status == ActionStatus.InProgress ||
        status == ActionStatus.InReview || status == ActionStatus.Overdue;

    /// <summary>
    /// Open and either flagged Overdue or past its due date. The date check
    /// matters because the Overdue status is only applied by a periodic job.
    /// </summary>
    public static bool IsOverdue(ActionStatus status, DateTime dueDate, DateTime today) =>
        IsOpen(status) && (status == ActionStatus.Overdue || dueDate < today);

    /// <summary>The due window an item falls in, or null when it is not open.</summary>
    public static string? DueBucket(ActionStatus status, DateTime dueDate, DateTime today)
    {
        if (!IsOpen(status)) return null;
        if (IsOverdue(status, dueDate, today)) return DueOverdue;
        if (dueDate < today.AddDays(8))  return DueNext7;
        if (dueDate < today.AddDays(31)) return DueNext30;
        return DueLater;
    }

    /// <summary>The age window an item falls in, or null when it is not open.</summary>
    public static string? AgeBucket(ActionStatus status, DateTime createdAt, DateTime today)
    {
        if (!IsOpen(status)) return null;
        if (createdAt >= today.AddDays(-7))  return Age0To7;
        if (createdAt >= today.AddDays(-30)) return Age8To30;
        if (createdAt >= today.AddDays(-90)) return Age31To90;
        return Age90Plus;
    }

    // ── The same rules as database predicates ────────────────────────────────

    public static Expression<Func<ActionItem, bool>> OpenPredicate() => a =>
        a.Status == ActionStatus.ToDo || a.Status == ActionStatus.InProgress ||
        a.Status == ActionStatus.InReview || a.Status == ActionStatus.Overdue;

    public static Expression<Func<ActionItem, bool>> OverduePredicate(DateTime today) => a =>
        a.Status == ActionStatus.Overdue ||
        ((a.Status == ActionStatus.ToDo || a.Status == ActionStatus.InProgress ||
          a.Status == ActionStatus.InReview) && a.DueDate < today);

    /// <summary>Predicate for a due window; null for an unknown key (caller ignores the filter).</summary>
    public static Expression<Func<ActionItem, bool>>? DueBucketPredicate(string? bucket, DateTime today)
    {
        var in8  = today.AddDays(8);
        var in31 = today.AddDays(31);

        return bucket?.Trim().ToLowerInvariant() switch
        {
            DueOverdue => OverduePredicate(today),
            DueNext7 => a =>
                (a.Status == ActionStatus.ToDo || a.Status == ActionStatus.InProgress ||
                 a.Status == ActionStatus.InReview) && a.DueDate >= today && a.DueDate < in8,
            DueNext30 => a =>
                (a.Status == ActionStatus.ToDo || a.Status == ActionStatus.InProgress ||
                 a.Status == ActionStatus.InReview) && a.DueDate >= in8 && a.DueDate < in31,
            DueLater => a =>
                (a.Status == ActionStatus.ToDo || a.Status == ActionStatus.InProgress ||
                 a.Status == ActionStatus.InReview) && a.DueDate >= in31,
            _ => null,
        };
    }

    /// <summary>Predicate for an age window; null for an unknown key (caller ignores the filter).</summary>
    public static Expression<Func<ActionItem, bool>>? AgeBucketPredicate(string? bucket, DateTime today)
    {
        var d7  = today.AddDays(-7);
        var d30 = today.AddDays(-30);
        var d90 = today.AddDays(-90);

        return bucket?.Trim().ToLowerInvariant() switch
        {
            Age0To7 => a =>
                (a.Status == ActionStatus.ToDo || a.Status == ActionStatus.InProgress ||
                 a.Status == ActionStatus.InReview || a.Status == ActionStatus.Overdue) && a.CreatedAt >= d7,
            Age8To30 => a =>
                (a.Status == ActionStatus.ToDo || a.Status == ActionStatus.InProgress ||
                 a.Status == ActionStatus.InReview || a.Status == ActionStatus.Overdue) &&
                a.CreatedAt < d7 && a.CreatedAt >= d30,
            Age31To90 => a =>
                (a.Status == ActionStatus.ToDo || a.Status == ActionStatus.InProgress ||
                 a.Status == ActionStatus.InReview || a.Status == ActionStatus.Overdue) &&
                a.CreatedAt < d30 && a.CreatedAt >= d90,
            Age90Plus => a =>
                (a.Status == ActionStatus.ToDo || a.Status == ActionStatus.InProgress ||
                 a.Status == ActionStatus.InReview || a.Status == ActionStatus.Overdue) && a.CreatedAt < d90,
            _ => null,
        };
    }
}

/// <summary>How the dashboards judge a project's progress and health.</summary>
public static class ProjectRules
{
    public const string HealthOnTrack    = "OnTrack";
    public const string HealthAtRisk     = "AtRisk";
    public const string HealthDelayed    = "Delayed";
    public const string HealthCompleted  = "Completed";
    public const string HealthNotStarted = "NotStarted";
    public const string HealthOnHold     = "OnHold";
    public const string HealthCancelled  = "Cancelled";

    public static string HealthLabel(string health) => health switch
    {
        HealthOnTrack    => "On track",
        HealthAtRisk     => "At risk",
        HealthDelayed    => "Delayed",
        HealthCompleted  => "Completed",
        HealthNotStarted => "Not started",
        HealthOnHold     => "On hold",
        HealthCancelled  => "Cancelled",
        _                => health,
    };

    /// <summary>
    /// 0–100. A completed project is 100. Otherwise the share of its action
    /// items that are done (the same measure as the project page's completion
    /// rate); a project with no action items falls back to the mean completion
    /// of its milestones.
    /// </summary>
    public static decimal Progress(
        ProjectStatus status, int actionsTotal, int actionsDone, IReadOnlyCollection<decimal> milestoneCompletion)
    {
        if (status == ProjectStatus.Completed) return 100m;
        if (actionsTotal > 0) return Math.Round((decimal)actionsDone / actionsTotal * 100m, 1);
        if (milestoneCompletion.Count > 0) return Math.Round(milestoneCompletion.Average(), 1);
        return 0m;
    }

    /// <summary>
    /// Delayed: active and past its planned end date. At risk: active, not yet
    /// late, but carrying overdue action items or late milestones. Everything
    /// else that is active is on track.
    /// </summary>
    public static string Health(
        ProjectStatus status, DateTime plannedEndDate, int actionsOverdue, int milestonesDelayed, DateTime today) =>
        status switch
        {
            ProjectStatus.Completed       => HealthCompleted,
            ProjectStatus.Cancelled       => HealthCancelled,
            ProjectStatus.OnHold          => HealthOnHold,
            ProjectStatus.Draft           => HealthNotStarted,
            ProjectStatus.PendingApproval => HealthNotStarted,
            _ when plannedEndDate < today                      => HealthDelayed,
            _ when actionsOverdue > 0 || milestonesDelayed > 0 => HealthAtRisk,
            _                                                  => HealthOnTrack,
        };

    /// <summary>A milestone is late when flagged Delayed, or still open past its due date.</summary>
    public static bool IsMilestoneLate(MilestoneStatus status, DateTime plannedDueDate, DateTime today) =>
        status == MilestoneStatus.Delayed ||
        ((status == MilestoneStatus.NotStarted || status == MilestoneStatus.InProgress) && plannedDueDate < today);
}

/// <summary>Direction-aware KPI achievement and the health bands built on it.</summary>
public static class KpiAchievement
{
    public const string HealthOnTrack  = "OnTrack";
    public const string HealthAtRisk   = "AtRisk";
    public const string HealthOffTrack = "OffTrack";
    public const string HealthNoData   = "NoData";

    /// <summary>At or above this is on track.</summary>
    public const decimal OnTrackThreshold = 90m;
    /// <summary>At or above this (and below on-track) is at risk; below is off track.</summary>
    public const decimal AtRiskThreshold  = 60m;

    /// <summary>
    /// Achievement of an actual against its target as a percentage, respecting
    /// whether higher, lower or exactly-on-target is better. Null when the
    /// target is missing or zero.
    /// </summary>
    public static decimal? Compute(decimal actual, decimal? target, KpiDirection direction)
    {
        if (target is null or 0) return null;
        var t = target.Value;

        return direction switch
        {
            KpiDirection.HigherIsBetter => Math.Round((actual / t) * 100m, 2),
            KpiDirection.LowerIsBetter when actual == 0 => 100m,
            KpiDirection.LowerIsBetter => Math.Round((t / actual) * 100m, 2),
            KpiDirection.OnTarget when actual == t => 100m,
            KpiDirection.OnTarget => Math.Round(100m - Math.Abs((actual - t) / t) * 100m, 2),
            _ => null,
        };
    }

    public static string Health(decimal? achievement) => achievement switch
    {
        null                => HealthNoData,
        >= OnTrackThreshold => HealthOnTrack,
        >= AtRiskThreshold  => HealthAtRisk,
        _                   => HealthOffTrack,
    };

    public static string HealthLabel(string health) => health switch
    {
        HealthOnTrack  => "On track",
        HealthAtRisk   => "At risk",
        HealthOffTrack => "Off track",
        HealthNoData   => "No data yet",
        _              => health,
    };
}
