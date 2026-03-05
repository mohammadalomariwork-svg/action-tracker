namespace ActionTracker.Application.Features.UserManagement.DTOs;

public class RegisterUserResponseDto
{
    public string       UserId    { get; set; } = string.Empty;
    public string       UserName  { get; set; } = string.Empty;
    public string       Email     { get; set; } = string.Empty;
    public string       FullName  { get; set; } = string.Empty;
    public List<string> Roles     { get; set; } = [];
    public bool         IsADUser  { get; set; }
    public DateTime     CreatedAt { get; set; }
}
