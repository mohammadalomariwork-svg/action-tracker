using ActionTracker.Application.Common.Interfaces;
using ActionTracker.Infrastructure.Data;
using Microsoft.AspNetCore.Identity;
using ActionTracker.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ActionTracker.Infrastructure.Services;

/// <summary>
/// Walks up the org-unit chain to the closest level-3 ancestor and returns
/// the head of that node as the configured plan approver. Per CR-001-H,
/// when no level-3 ancestor has a head configured the resolver returns
/// null — callers must surface a clear error rather than fall back.
/// </summary>
public class ApprovalAuthorityResolver : IApprovalAuthorityResolver
{
    private readonly AppDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<ApprovalAuthorityResolver> _logger;

    // Per-request cache to avoid repeated walks for the same org unit.
    private readonly Dictionary<Guid, ApprovalAuthorityDto?> _cache = new();

    public ApprovalAuthorityResolver(
        AppDbContext db,
        UserManager<ApplicationUser> userManager,
        ILogger<ApprovalAuthorityResolver> logger)
    {
        _db = db;
        _userManager = userManager;
        _logger = logger;
    }

    public async Task<ApprovalAuthorityDto?> ResolvePlanApproverAsync(
        Guid orgUnitId, CancellationToken ct = default)
    {
        if (_cache.TryGetValue(orgUnitId, out var cached))
            return cached;

        var target = await _db.OrgUnits
            .AsNoTracking()
            .FirstOrDefaultAsync(o => o.Id == orgUnitId, ct);

        if (target is null)
        {
            _cache[orgUnitId] = null;
            return null;
        }

        // Load all (rare set; org chart is small) to walk parents in memory.
        var all = await _db.OrgUnits
            .AsNoTracking()
            .Select(o => new { o.Id, o.Name, o.ParentId, o.Level, o.HeadUserId })
            .ToListAsync(ct);

        var lookup = all.ToDictionary(o => o.Id);

        // Walk up until we find Level == 3. Root = level 1.
        var current = lookup.TryGetValue(target.Id, out var n) ? n : null;
        while (current is not null && current.Level > 3)
        {
            current = current.ParentId is { } pid && lookup.TryGetValue(pid, out var p) ? p : null;
        }

        if (current is null || current.Level != 3 || string.IsNullOrEmpty(current.HeadUserId))
        {
            _cache[orgUnitId] = null;
            return null;
        }

        var head = await _userManager.FindByIdAsync(current.HeadUserId);
        if (head is null || !head.IsActive || string.IsNullOrEmpty(head.Email))
        {
            _logger.LogWarning(
                "OrgUnit {OrgUnitId} has HeadUserId {HeadUserId} but the user is missing/inactive — resolving to null.",
                current.Id, current.HeadUserId);
            _cache[orgUnitId] = null;
            return null;
        }

        var dto = new ApprovalAuthorityDto(
            head.Id,
            head.FullName,
            head.Email!,
            current.Id,
            current.Level,
            current.Name);

        _cache[orgUnitId] = dto;
        return dto;
    }
}
