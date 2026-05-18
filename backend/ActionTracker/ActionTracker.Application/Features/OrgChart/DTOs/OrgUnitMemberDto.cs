namespace ActionTracker.Application.Features.OrgChart.DTOs;

public class OrgUnitMemberDto
{
    public string UserId      { get; set; } = string.Empty;
    public string FullName    { get; set; } = string.Empty;
    public string Email       { get; set; } = string.Empty;
    public string[] Roles     { get; set; } = System.Array.Empty<string>();
    public bool IsActive      { get; set; }
}
