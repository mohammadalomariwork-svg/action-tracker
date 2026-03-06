using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ActionTracker.Application.Common.Interfaces;
using ActionTracker.Application.Features.Projects.DTOs;
using ActionTracker.Application.Features.Projects.Interfaces;
using ActionTracker.Application.Features.Projects.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ActionTracker.Application.Features.Projects.Services;

/// <summary>
/// Application service for managing workspace-scoped strategic objectives.
/// Persists to <c>WorkspaceStrategicObjectives</c> via <see cref="IAppDbContext"/>.
/// </summary>
public class StrategicObjectiveService : IStrategicObjectiveService
{
    private readonly IAppDbContext _db;
    private readonly ILogger<StrategicObjectiveService> _logger;

    /// <summary>Initialises the service with its required dependencies.</summary>
    public StrategicObjectiveService(IAppDbContext db, ILogger<StrategicObjectiveService> logger)
    {
        _db     = db;
        _logger = logger;
    }

    // ── Queries ───────────────────────────────────────────────────────────────

    /// <inheritdoc/>
    public async Task<IEnumerable<StrategicObjectiveDto>> GetAllAsync()
    {
        try
        {
            var entities = await _db.WorkspaceStrategicObjectives
                .Where(o => o.IsActive)
                .OrderByDescending(o => o.FiscalYear)
                .ThenBy(o => o.Title)
                .ToListAsync();

            return entities.Select(MapToDto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving all strategic objectives.");
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task<IEnumerable<StrategicObjectiveDto>> GetByOrganizationUnitAsync(string orgUnit)
    {
        try
        {
            var entities = await _db.WorkspaceStrategicObjectives
                .Where(o => o.IsActive && o.OrganizationUnit == orgUnit)
                .OrderByDescending(o => o.FiscalYear)
                .ThenBy(o => o.Title)
                .ToListAsync();

            return entities.Select(MapToDto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving strategic objectives for org unit '{OrgUnit}'.", orgUnit);
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task<StrategicObjectiveDto?> GetByIdAsync(int id)
    {
        try
        {
            var entity = await _db.WorkspaceStrategicObjectives
                .FirstOrDefaultAsync(o => o.Id == id);

            return entity is null ? null : MapToDto(entity);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving strategic objective {Id}.", id);
            throw;
        }
    }

    // ── Commands ──────────────────────────────────────────────────────────────

    /// <inheritdoc/>
    public async Task<StrategicObjectiveDto> CreateAsync(CreateStrategicObjectiveDto dto)
    {
        try
        {
            var entity = new StrategicObjective
            {
                Title            = dto.Title,
                Description      = dto.Description,
                OrganizationUnit = dto.OrganizationUnit,
                FiscalYear       = dto.FiscalYear,
                IsActive         = true,
                CreatedAt        = DateTime.UtcNow
            };

            _db.WorkspaceStrategicObjectives.Add(entity);
            await _db.SaveChangesAsync();

            _logger.LogInformation("Created strategic objective {Id} '{Title}'.", entity.Id, entity.Title);
            return MapToDto(entity);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating strategic objective '{Title}'.", dto.Title);
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task<StrategicObjectiveDto?> UpdateAsync(int id, UpdateStrategicObjectiveDto dto)
    {
        try
        {
            var entity = await _db.WorkspaceStrategicObjectives
                .FirstOrDefaultAsync(o => o.Id == id);

            if (entity is null)
                return null;

            if (dto.Title            is not null) entity.Title            = dto.Title;
            if (dto.Description      is not null) entity.Description      = dto.Description;
            if (dto.OrganizationUnit is not null) entity.OrganizationUnit = dto.OrganizationUnit;
            if (dto.FiscalYear       is not null) entity.FiscalYear       = dto.FiscalYear.Value;
            if (dto.IsActive         is not null) entity.IsActive         = dto.IsActive.Value;

            await _db.SaveChangesAsync();

            _logger.LogInformation("Updated strategic objective {Id}.", id);
            return MapToDto(entity);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating strategic objective {Id}.", id);
            throw;
        }
    }

    /// <inheritdoc/>
    /// <remarks>Performs a soft delete by setting <c>IsActive = false</c>.</remarks>
    public async Task<bool> DeleteAsync(int id)
    {
        try
        {
            var entity = await _db.WorkspaceStrategicObjectives
                .FirstOrDefaultAsync(o => o.Id == id);

            if (entity is null)
                return false;

            entity.IsActive = false;
            await _db.SaveChangesAsync();

            _logger.LogInformation("Soft-deleted strategic objective {Id}.", id);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting strategic objective {Id}.", id);
            throw;
        }
    }

    // ── Private mapping ───────────────────────────────────────────────────────

    /// <summary>Maps a <see cref="StrategicObjective"/> entity to its DTO.</summary>
    private static StrategicObjectiveDto MapToDto(StrategicObjective e) => new()
    {
        Id               = e.Id,
        Title            = e.Title,
        Description      = e.Description,
        OrganizationUnit = e.OrganizationUnit,
        FiscalYear       = e.FiscalYear,
        IsActive         = e.IsActive
    };
}
