using System.Security.Claims;
using ActionTracker.Application.Common.Interfaces;
using Microsoft.AspNetCore.Http;

namespace ActionTracker.Infrastructure.Services;

/// <inheritdoc />
public class CurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUserService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public string? UserId
    {
        get
        {
            var user = _httpContextAccessor.HttpContext?.User;
            if (user?.Identity?.IsAuthenticated != true) return null;
            var id = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return string.IsNullOrEmpty(id) ? null : id;
        }
    }
}
