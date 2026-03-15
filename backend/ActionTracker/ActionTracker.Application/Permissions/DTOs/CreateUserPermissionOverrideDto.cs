using System.ComponentModel.DataAnnotations;

namespace ActionTracker.Application.Permissions.DTOs;

public class CreateUserPermissionOverrideDto
{
    [Required]
    [MaxLength(450)]
    public string UserId { get; set; } = string.Empty;

    [Required]
    [MaxLength(256)]
    public string UserDisplayName { get; set; } = string.Empty;

    [Required]
    public Guid AreaId { get; set; }

    [Required]
    public Guid ActionId { get; set; }

    [Required]
    public int OrgUnitScope { get; set; }

    public Guid? OrgUnitId { get; set; }

    [MaxLength(256)]
    public string? OrgUnitName { get; set; }

    [Required]
    public bool IsGranted { get; set; }

    [MaxLength(1000)]
    public string? Reason { get; set; }

    public DateTime? ExpiresAt { get; set; }
}
