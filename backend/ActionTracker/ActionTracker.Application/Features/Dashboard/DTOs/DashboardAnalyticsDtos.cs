namespace ActionTracker.Application.Features.Dashboard.DTOs;

// ─────────────────────────────────────────────────────────────────────────────
// Shared shapes
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>One segment of a chart: a stable key the client sends back when
/// drilling down, a display label and the count behind it.</summary>
public class ChartSliceDto
{
    public string Key   { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public int    Count { get; set; }
}

/// <summary>Action-item counts for one person, used by "Team Activity".</summary>
public class TeamMemberActivityDto
{
    public string  UserId         { get; set; } = string.Empty;
    public string  UserName       { get; set; } = string.Empty;
    public int     Total          { get; set; }
    public int     Open           { get; set; }
    public int     InProgress     { get; set; }
    public int     Completed      { get; set; }
    public int     Overdue        { get; set; }
    public decimal CompletionRate { get; set; }
}

// ─────────────────────────────────────────────────────────────────────────────
// My actions (the caller's own assignments)
// ─────────────────────────────────────────────────────────────────────────────

public class MyActionsDashboardDto
{
    public int     Total          { get; set; }
    public int     Open           { get; set; }
    public int     Completed      { get; set; }
    public int     Overdue        { get; set; }
    public int     DueThisWeek    { get; set; }
    public decimal CompletionRate { get; set; }

    /// <summary>Every item, by status (key = ActionStatus value).</summary>
    public List<ChartSliceDto> ByStatus   { get; set; } = new();
    /// <summary>Open items only, by priority (key = ActionPriority value).</summary>
    public List<ChartSliceDto> ByPriority { get; set; } = new();
    /// <summary>Open items only, by due window (key = ActionItemRules due bucket).</summary>
    public List<ChartSliceDto> ByDue      { get; set; } = new();
}

// ─────────────────────────────────────────────────────────────────────────────
// Action items (everything the caller may see)
// ─────────────────────────────────────────────────────────────────────────────

public class ActionItemsDashboardDto
{
    public int     Total          { get; set; }
    public int     Open           { get; set; }
    public int     Completed      { get; set; }
    public int     Overdue        { get; set; }
    public int     Escalated      { get; set; }
    public int     Standalone     { get; set; }
    public int     ProjectLinked  { get; set; }
    public decimal CompletionRate { get; set; }
    public decimal OnTimeRate     { get; set; }

    public List<ChartSliceDto> ByStatus   { get; set; } = new();
    public List<ChartSliceDto> ByPriority { get; set; } = new();
    public List<ChartSliceDto> ByDue      { get; set; } = new();
    /// <summary>Open items by time since creation (key = ActionItemRules age bucket).</summary>
    public List<ChartSliceDto> ByAge      { get; set; } = new();

    /// <summary>Created vs completed per week, oldest first.</summary>
    public List<ActionTrendPointDto> Trend { get; set; } = new();

    public List<WorkspaceActionsDto>   ByWorkspace { get; set; } = new();
    public List<TeamMemberActivityDto> Team        { get; set; } = new();
}

public class ActionTrendPointDto
{
    public DateTime WeekStart { get; set; }
    public string   Label     { get; set; } = string.Empty;
    public int      Created   { get; set; }
    public int      Completed { get; set; }
}

public class WorkspaceActionsDto
{
    public Guid   WorkspaceId { get; set; }
    public string Title       { get; set; } = string.Empty;
    public int    Total       { get; set; }
    public int    Open        { get; set; }
    public int    Completed   { get; set; }
    public int    Overdue     { get; set; }
}

// ─────────────────────────────────────────────────────────────────────────────
// Projects
// ─────────────────────────────────────────────────────────────────────────────

public class ProjectsDashboardDto
{
    public int     Total       { get; set; }
    public int     Active      { get; set; }
    public int     Completed   { get; set; }
    public int     OnTrack     { get; set; }
    public int     AtRisk      { get; set; }
    public int     Delayed     { get; set; }
    /// <summary>Mean progress of projects that are under way (Active / On Hold).</summary>
    public decimal AvgProgress { get; set; }

    public int MilestonesTotal     { get; set; }
    public int MilestonesCompleted { get; set; }
    public int MilestonesDelayed   { get; set; }
    public int MilestonesDueSoon   { get; set; }

