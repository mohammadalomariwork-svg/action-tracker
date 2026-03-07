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
/// Application service for managing a project's budget record and its
/// associated contracts.
/// Budget upsert uses the <c>ProjectId</c> as the natural key; contracts
/// support full CRUD with a soft-delete pattern.
/// </summary>
public class BudgetService : IBudgetService
{
    private readonly IAppDbContext _db;
    private readonly ILogger<BudgetService> _logger;

    /// <summary>Initialises the service with its required dependencies.</summary>
    public BudgetService(IAppDbContext db, ILogger<BudgetService> logger)
    {
        _db     = db;
        _logger = logger;
    }

    // ── Budget ────────────────────────────────────────────────────────────────

    /// <inheritdoc/>
    public async Task<ProjectBudgetDto?> GetByProjectAsync(Guid projectId)
    {
        try
        {
            var budget = await _db.ProjectBudgets
                .FirstOrDefaultAsync(b => b.ProjectId == projectId);

            return budget is null ? null : MapBudgetToDto(budget);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error retrieving budget for project {ProjectId}.", projectId);
            throw;
        }
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Upserts the budget record keyed by <see cref="CreateUpdateBudgetDto.ProjectId"/>.
    /// When an existing record is found its monetary and note fields are
    /// overwritten and <c>UpdatedAt</c> is stamped; otherwise a new record is
    /// created with <c>CreatedAt = UtcNow</c>.
    /// <see cref="ProjectBudgetDto.RemainingBudget"/> is calculated as
    /// <c>TotalBudget − (SpentAmount ?? 0)</c>.
    /// </remarks>
    public async Task<ProjectBudgetDto> CreateOrUpdateAsync(CreateUpdateBudgetDto dto)
    {
        try
        {
            var existing = await _db.ProjectBudgets
                .FirstOrDefaultAsync(b => b.ProjectId == dto.ProjectId);

            if (existing is not null)
            {
                existing.TotalBudget  = dto.TotalBudget;
                existing.SpentAmount  = dto.SpentAmount;
                existing.Currency     = dto.Currency;
                existing.BudgetNotes  = dto.BudgetNotes;
                existing.UpdatedAt    = DateTime.UtcNow;

                await _db.SaveChangesAsync();

                _logger.LogInformation(
                    "Updated budget record {Id} for project {ProjectId}.",
                    existing.Id, dto.ProjectId);

                return MapBudgetToDto(existing);
            }

            var budget = new ProjectBudget
            {
                ProjectId   = dto.ProjectId,
                TotalBudget = dto.TotalBudget,
                SpentAmount = dto.SpentAmount,
                Currency    = dto.Currency,
                BudgetNotes = dto.BudgetNotes,
                CreatedAt   = DateTime.UtcNow
            };

            _db.ProjectBudgets.Add(budget);
            await _db.SaveChangesAsync();

            _logger.LogInformation(
                "Created budget record {Id} for project {ProjectId}.",
                budget.Id, dto.ProjectId);

            return MapBudgetToDto(budget);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error upserting budget for project {ProjectId}.", dto.ProjectId);
            throw;
        }
    }

    // ── Contracts ─────────────────────────────────────────────────────────────

    /// <inheritdoc/>
    public async Task<IEnumerable<ContractDto>> GetContractsByProjectAsync(Guid projectId)
    {
        try
        {
            var contracts = await _db.Contracts
                .Where(c => c.ProjectId == projectId && c.IsActive)
                .OrderBy(c => c.StartDate)
                .ToListAsync();

            return contracts.Select(MapContractToDto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error retrieving contracts for project {ProjectId}.", projectId);
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task<ContractDto> CreateContractAsync(CreateContractDto dto)
    {
        try
        {
            var contract = new Contract
            {
                ProjectId         = dto.ProjectId,
                ContractNumber    = dto.ContractNumber,
                ContractorName    = dto.ContractorName,
                ContractorContact = dto.ContractorContact,
                ContractValue     = dto.ContractValue,
                Currency          = dto.Currency,
                StartDate         = dto.StartDate,
                EndDate           = dto.EndDate,
                Description       = dto.Description,
                IsActive          = true,
                CreatedAt         = DateTime.UtcNow
            };

            _db.Contracts.Add(contract);
            await _db.SaveChangesAsync();

            _logger.LogInformation(
                "Created contract {Id} '{ContractNumber}' for project {ProjectId}.",
                contract.Id, contract.ContractNumber, contract.ProjectId);

            return MapContractToDto(contract);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error creating contract '{ContractNumber}' for project {ProjectId}.",
                dto.ContractNumber, dto.ProjectId);
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task<ContractDto?> UpdateContractAsync(Guid id, UpdateContractDto dto)
    {
        try
        {
            var contract = await _db.Contracts
                .FirstOrDefaultAsync(c => c.Id == id && c.IsActive);

            if (contract is null) return null;

            if (dto.ContractNumber    is not null) contract.ContractNumber    = dto.ContractNumber;
            if (dto.ContractorName    is not null) contract.ContractorName    = dto.ContractorName;
            if (dto.ContractorContact is not null) contract.ContractorContact = dto.ContractorContact;
            if (dto.ContractValue     is not null) contract.ContractValue     = dto.ContractValue.Value;
            if (dto.Currency          is not null) contract.Currency          = dto.Currency;
            if (dto.StartDate         is not null) contract.StartDate         = dto.StartDate.Value;
            if (dto.EndDate           is not null) contract.EndDate           = dto.EndDate;
            if (dto.Description       is not null) contract.Description       = dto.Description;
            if (dto.IsActive          is not null) contract.IsActive          = dto.IsActive.Value;

            await _db.SaveChangesAsync();

            _logger.LogInformation("Updated contract {Id}.", id);
            return MapContractToDto(contract);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating contract {Id}.", id);
            throw;
        }
    }

    /// <inheritdoc/>
    /// <remarks>Performs a soft delete by setting <c>IsActive = false</c>.</remarks>
    public async Task<bool> DeleteContractAsync(Guid id)
    {
        try
        {
            var contract = await _db.Contracts
                .FirstOrDefaultAsync(c => c.Id == id && c.IsActive);

            if (contract is null) return false;

            contract.IsActive = false;
            await _db.SaveChangesAsync();

            _logger.LogInformation("Soft-deleted contract {Id}.", id);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting contract {Id}.", id);
            throw;
        }
    }

    // ── Private mapping ───────────────────────────────────────────────────────

    /// <summary>
    /// Maps a <see cref="ProjectBudget"/> entity to its DTO.
    /// <see cref="ProjectBudgetDto.RemainingBudget"/> is the calculated
    /// expression; it is evaluated here so the DTO stays a plain data object.
    /// </summary>
    private static ProjectBudgetDto MapBudgetToDto(ProjectBudget b)
    {
        decimal spent = b.SpentAmount ?? 0m;
        return new ProjectBudgetDto
        {
            Id          = b.Id,
            ProjectId   = b.ProjectId,
            TotalBudget = b.TotalBudget,
            SpentAmount = spent,
            Currency    = b.Currency,
            BudgetNotes = b.BudgetNotes,
            // RemainingBudget is a calculated property on the DTO (TotalBudget - SpentAmount),
            // but we set SpentAmount here so the property derives correctly.
            CreatedAt   = b.CreatedAt,
            UpdatedAt   = b.UpdatedAt
        };
    }

    /// <summary>Maps a <see cref="Contract"/> entity to its DTO.</summary>
    private static ContractDto MapContractToDto(Contract c) => new()
    {
        Id                = c.Id,
        ProjectId         = c.ProjectId,
        ContractNumber    = c.ContractNumber,
        ContractorName    = c.ContractorName,
        ContractorContact = c.ContractorContact,
        ContractValue     = c.ContractValue,
        Currency          = c.Currency,
        StartDate         = c.StartDate,
        EndDate           = c.EndDate,
        Description       = c.Description,
        IsActive          = c.IsActive
    };
}
