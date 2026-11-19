using ActionTracker.Domain.Enums;

namespace ActionTracker.Application.Features.ActionItems.DTOs;

public class ActionItemFilterDto
{
    public ActionStatus?   Status      { get; set; }
    public ActionPriority? Priority    { get; set; }
    public string          AssigneeId   { get; set; } = string.Empty;
    public string          CreatedById  { get; set; } = string.Empty;
    public Guid?           WorkspaceId { get; set; }
    public Guid?           ProjectId   { get; set; }
    public Guid?           MilestoneId { get; set; }
    public bool?           IsStandalone { get; set; }
    public string          SearchTerm  { get; set; } = string.Empty;

    // ── Dashboard drill-down filters ─────────────────────────────────────────
    // These use the same definitions the dashboards count by
    // (ActionItemRules), so a list opened from a chart segment matches it.

    /// <summary>Only items that still need work (To Do / In Progress / In Review / Overdue).</summary>
    public bool? OpenOnly    { get; set; }
    /// <summary>Only open items that are flagged Overdue or past their due date.</summary>
    public bool? OverdueOnly { get; set; }
    public bool? IsEscalated { get; set; }
    /// <summary>Due window for open items: overdue | next7 | next30 | later.</summary>
    public string? DueBucket { get; set; }
    /// <summary>Age window for open items (days since creation): 0-7 | 8-30 | 31-90 | 90+.</summary>
    public string? AgeBucket { get; set; }

    public int PageNumber { get; set; } = 1;

    private int _pageSize = 20;
    public int PageSize
    {
        get => _pageSize;
        set => _pageSize = value > 100 ? 100 : value < 1 ? 1 : value;
    }

    public string SortBy          { get; set; } = "DueDate";
    public bool   SortDescending  { get; set; } = false;
    public bool   IncludeDeleted  { get; set; } = false;

    /// <summary>
    /// When non-null and non-empty, only action items whose workspace OrgUnitId is
    /// in this list (or is null) are returned. Populated server-side from the
    /// user's org unit scope — never sent from the client.
    /// </summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public List<Guid>? VisibleOrgUnitIds { get; set; }

    /// <summary>
    /// When non-null, only action items belonging to one of these workspace IDs are
    /// returned. Populated server-side from <c>IWorkspaceAccessResolver</c>; a
    /// <c>null</c> value means "no workspace restriction" (privileged role).
    /// </summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public List<Guid>? VisibleWorkspaceIds { get; set; }
}
