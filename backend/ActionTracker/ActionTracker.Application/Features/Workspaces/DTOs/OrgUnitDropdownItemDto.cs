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
}
