namespace ActionTracker.Domain.Entities;

public class OrgUnit
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Code { get; set; }
    public int Level { get; set; }
    public Guid? ParentId { get; set; }
    public bool IsDeleted { get; set; } = false;
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public DateTime? DeletedAt { get; set; }

    // Navigation
    public OrgUnit? Parent { get; set; }
    public ICollection<OrgUnit> Children { get; set; } = new List<OrgUnit>();
    public ICollection<StrategicObjective> StrategicObjectives { get; set; } = new List<StrategicObjective>();
}
