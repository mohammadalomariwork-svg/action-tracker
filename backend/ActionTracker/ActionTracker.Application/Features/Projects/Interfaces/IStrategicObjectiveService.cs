using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ActionTracker.Application.Features.Projects.DTOs;

namespace ActionTracker.Application.Features.Projects.Interfaces;

/// <summary>
/// Defines the application-level operations for managing strategic objectives.
/// Strategic objectives represent high-level organisational goals to which
/// projects may be aligned.
/// </summary>
public interface IStrategicObjectiveService
{
    /// <summary>
    /// Returns all strategic objectives across all organisation units.
    /// </summary>
    Task<IEnumerable<StrategicObjectiveDto>> GetAllAsync();

    /// <summary>
    /// Returns all strategic objectives that belong to the specified
    /// organisation unit.
    /// </summary>
    /// <param name="orgUnit">
    /// The organisation unit identifier used to filter objectives.
    /// </param>
    Task<IEnumerable<StrategicObjectiveDto>> GetByOrganizationUnitAsync(string orgUnit);

    /// <summary>
    /// Returns the strategic objective with the given primary key, or
    /// <c>null</c> if no matching record exists.
    /// </summary>
    /// <param name="id">Primary key of the strategic objective.</param>
    Task<StrategicObjectiveDto?> GetByIdAsync(Guid id);

    /// <summary>
    /// Creates a new strategic objective from the supplied data.
    /// </summary>
    /// <param name="dto">Data for the new strategic objective.</param>
    /// <returns>The newly created strategic objective.</returns>
    Task<StrategicObjectiveDto> CreateAsync(CreateStrategicObjectiveDto dto);

    /// <summary>
    /// Updates the strategic objective identified by <paramref name="id"/>
    /// with the supplied data.
    /// </summary>
    /// <param name="id">Primary key of the strategic objective to update.</param>
    /// <param name="dto">Updated field values.</param>
    /// <returns>
    /// The updated strategic objective, or <c>null</c> if not found.
    /// </returns>
    Task<StrategicObjectiveDto?> UpdateAsync(Guid id, UpdateStrategicObjectiveDto dto);

    /// <summary>
    /// Deletes the strategic objective with the given primary key.
    /// </summary>
    /// <param name="id">Primary key of the strategic objective to delete.</param>
    /// <returns>
    /// <c>true</c> if the record was found and deleted; <c>false</c> otherwise.
    /// </returns>
    Task<bool> DeleteAsync(Guid id);
}