    /// <summary>Every visible project. Charts are grouped from these rows on the
    /// client so a drill-down always lists exactly what the chart counted.</summary>
    public List<ProjectRowDto>   Projects   { get; set; } = new();
    /// <summary>Open milestones that are late or due within 30 days.</summary>
    public List<MilestoneRowDto> Milestones { get; set; } = new();
}

public class ProjectRowDto
{
    public Guid     Id                 { get; set; }
    public string   Code               { get; set; } = string.Empty;
    public string   Name               { get; set; } = string.Empty;
    public Guid     WorkspaceId        { get; set; }
    public string   WorkspaceTitle     { get; set; } = string.Empty;
    public int      Status             { get; set; }
    public string   StatusLabel        { get; set; } = string.Empty;
    public int      ProjectType        { get; set; }
    public string   TypeLabel          { get; set; } = string.Empty;
    public int      Priority           { get; set; }
    public string   PriorityLabel      { get; set; } = string.Empty;
    public string   ProjectManagerName { get; set; } = string.Empty;
    public Guid?    StrategicObjectiveId { get; set; }
    public DateTime PlannedStartDate   { get; set; }
    public DateTime PlannedEndDate     { get; set; }
    /// <summary>0–100. See <c>ProjectRules.Progress</c>.</summary>
    public decimal  Progress           { get; set; }
    public int      ActionsTotal       { get; set; }
    public int      ActionsDone        { get; set; }
    public int      ActionsOverdue     { get; set; }
    public int      MilestonesTotal    { get; set; }
    public int      MilestonesDone     { get; set; }
    public int      MilestonesDelayed  { get; set; }
    /// <summary>One of <c>ProjectRules.Health*</c>.</summary>
    public string   Health             { get; set; } = string.Empty;
    public string   HealthLabel        { get; set; } = string.Empty;
    /// <summary>Days until the planned end date; negative when past it.</summary>
    public int      DaysToEnd          { get; set; }
}

public class MilestoneRowDto
{
    public Guid     Id             { get; set; }
    public string   Code           { get; set; } = string.Empty;
    public string   Name           { get; set; } = string.Empty;
    public Guid     ProjectId      { get; set; }
    public string   ProjectName    { get; set; } = string.Empty;
    public int      Status         { get; set; }
    public string   StatusLabel    { get; set; } = string.Empty;
    public DateTime PlannedDueDate { get; set; }
    public decimal  Completion     { get; set; }
    public bool     IsLate         { get; set; }
    public int      DaysToDue      { get; set; }
}

// ─────────────────────────────────────────────────────────────────────────────
// Strategy
// ─────────────────────────────────────────────────────────────────────────────

public class StrategyDashboardDto
{
    /// <summary>The calendar year KPI achievement is measured for.</summary>
    public int Year { get; set; }

    public int      Plans                      { get; set; }
    public int      Objectives                 { get; set; }
    public int      Kpis                       { get; set; }
    public int      KpisWithData               { get; set; }
    public int      StrategicProjects          { get; set; }
    public int      StrategicProjectsCompleted { get; set; }
    public int      ActualsPendingApproval     { get; set; }
    /// <summary>Mean achievement across KPIs that have approved actuals; null when none do.</summary>
    public decimal? AvgAchievement             { get; set; }

    public List<ChartSliceDto> ActualsByStatus { get; set; } = new();

    public List<StrategyPlanRowDto>      PlanRows      { get; set; } = new();
    public List<StrategyObjectiveRowDto> ObjectiveRows { get; set; } = new();
    public List<StrategyKpiRowDto>       KpiRows       { get; set; } = new();
    /// <summary>Strategic projects linked to the objectives above.</summary>
    public List<ProjectRowDto>           ProjectRows   { get; set; } = new();
}

public class StrategyPlanRowDto
{
    public Guid     Id             { get; set; }
    public string   Name           { get; set; } = string.Empty;
    public Guid     OrgUnitId      { get; set; }
    public string   OrgUnitName    { get; set; } = string.Empty;
    public int      Status         { get; set; }
    public string   StatusLabel    { get; set; } = string.Empty;
    public int      StartYear      { get; set; }
    public int      EndYear        { get; set; }
    public int      ObjectiveCount { get; set; }
    public int      KpiCount       { get; set; }
    public decimal? AvgAchievement { get; set; }
}

public class StrategyObjectiveRowDto
{
    public Guid     Id                 { get; set; }
    public string   Code               { get; set; } = string.Empty;
    public string   Statement          { get; set; } = string.Empty;
    public Guid     PlanId             { get; set; }
    public string   PlanName           { get; set; } = string.Empty;
    public Guid     OrgUnitId          { get; set; }
    public string   OrgUnitName        { get; set; } = string.Empty;
    public int      KpiCount           { get; set; }
    public int      KpisOnTrack        { get; set; }
    public int      KpisAtRisk         { get; set; }
    public int      KpisOffTrack       { get; set; }
    public int      KpisNoData         { get; set; }
    public decimal? AvgAchievement     { get; set; }
    public int      ProjectCount       { get; set; }
    public int      ProjectsCompleted  { get; set; }
    public decimal  AvgProjectProgress { get; set; }
}

public class StrategyKpiRowDto
{
    public Guid     Id            { get; set; }
    public int      KpiNumber     { get; set; }
    public string   Name          { get; set; } = string.Empty;
    public string?  Unit          { get; set; }
    public string   PeriodLabel   { get; set; } = string.Empty;
    public Guid     ObjectiveId   { get; set; }
    public string   ObjectiveCode { get; set; } = string.Empty;
    public Guid     PlanId        { get; set; }
    public Guid     OrgUnitId     { get; set; }
    public string   OrgUnitName   { get; set; } = string.Empty;
    /// <summary>Target rows set for <see cref="StrategyDashboardDto.Year"/>.</summary>
    public int      TargetCount   { get; set; }
    /// <summary>Approved actuals for the year.</summary>
    public int      ActualCount   { get; set; }
    public decimal? Achievement   { get; set; }
    /// <summary>One of <c>KpiAchievement.Health*</c>.</summary>
    public string   Health        { get; set; } = string.Empty;
    public string   HealthLabel   { get; set; } = string.Empty;
}
