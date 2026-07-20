using System.Security.Claims;
using ActionTracker.API.Models;
using ActionTracker.Application.Features.MailServerConfig;
using ActionTracker.Application.Features.MailServerConfig.DTOs;
using ActionTracker.Infrastructure.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ActionTracker.API.Controllers;

[ApiController]
[Route("api/mail-server-config")]
[Authorize(Policy = PermissionPolicies.MailServerConfigView)]
public class MailServerConfigController : ControllerBase
{
    private readonly IMailServerConfigService _service;
    private readonly ILogger<MailServerConfigController> _logger;

    public MailServerConfigController(
        IMailServerConfigService service,
        ILogger<MailServerConfigController> logger)
    {
        _service = service;
        _logger  = logger;
    }

    [HttpGet]
    public async Task<IActionResult> Get()
    {
        var result = await _service.GetAsync();
        return Ok(ApiResponse<MailServerConfigDto?>.Ok(result));
    }

    [HttpPut]
    [Authorize(Policy = PermissionPolicies.MailServerConfigEdit)]
    public async Task<IActionResult> Upsert([FromBody] UpdateMailServerConfigDto dto)
    {
        if (dto is null)
            return BadRequest(ApiResponse<string>.Fail("Request body is required."));

        if (string.IsNullOrWhiteSpace(dto.Host))
            return BadRequest(ApiResponse<string>.Fail("Host is required."));
        if (dto.Port <= 0 || dto.Port > 65535)
            return BadRequest(ApiResponse<string>.Fail("Port must be between 1 and 65535."));
        if (string.IsNullOrWhiteSpace(dto.FromEmail))
            return BadRequest(ApiResponse<string>.Fail("From email is required."));

        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var result = await _service.UpsertAsync(dto, userId);
        return Ok(ApiResponse<MailServerConfigDto>.Ok(result, "Mail server configuration saved."));
    }
}
