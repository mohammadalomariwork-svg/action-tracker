namespace ActionTracker.Application.Features.Workspaces.DTOs;

/// <summary>
/// Lightweight user item used to populate the Workspace Admin dropdown
/// on the workspace form. Only active users are included.
/// </summary>
public class UserDropdownItemDto
{
    /// <summary>AspNetUsers.Id of the user.</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>Display name shown in the dropdown (DisplayName ?? FirstName + LastName).</summary>
    public string DisplayName { get; set; } = string.Empty;
}
