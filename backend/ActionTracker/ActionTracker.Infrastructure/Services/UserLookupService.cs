using ActionTracker.Application.Common.Interfaces;
using ActionTracker.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace ActionTracker.Infrastructure.Services;

public class UserLookupService : IUserLookupService
{
    private readonly UserManager<ApplicationUser> _userManager;

    public UserLookupService(UserManager<ApplicationUser> userManager)
    {
        _userManager = userManager;
    }

    public async Task<string?> GetDisplayNameAsync(string? userId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(userId)) return null;
        var user = await _userManager.Users
            .Where(u => u.Id == userId)
            .Select(u => new { u.FirstName, u.LastName, u.DisplayName })
            .FirstOrDefaultAsync(ct);
        if (user is null) return null;
        var fullName = $"{user.FirstName} {user.LastName}".Trim();
        return !string.IsNullOrWhiteSpace(fullName) ? fullName : user.DisplayName;
    }

    public async Task<Dictionary<string, string>> GetDisplayNamesAsync(
        IEnumerable<string> userIds,
        CancellationToken   ct = default)
    {
        var ids = userIds.Where(id => !string.IsNullOrWhiteSpace(id)).Distinct().ToList();
        if (ids.Count == 0) return new Dictionary<string, string>();

        var users = await _userManager.Users
            .Where(u => ids.Contains(u.Id))
            .Select(u => new { u.Id, u.FirstName, u.LastName, u.DisplayName })
            .ToListAsync(ct);

        return users.ToDictionary(u => u.Id, u =>
        {
            var fullName = $"{u.FirstName} {u.LastName}".Trim();
            return !string.IsNullOrWhiteSpace(fullName) ? fullName : u.DisplayName ?? string.Empty;
        });
    }
}
