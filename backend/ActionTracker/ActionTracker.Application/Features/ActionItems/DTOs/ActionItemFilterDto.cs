using ActionTracker.Domain.Enums;

namespace ActionTracker.Application.Features.ActionItems.DTOs;

public class ActionItemFilterDto
{
    public ActionStatus?   Status      { get; set; }
    public ActionPriority? Priority    { get; set; }
    public string          AssigneeId  { get; set; } = string.Empty;
    public Guid?           WorkspaceId { get; set; }
    public Guid?           ProjectId   { get; set; }
    public Guid?           MilestoneId { get; set; }
    public bool?           IsStandalone { get; set; }
    public string          SearchTerm  { get; set; } = string.Empty;

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
}
