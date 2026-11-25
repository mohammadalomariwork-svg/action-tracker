namespace ActionTracker.Application.Features.Documents;

/// <summary>
/// The owning record types <c>/api/documents</c> serves (WO-2026-11 A-4). Any
/// other type is refused (400): a document is only reachable when the caller
/// can see its parent, and an unknown type has no parent to check.
/// </summary>
public static class DocumentEntityTypes
{
    public const string ActionItem        = "ActionItem";
    public const string ActionItemSubItem = "ActionItemSubItem";
    public const string ActionItemUpdate  = "ActionItemUpdate";
    public const string Project           = "Project";
    public const string Milestone         = "Milestone";
    public const string ProjectRisk       = "ProjectRisk";
    public const string KpiTarget         = "KpiTarget";
    public const string KpiActual         = "KpiActual";

    public static readonly IReadOnlyList<string> All = new[]
    {
        ActionItem, ActionItemSubItem, ActionItemUpdate, Project, Milestone, ProjectRisk, KpiTarget, KpiActual,
    };

    /// <summary>The canonical spelling of a supported type (case-insensitive), or null.</summary>
    public static string? Normalize(string? entityType) =>
        All.FirstOrDefault(t => string.Equals(t, entityType?.Trim(), StringComparison.OrdinalIgnoreCase));

    /// <summary>Types that need <c>ActionItems.View</c> to read and <c>ActionItems.Edit</c> to write.</summary>
    public static bool IsActionItemType(string entityType) =>
        entityType is ActionItem or ActionItemSubItem or ActionItemUpdate;

    public static bool IsKpiType(string entityType) => entityType is KpiTarget or KpiActual;
}
