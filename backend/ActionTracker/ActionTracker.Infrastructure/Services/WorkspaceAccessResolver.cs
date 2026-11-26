using ActionTracker.Application.Helpers;
using ActionTracker.Domain.Constants;
using ActionTracker.Domain.Entities;
using ActionTracker.Infrastructure.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace ActionTracker.Infrastructure.Services;

/// <inheritdoc />
public class WorkspaceAccessResolver : IWorkspaceAccessResolver
{
    // Roles that retain full cross-workspace visibility regardless of membership.
    private static readonly string[] PrivilegedRoles =
    {
        AppRoles.Admin,
        AppRoles.PmoHead,
        AppRoles.PmoAnalyst,
    };

    private readonly AppDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;

    public WorkspaceAccessResolver(AppDbContext db, UserManager<ApplicationUser> userManager)
    {
        _db          = db;
        _userManager = userManager;
    }

    public async Task<List<Guid>?> GetVisibleWorkspaceIdsAsync(string userId)
    {
        if (string.IsNullOrEmpty(userId))
            return new List<Guid>();

        var user = await _userManager.FindByIdAsync(userId);
        if (user is null)
            return new List<Guid>();

        var roles = await _userManager.GetRolesAsync(user);
        if (roles.Any(r => PrivilegedRoles.Contains(r)))
            return null; // unrestricted

        var adminIds  = _db.WorkspaceAdmins.Where(a => a.AdminUserId == userId).Select(a => a.WorkspaceId);
        var memberIds = _db.WorkspaceMembers.Where(m => m.UserId == userId).Select(m => m.WorkspaceId);

        return await adminIds.Concat(memberIds).Distinct().ToListAsync();
    }

    /// <inheritdoc />
    public async Task<List<Guid>> GetProjectRoleProjectIdsAsync(string userId)
    {
        if (string.IsNullOrEmpty(userId))
            return new List<Guid>();

        // Same rule as AppDbContext.ProjectRoleProjectIds, for any user (not only
        // the request's). Read fresh on every call: removing a person takes effect
        // on the next request.
        return await _db.Projects.IgnoreQueryFilters()
            .Where(p => !p.IsDeleted
                && (p.ProjectManagerUserId == userId
                    || p.Sponsors.Any(s => s.UserId == userId)
                    || p.People.Any(m => m.UserId == userId)))
            .Select(p => p.Id)
            .ToListAsync();
    }

    /// <inheritdoc />
    public Task<bool> IsWorkspaceLevelVisibleAsync(string? userId, Guid workspaceId)
        => _db.Workspaces.IgnoreQueryFilters()
            .Where(w => w.Id == workspaceId)
            .WhereWorkspaceLevelVisibleTo(userId)
            .AnyAsync();
}
