using System.ComponentModel.DataAnnotations;

namespace ActionTracker.Application.Permissions.DTOs;

public class CreateAreaActionMappingDto
{
    [Required]
    public Guid AreaId { get; set; }

    [Required]
    public Guid ActionId { get; set; }
}
