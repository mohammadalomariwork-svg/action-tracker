namespace ActionTracker.Application.Features.OrgChart.DTOs;

public class OrgUnitTreeDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Code { get; set; }
    public string? Description { get; set; }
    public int Level { get; set; }
    public Guid? ParentId { get; set; }
    public bool IsDeleted { get; set; }
    public string? CreatedBy { get; set; }
    public string? UpdatedBy { get; set; }
    public string? DeletedBy { get; set; }
    public List<OrgUnitTreeDto> Children { get; set; } = new();
}
