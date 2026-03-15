namespace ActionTracker.Application.Permissions.DTOs;

public class AreaActionMappingDto
{
    public Guid Id { get; set; }
    public Guid AreaId { get; set; }
    public string AreaName { get; set; } = string.Empty;
    public string AreaDisplayName { get; set; } = string.Empty;
    public Guid ActionId { get; set; }
    public string ActionName { get; set; } = string.Empty;
    public string ActionDisplayName { get; set; } = string.Empty;
}
