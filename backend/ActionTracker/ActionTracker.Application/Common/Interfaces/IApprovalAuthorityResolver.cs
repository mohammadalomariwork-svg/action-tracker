namespace ActionTracker.Application.Common.Interfaces;

/// <summary>
/// Resolves the user who must approve strategic-plan submissions or KPI
/// actuals authored within a given org unit (CR-001-H).
///
/// Walks up the org-unit chain to the closest level-3 ancestor and returns
/// that node's <c>HeadUser</c>. Returns null when no level-3 ancestor has
/// a head configured. Callers must surface a clear error in that case
/// rather than silently routing elsewhere.
/// </summary>
public interface IApprovalAuthorityResolver
{
    Task<ApprovalAuthorityDto?> ResolvePlanApproverAsync(
        Guid orgUnitId,
        CancellationToken ct = default);
}

public record ApprovalAuthorityDto(
    string UserId,
    string DisplayName,
    string Email,
    Guid SourceOrgUnitId,
    int SourceOrgUnitLevel,
    string SourceOrgUnitName);
