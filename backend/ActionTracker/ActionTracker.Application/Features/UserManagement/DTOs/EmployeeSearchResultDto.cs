namespace ActionTracker.Application.Features.UserManagement.DTOs;

public class EmployeeSearchResultDto
{
    public string  EmployeeId        { get; set; } = string.Empty;
    public string  FullName          { get; set; } = string.Empty;
    public string  Email             { get; set; } = string.Empty;
    public string? Department        { get; set; }
    public string? JobTitle          { get; set; }
    public string? PhoneNumber       { get; set; }
    public bool    AlreadyRegistered { get; set; }
}
