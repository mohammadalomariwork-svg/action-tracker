using ActionTracker.API.Models;
using ActionTracker.Application.Features.Auth.DTOs;
using ActionTracker.Application.Features.Auth.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ActionTracker.API.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly ILogger<AuthController> _logger;

    public AuthController(IAuthService authService, ILogger<AuthController> logger)
    {
        _authService = authService;
        _logger      = logger;
    }

    /// <summary>Authenticates a user and returns JWT access and refresh tokens.</summary>
    [HttpPost("login")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<LoginResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Login([FromBody] LoginRequestDto dto, CancellationToken ct)
    {
        var ip     = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var result = await _authService.LoginAsync(dto, ip, ct);
        return Ok(ApiResponse<LoginResponseDto>.Ok(result));
    }

    /// <summary>Returns a new access/refresh token pair given a valid refresh token.</summary>
    [HttpPost("refresh")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<LoginResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Refresh([FromBody] RefreshTokenRequestDto dto, CancellationToken ct)
    {
        var ip     = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var result = await _authService.RefreshTokenAsync(dto.RefreshToken, ip, ct);
        return Ok(ApiResponse<LoginResponseDto>.Ok(result));
    }

    /// <summary>Registers a new user account.</summary>
    [HttpPost("register")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<string>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Register([FromBody] RegisterRequestDto dto, CancellationToken ct)
    {
        await _authService.RegisterAsync(dto, ct);
        _logger.LogInformation("New user registered: {Email}", dto.Email);
        return Ok(ApiResponse<string>.Ok("User registered successfully."));
    }

    /// <summary>Revokes an active refresh token.</summary>
    [HttpPost("revoke")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<string>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Revoke([FromBody] RefreshTokenRequestDto dto, CancellationToken ct)
    {
        await _authService.RevokeTokenAsync(dto.RefreshToken, ct);
        return Ok(ApiResponse<string>.Ok("Token revoked."));
    }
}
