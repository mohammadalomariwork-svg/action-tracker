using ActionTracker.Application.Common.Interfaces;
using ActionTracker.Domain.Entities;
using Microsoft.AspNetCore.Identity;

namespace ActionTracker.Infrastructure.Services;

/// <inheritdoc />
public class UserRoleService : IUserRoleService
{
    private readonly UserManager<ApplicationUser> _userManager;

    public UserRoleService(UserManager<ApplicationUser> userManager) => _userManager = userManager;

    public async Task<bool> IsInRoleAsync(string userId, string role, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(userId)) return false;
        var user = await _userManager.FindByIdAsync(userId);
        return user is not null && await _userManager.IsInRoleAsync(user, role);
    }
}
