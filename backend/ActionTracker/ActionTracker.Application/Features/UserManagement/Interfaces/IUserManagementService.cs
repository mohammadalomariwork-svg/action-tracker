using ActionTracker.Application.Features.UserManagement.DTOs;

namespace ActionTracker.Application.Features.UserManagement.Interfaces;

/// <summary>
/// Defines the contract for user management operations, including listing,
/// registration, role assignment, employee lookup, and deactivation.
/// </summary>
public interface IUserManagementService
{
    /// <summary>
    /// Returns a paginated list of all ASP.NET Identity users with their assigned roles.
    /// </summary>
    /// <param name="page">The 1-based page number.</param>
    /// <param name="pageSize">The number of users per page.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    Task<UserListResponseDto> GetAllUsersAsync(
        int               page,
        int               pageSize,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns a single user by their Identity user ID, or <c>null</c> if not found.
    /// </summary>
    /// <param name="userId">The ASP.NET Identity user ID (GUID string).</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    Task<UserListItemDto?> GetUserByIdAsync(
        string            userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a new local Identity user with a username and password, then assigns
    /// the specified role.
    /// </summary>
    /// <param name="request">The registration details including credentials and role.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <exception cref="ArgumentException">
    /// Thrown when the email address or username is already registered.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the specified role does not exist.
    /// </exception>
    Task<RegisterUserResponseDto> RegisterExternalUserAsync(
        RegisterExternalUserRequestDto request,
        CancellationToken              cancellationToken = default);

    /// <summary>
    /// Creates a new Identity user with no password for Azure AD / Microsoft account
    /// authentication, then assigns the specified role.
    /// </summary>
    /// <param name="request">
    /// The registration details. The email must match the user's Microsoft account UPN.
    /// </param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <exception cref="ArgumentException">
    /// Thrown when the email address is already registered.
    /// </exception>
    Task<RegisterUserResponseDto> RegisterADUserAsync(
        RegisterADUserRequestDto request,
        CancellationToken        cancellationToken = default);

    /// <summary>
    /// Searches the <c>ku_employee_info</c> table by name, email, or employee ID.
    /// Each result is annotated with <see cref="EmployeeSearchResultDto.AlreadyRegistered"/>
    /// set to <c>true</c> when the employee's email already exists in <c>AspNetUsers</c>.
    /// </summary>
    /// <param name="request">The search term and pagination parameters.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    Task<EmployeeSearchResultDto[]> SearchEmployeesAsync(
        EmployeeSearchRequestDto request,
        CancellationToken        cancellationToken = default);

    /// <summary>
    /// Removes all roles currently assigned to the user and assigns the single
    /// role specified in <paramref name="request"/>.
    /// </summary>
    /// <param name="request">The target user ID and the new role name.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    Task UpdateUserRoleAsync(
        UpdateUserRoleRequestDto request,
        CancellationToken        cancellationToken = default);

    /// <summary>
    /// Deactivates a user account, preventing future logins without deleting
    /// the user record.
    /// </summary>
    /// <param name="userId">The ASP.NET Identity user ID (GUID string).</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    Task DeactivateUserAsync(
        string            userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Reactivates a previously deactivated user account, restoring login access.
    /// </summary>
    /// <param name="userId">The ASP.NET Identity user ID (GUID string).</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    Task ReactivateUserAsync(
        string            userId,
        CancellationToken cancellationToken = default);
}
