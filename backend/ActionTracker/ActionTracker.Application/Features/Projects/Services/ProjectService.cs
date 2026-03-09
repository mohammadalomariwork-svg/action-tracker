using ActionTracker.Application.Common.Interfaces;
using ActionTracker.Application.Features.Projects.DTOs;
using ActionTracker.Application.Features.Projects.Interfaces;
using ActionTracker.Application.Helpers;
using ActionTracker.Domain.Entities;
using ActionTracker.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ActionTracker.Application.Features.Projects.Services;

public class ProjectService : IProjectService
{
    private readonly IAppDbContext _db;
    private readonly ILogger<ProjectService> _logger;

    public ProjectService(IAppDbContext db, ILogger<ProjectService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<PagedResult<ProjectResponseDto>> GetAllAsync(ProjectFilterDto filter, CancellationToken ct)
    {
        var query = _db.Projects
            .Include(p => p.Workspace)
            .Include(p => p.ProjectManager)
            .Include(p => p.StrategicObjective)
            .Include(p => p.OwnerOrgUnit)
            .Include(p => p.Sponsors).ThenInclude(s => s.User)
            .AsQueryable();

        if (filter.WorkspaceId.HasValue)
            query = query.Where(p => p.WorkspaceId == filter.WorkspaceId.Value);

        if (filter.Status.HasValue)
            query = query.Where(p => p.Status == filter.Status.Value);

        if (filter.ProjectType.HasValue)
            query = query.Where(p => p.ProjectType == filter.ProjectType.Value);

        if (filter.Priority.HasValue)
            query = query.Where(p => p.Priority == filter.Priority.Value);

        if (!string.IsNullOrWhiteSpace(filter.SearchTerm))
        {
            var term = filter.SearchTerm.Trim().ToLower();
            query = query.Where(p =>
                p.Name.ToLower().Contains(term) ||
                p.ProjectCode.ToLower().Contains(term) ||
                (p.Description != null && p.Description.ToLower().Contains(term)));
        }

        // Sorting
        query = filter.SortBy?.ToLower() switch
        {
            "name"             => filter.SortDescending ? query.OrderByDescending(p => p.Name) : query.OrderBy(p => p.Name),
            "projectcode"      => filter.SortDescending ? query.OrderByDescending(p => p.ProjectCode) : query.OrderBy(p => p.ProjectCode),
            "status"           => filter.SortDescending ? query.OrderByDescending(p => p.Status) : query.OrderBy(p => p.Status),
            "priority"         => filter.SortDescending ? query.OrderByDescending(p => p.Priority) : query.OrderBy(p => p.Priority),
            "plannedstartdate" => filter.SortDescending ? query.OrderByDescending(p => p.PlannedStartDate) : query.OrderBy(p => p.PlannedStartDate),
            "plannedenddate"   => filter.SortDescending ? query.OrderByDescending(p => p.PlannedEndDate) : query.OrderBy(p => p.PlannedEndDate),
            _                  => filter.SortDescending ? query.OrderByDescending(p => p.CreatedAt) : query.OrderBy(p => p.CreatedAt),
        };

        var projected = query.Select(p => ToDto(p));

        return await PagedResult<ProjectResponseDto>.CreateAsync(projected, filter.PageNumber, filter.PageSize, ct);
    }

    public async Task<ProjectResponseDto?> GetByIdAsync(Guid id, CancellationToken ct)
    {
        var project = await _db.Projects
            .Include(p => p.Workspace)
            .Include(p => p.ProjectManager)
            .Include(p => p.StrategicObjective)
            .Include(p => p.OwnerOrgUnit)
            .Include(p => p.Sponsors).ThenInclude(s => s.User)
            .FirstOrDefaultAsync(p => p.Id == id, ct);

        return project is null ? null : MapToDto(project);
    }

    public async Task<ProjectResponseDto> CreateAsync(ProjectCreateDto dto, string userId, CancellationToken ct)
    {
        // Validate strategic objective requirement
        if (dto.ProjectType == ProjectType.Strategic && !dto.StrategicObjectiveId.HasValue)
            throw new ArgumentException("Strategic objective is required for strategic projects.");

        if (dto.SponsorUserIds.Count == 0)
            throw new ArgumentException("At least one sponsor is required.");

        // Generate project code: PRJ-{year}-{sequence}
        var year = DateTime.UtcNow.Year;
        var count = await _db.Projects
            .IgnoreQueryFilters()
            .CountAsync(p => p.ProjectCode.StartsWith($"PRJ-{year}-"), ct);
        var projectCode = $"PRJ-{year}-{(count + 1):D3}";

        var project = new Project
        {
            Id                    = Guid.NewGuid(),
            ProjectCode           = projectCode,
            Name                  = dto.Name.Trim(),
            Description           = dto.Description?.Trim(),
            WorkspaceId           = dto.WorkspaceId,
            ProjectType           = dto.ProjectType,
            Status                = ProjectStatus.Draft,
            StrategicObjectiveId  = dto.ProjectType == ProjectType.Strategic ? dto.StrategicObjectiveId : null,
            Priority              = dto.Priority,
            ProjectManagerUserId  = dto.ProjectManagerUserId,
            OwnerOrgUnitId        = dto.OwnerOrgUnitId,
            PlannedStartDate      = dto.PlannedStartDate,
            PlannedEndDate        = dto.PlannedEndDate,
            ApprovedBudget        = dto.ApprovedBudget,
            Currency              = "AED",
            IsBaselined           = false,
            CreatedBy             = userId,
            CreatedAt             = DateTime.UtcNow,
        };

        _db.Projects.Add(project);

        // Add sponsors
        foreach (var sponsorId in dto.SponsorUserIds.Distinct())
        {
            _db.ProjectSponsors.Add(new ProjectSponsor
            {
                ProjectId = project.Id,
                UserId    = sponsorId,
            });
        }

        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Project {ProjectCode} created by user {UserId}", projectCode, userId);

        return (await GetByIdAsync(project.Id, ct))!;
    }

    public async Task<ProjectResponseDto> UpdateAsync(Guid id, ProjectUpdateDto dto, CancellationToken ct)
    {
        var project = await _db.Projects
            .Include(p => p.Sponsors)
            .FirstOrDefaultAsync(p => p.Id == id, ct)
            ?? throw new KeyNotFoundException($"Project {id} not found.");

        if (dto.ProjectType == ProjectType.Strategic && !dto.StrategicObjectiveId.HasValue)
            throw new ArgumentException("Strategic objective is required for strategic projects.");

        if (dto.SponsorUserIds.Count == 0)
            throw new ArgumentException("At least one sponsor is required.");

        // Set actual start date when transitioning to Active
        if (dto.Status == ProjectStatus.Active && project.Status == ProjectStatus.Draft && !dto.ActualStartDate.HasValue)
            dto.ActualStartDate = DateTime.UtcNow;

        project.Name                  = dto.Name.Trim();
        project.Description           = dto.Description?.Trim();
        project.ProjectType           = dto.ProjectType;
        project.Status                = dto.Status;
        project.StrategicObjectiveId  = dto.ProjectType == ProjectType.Strategic ? dto.StrategicObjectiveId : null;
        project.Priority              = dto.Priority;
        project.ProjectManagerUserId  = dto.ProjectManagerUserId;
        project.OwnerOrgUnitId        = dto.OwnerOrgUnitId;
        project.PlannedStartDate      = dto.PlannedStartDate;
        project.PlannedEndDate        = dto.PlannedEndDate;
        project.ActualStartDate       = dto.ActualStartDate;
        project.ApprovedBudget        = dto.ApprovedBudget;

        // Sync sponsors
        var existingSponsorIds = project.Sponsors.Select(s => s.UserId).ToHashSet();
        var newSponsorIds = dto.SponsorUserIds.Distinct().ToHashSet();

        // Remove sponsors no longer in list
        foreach (var removed in project.Sponsors.Where(s => !newSponsorIds.Contains(s.UserId)).ToList())
            _db.ProjectSponsors.Remove(removed);

        // Add new sponsors
        foreach (var addId in newSponsorIds.Where(id2 => !existingSponsorIds.Contains(id2)))
        {
            _db.ProjectSponsors.Add(new ProjectSponsor
            {
                ProjectId = project.Id,
                UserId    = addId,
            });
        }

        await _db.SaveChangesAsync(ct);

        return (await GetByIdAsync(project.Id, ct))!;
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct)
    {
        var project = await _db.Projects.FirstOrDefaultAsync(p => p.Id == id, ct)
            ?? throw new KeyNotFoundException($"Project {id} not found.");

        project.IsDeleted = true;
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Project {ProjectCode} soft-deleted", project.ProjectCode);
    }

    public async Task<List<StrategicObjectiveOptionDto>> GetStrategicObjectivesForWorkspaceAsync(
        Guid workspaceId, CancellationToken ct)
    {
        var workspace = await _db.Workspaces.FirstOrDefaultAsync(w => w.Id == workspaceId, ct)
            ?? throw new KeyNotFoundException($"Workspace {workspaceId} not found.");

        // Find the OrgUnit matching the workspace's OrganizationUnit name
        var orgUnit = await _db.OrgUnits
            .FirstOrDefaultAsync(o => o.Name == workspace.OrganizationUnit, ct);

        if (orgUnit is null)
            return new List<StrategicObjectiveOptionDto>();

        // Walk up the org unit hierarchy until we find strategic objectives
        var currentOrgUnitId = orgUnit.Id;
        Guid? currentParentId = orgUnit.ParentId;

        while (true)
        {
            var objectives = await _db.StrategicObjectives
                .Where(so => so.OrgUnitId == currentOrgUnitId)
                .OrderBy(so => so.ObjectiveCode)
                .Select(so => new StrategicObjectiveOptionDto
                {
                    Id            = so.Id,
                    ObjectiveCode = so.ObjectiveCode,
                    Statement     = so.Statement,
                })
                .ToListAsync(ct);

            if (objectives.Count > 0)
                return objectives;

            // No objectives found — try parent
            if (!currentParentId.HasValue)
                return new List<StrategicObjectiveOptionDto>();

            var parent = await _db.OrgUnits
                .FirstOrDefaultAsync(o => o.Id == currentParentId.Value, ct);

            if (parent is null)
                return new List<StrategicObjectiveOptionDto>();

            currentOrgUnitId = parent.Id;
            currentParentId = parent.ParentId;
        }
    }

    // ── Mapping helpers ─────────────────────────────────────
    private static ProjectResponseDto MapToDto(Project p)
    {
        return new ProjectResponseDto
        {
            Id                          = p.Id,
            ProjectCode                 = p.ProjectCode,
            Name                        = p.Name,
            Description                 = p.Description,
            WorkspaceId                 = p.WorkspaceId,
            WorkspaceTitle              = p.Workspace?.Title ?? string.Empty,
            ProjectType                 = p.ProjectType,
            Status                      = p.Status,
            Priority                    = p.Priority,
            StrategicObjectiveId        = p.StrategicObjectiveId,
            StrategicObjectiveStatement = p.StrategicObjective?.Statement,
            ProjectManagerUserId        = p.ProjectManagerUserId,
            ProjectManagerName          = p.ProjectManager?.FullName ?? string.Empty,
            Sponsors                    = p.Sponsors.Select(s => new SponsorDto
            {
                UserId   = s.UserId,
                FullName = s.User?.FullName ?? string.Empty,
                Email    = s.User?.Email ?? string.Empty,
            }).ToList(),
            OwnerOrgUnitId   = p.OwnerOrgUnitId,
            OwnerOrgUnitName = p.OwnerOrgUnit?.Name,
            PlannedStartDate = p.PlannedStartDate,
            PlannedEndDate   = p.PlannedEndDate,
            ActualStartDate  = p.ActualStartDate,
            ApprovedBudget   = p.ApprovedBudget,
            Currency         = p.Currency,
            IsBaselined      = p.IsBaselined,
            IsDeleted        = p.IsDeleted,
            CreatedAt        = p.CreatedAt,
            UpdatedAt        = p.UpdatedAt,
        };
    }

    // Expression-based projection for IQueryable (used in GetAllAsync)
    private static ProjectResponseDto ToDto(Project p) => new()
    {
        Id                          = p.Id,
        ProjectCode                 = p.ProjectCode,
        Name                        = p.Name,
        Description                 = p.Description,
        WorkspaceId                 = p.WorkspaceId,
        WorkspaceTitle              = p.Workspace != null ? p.Workspace.Title : string.Empty,
        ProjectType                 = p.ProjectType,
        Status                      = p.Status,
        Priority                    = p.Priority,
        StrategicObjectiveId        = p.StrategicObjectiveId,
        StrategicObjectiveStatement = p.StrategicObjective != null ? p.StrategicObjective.Statement : null,
        ProjectManagerUserId        = p.ProjectManagerUserId,
        ProjectManagerName          = p.ProjectManager != null ? p.ProjectManager.FirstName + " " + p.ProjectManager.LastName : string.Empty,
        Sponsors                    = p.Sponsors.Select(s => new SponsorDto
        {
            UserId   = s.UserId,
            FullName = s.User != null ? s.User.FirstName + " " + s.User.LastName : string.Empty,
            Email    = s.User != null ? s.User.Email! : string.Empty,
        }).ToList(),
        OwnerOrgUnitId   = p.OwnerOrgUnitId,
        OwnerOrgUnitName = p.OwnerOrgUnit != null ? p.OwnerOrgUnit.Name : null,
        PlannedStartDate = p.PlannedStartDate,
        PlannedEndDate   = p.PlannedEndDate,
        ActualStartDate  = p.ActualStartDate,
        ApprovedBudget   = p.ApprovedBudget,
        Currency         = p.Currency,
        IsBaselined      = p.IsBaselined,
        IsDeleted        = p.IsDeleted,
        CreatedAt        = p.CreatedAt,
        UpdatedAt        = p.UpdatedAt,
    };
}
