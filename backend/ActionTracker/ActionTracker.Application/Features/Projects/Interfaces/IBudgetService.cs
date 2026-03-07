using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ActionTracker.Application.Features.Projects.DTOs;

namespace ActionTracker.Application.Features.Projects.Interfaces;

/// <summary>
/// Defines the application-level operations for managing a project's budget
/// record and associated contractor contracts.
/// </summary>
public interface IBudgetService
{
    /// <summary>
    /// Returns the budget record for the specified project, or <c>null</c> if
    /// no budget has been set up yet.
    /// </summary>
    /// <param name="projectId">Primary key of the project.</param>
    Task<ProjectBudgetDto?> GetByProjectAsync(Guid projectId);

    /// <summary>
    /// Creates a new budget record for the project, or fully replaces it if
    /// one already exists (upsert semantics).
    /// </summary>
    /// <param name="dto">Budget values to persist.</param>
    /// <returns>The created or updated budget record.</returns>
    Task<ProjectBudgetDto> CreateOrUpdateAsync(CreateUpdateBudgetDto dto);

    /// <summary>
    /// Returns all active contracts associated with the specified project.
    /// </summary>
    /// <param name="projectId">Primary key of the project.</param>
    Task<IEnumerable<ContractDto>> GetContractsByProjectAsync(Guid projectId);

    /// <summary>
    /// Creates a new contract record associated with a project.
    /// </summary>
    /// <param name="dto">Data for the new contract.</param>
    /// <returns>The newly created contract.</returns>
    Task<ContractDto> CreateContractAsync(CreateContractDto dto);

    /// <summary>
    /// Updates the contract identified by <paramref name="id"/> with the
    /// supplied data.
    /// </summary>
    /// <param name="id">Primary key of the contract to update.</param>
    /// <param name="dto">Updated field values.</param>
    /// <returns>
    /// The updated contract, or <c>null</c> if not found.
    /// </returns>
    Task<ContractDto?> UpdateContractAsync(Guid id, UpdateContractDto dto);

    /// <summary>
    /// Soft-deletes the contract with the given primary key by setting its
    /// <c>IsActive</c> flag to <c>false</c>.
    /// </summary>
    /// <param name="id">Primary key of the contract to delete.</param>
    /// <returns>
    /// <c>true</c> if the record was found and soft-deleted; <c>false</c>
    /// otherwise.
    /// </returns>
    Task<bool> DeleteContractAsync(Guid id);
}
