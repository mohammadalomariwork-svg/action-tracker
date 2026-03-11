using ActionTracker.API.Models;
using ActionTracker.Application.Features.UserManagement.DTOs;
using ActionTracker.Application.Features.UserManagement.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace ActionTracker.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ProfileController : ControllerBase
{
    private readonly IUserManagementService   _userManagement;
    private readonly ILogger<ProfileController> _logger;

    public ProfileController(
        IUserManagementService     userManagement,
        ILogger<ProfileController> logger)
    {
        _userManagement = userManagement;
        _logger         = logger;
    }

    /// <summary>
    /// Returns the current user's employee profile from KU employee directory.
    /// </summary>
    [HttpGet("me")]
    [ProducesResponseType(typeof(ApiResponse<EmployeeProfileDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<string>),             StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetMyProfile(CancellationToken ct = default)
    {
        var email = User.FindFirstValue(ClaimTypes.Email)
                 ?? User.FindFirstValue("email")
                 ?? User.FindFirstValue(ClaimTypes.Name);

        if (string.IsNullOrWhiteSpace(email))
            return NotFound(ApiResponse<string>.Fail("Unable to determine user email from token."));

        _logger.LogInformation("GET /api/profile/me email={Email}", email);

        var profile = await _userManagement.GetEmployeeProfileByEmailAsync(email, ct);

        if (profile is null)
            return NotFound(ApiResponse<string>.Fail("Employee profile not found."));

        return Ok(ApiResponse<EmployeeProfileDto>.Ok(profile));
    }
}
