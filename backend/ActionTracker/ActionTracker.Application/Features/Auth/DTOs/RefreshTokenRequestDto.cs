using System.ComponentModel.DataAnnotations;

namespace ActionTracker.Application.Features.Auth.DTOs;

public class RefreshTokenRequestDto
{
    [Required]
    public string RefreshToken { get; set; } = string.Empty;
}
