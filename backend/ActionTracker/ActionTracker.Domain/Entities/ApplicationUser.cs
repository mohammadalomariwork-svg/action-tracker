using Microsoft.AspNetCore.Identity;

namespace ActionTracker.Domain.Entities;

public class ApplicationUser : IdentityUser
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public string Department { get; set; } = string.Empty;

    /// <summary>
    /// Optional display name shown in the UI, separate from FirstName/LastName.
    /// Used when the user's preferred display name differs from their legal name.
    /// </summary>
    public string? DisplayName { get; set; }

    /// <summary>
    /// The Azure Entra ID Object ID (oid claim) for users who sign in via Azure AD.
    /// Null for locally-registered users.
    /// </summary>
    public string? AzureObjectId { get; set; }

    /// <summary>
    /// Identifies how the user authenticates. "Local" for username/password accounts,
    /// "AzureAD" for accounts federated through Microsoft Entra ID.
    /// </summary>
    public string LoginProvider { get; set; } = "Local";

    /// <summary>
    /// Indicates whether the user account is active. Inactive accounts cannot log in.
    /// Defaults to true on creation.
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// UTC timestamp of when the account was created.
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// The identifier (user ID or system name) of whoever created this account.
    /// Null when self-registered or seeded by the system.
    /// </summary>
    public string? CreatedBy { get; set; }

    public DateTime? LastLoginAt { get; set; }

    public string FullName => $"{FirstName} {LastName}";

    public ICollection<ActionItem> AssignedActions { get; set; } = new List<ActionItem>();
}
