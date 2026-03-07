namespace ActionTracker.Application.Features.Workspaces.DTOs;

/// <summary>
/// Lightweight org unit item used to populate the Organisation Unit dropdown
/// on the workspace form.
/// </summary>
public class OrgUnitDropdownItemDto
{
    /// <summary>Org unit ID (Guid as string).</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>Display name of the org unit.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Auto-generated short code (e.g. "OC-1").</summary>
    public string? Code { get; set; }

    /// <summary>Hierarchy depth — 1 for root, 2 for its children, etc.</summary>
    public int Level { get; set; } = 1;
}
