using ActionTracker.Application.Features.UserManagement.DTOs;
using ActionTracker.Application.Features.UserManagement.Interfaces;
using ActionTracker.Domain.Entities;
using ActionTracker.Infrastructure.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ActionTracker.Infrastructure.Services;

public class UserManagementService : IUserManagementService
{
    private readonly UserManager<ApplicationUser>     _userManager;
    private readonly RoleManager<IdentityRole>        _roleManager;
    private readonly AppDbContext                     _context;
    private readonly ILogger<UserManagementService>  _logger;

    public UserManagementService(
        UserManager<ApplicationUser>    userManager,
        RoleManager<IdentityRole>       roleManager,
        AppDbContext                    context,
        ILogger<UserManagementService> logger)
    {
        _userManager = userManager;
        _roleManager = roleManager;
        _context     = context;
        _logger      = logger;
    }

    // -------------------------------------------------------------------------
    // GetAllUsersAsync
    // -------------------------------------------------------------------------

    public async Task<UserListResponseDto> GetAllUsersAsync(
        int               page,
        int               pageSize,
        string            search            = "",
        string            sortBy            = "fullName",
        string            sortDir           = "asc",
        CancellationToken cancellationToken = default)
    {
        var term = (search ?? string.Empty).Trim();

        var query = _userManager.Users.AsQueryable();

        if (!string.IsNullOrEmpty(term))
        {
            query = query.Where(u =>
                (u.FirstName + " " + u.LastName).Contains(term) ||
                u.FirstName.Contains(term) ||
                u.LastName.Contains(term) ||
                (u.Email != null && u.Email.Contains(term)) ||
                (u.UserName != null && u.UserName.Contains(term)));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        bool descending = string.Equals(sortDir, "desc", StringComparison.OrdinalIgnoreCase);

        query = sortBy?.ToLowerInvariant() switch
        {
            "email"    => descending ? query.OrderByDescending(u => u.Email)    : query.OrderBy(u => u.Email),
            "username" => descending ? query.OrderByDescending(u => u.UserName) : query.OrderBy(u => u.UserName),
            _          => descending
                            ? query.OrderByDescending(u => u.LastName).ThenByDescending(u => u.FirstName)
                            : query.OrderBy(u => u.LastName).ThenBy(u => u.FirstName),
        };

        var users = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var items = new List<UserListItemDto>(users.Count);
        foreach (var user in users)
        {
            var roles = await _userManager.GetRolesAsync(user);
            items.Add(MapToListItem(user, roles));
        }

        return new UserListResponseDto
        {
            Users      = items,
            TotalCount = totalCount,
            Page       = page,
            PageSize   = pageSize,
        };
    }

    // -------------------------------------------------------------------------
    // GetUserByIdAsync
    // -------------------------------------------------------------------------

    public async Task<UserListItemDto?> GetUserByIdAsync(
        string            userId,
        CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user is null) return null;

        var roles = await _userManager.GetRolesAsync(user);
        return MapToListItem(user, roles);
    }

    // -------------------------------------------------------------------------
    // RegisterExternalUserAsync
    // -------------------------------------------------------------------------

    public async Task<RegisterUserResponseDto> RegisterExternalUserAsync(
        RegisterExternalUserRequestDto request,
        CancellationToken              cancellationToken = default)
    {
        if (!await _roleManager.RoleExistsAsync(request.RoleName))
            throw new InvalidOperationException(
                $"Role '{request.RoleName}' does not exist.");

        if (await _userManager.FindByEmailAsync(request.Email) is not null)
            throw new ArgumentException(
                $"A user with email '{request.Email}' is already registered.", nameof(request));

        var (firstName, lastName) = SplitFullName(request.FullName);

        var user = new ApplicationUser
        {
            UserName       = request.Email,   // email doubles as username for external accounts
            Email          = request.Email,
            FirstName      = firstName,
            LastName       = lastName,
            PhoneNumber    = request.PhoneNumber,
            LoginProvider  = "Local",
            IsActive       = true,
            EmailConfirmed = true,
            CreatedAt      = DateTime.UtcNow,
        };

        var createResult = await _userManager.CreateAsync(user, request.Password);
        if (!createResult.Succeeded)
        {
            var errors = string.Join("; ", createResult.Errors.Select(e => e.Description));
            _logger.LogError("Failed to create external user {Email}: {Errors}", request.Email, errors);
            throw new InvalidOperationException($"User creation failed: {errors}");
        }

        await AssignRoleAsync(user, request.RoleName);

        var roles = await _userManager.GetRolesAsync(user);
        return MapToResponse(user, roles);
    }

    // -------------------------------------------------------------------------
    // RegisterADUserAsync
    // -------------------------------------------------------------------------

    public async Task<RegisterUserResponseDto> RegisterADUserAsync(
        RegisterADUserRequestDto request,
        CancellationToken        cancellationToken = default)
    {
        if (!await _roleManager.RoleExistsAsync(request.RoleName))
            throw new InvalidOperationException(
                $"Role '{request.RoleName}' does not exist.");

        if (await _userManager.FindByEmailAsync(request.Email) is not null)
            throw new ArgumentException(
                $"A user with email '{request.Email}' is already registered.", nameof(request));

        var (firstName, lastName) = SplitFullName(request.FullName);

        var user = new ApplicationUser
        {
            UserName       = request.Email,   // UPN doubles as username for AD accounts
            Email          = request.Email,
            FirstName      = firstName,
            LastName       = lastName,
            PhoneNumber    = request.PhoneNumber,
            Department     = request.Department ?? string.Empty,
            LoginProvider  = "AzureAD",
            IsActive       = true,
            EmailConfirmed = true,
            CreatedAt      = DateTime.UtcNow,
        };

        // Create without password — AD accounts authenticate via Azure AD only.
        var createResult = await _userManager.CreateAsync(user);
        if (!createResult.Succeeded)
        {
            var errors = string.Join("; ", createResult.Errors.Select(e => e.Description));
            _logger.LogError("Failed to create AD user {Email}: {Errors}", request.Email, errors);
            throw new InvalidOperationException($"AD user creation failed: {errors}");
        }

        await AssignRoleAsync(user, request.RoleName);

        var roles = await _userManager.GetRolesAsync(user);
        return MapToResponse(user, roles);
    }

    // -------------------------------------------------------------------------
    // SearchEmployeesAsync
    // -------------------------------------------------------------------------

    public async Task<EmployeeSearchResultDto[]> SearchEmployeesAsync(
        EmployeeSearchRequestDto request,
        CancellationToken        cancellationToken = default)
    {
        var term = request.SearchTerm.Trim();

        var query = _context.KuEmployeeInfo
            .Where(e =>
                (e.EmployeeName      != null && e.EmployeeName.Contains(term))      ||
                (e.EmployeeArabicName!= null && e.EmployeeArabicName.Contains(term))||
                (e.EmailAddress      != null && e.EmailAddress.Contains(term))       ||
                (e.EmpNo             != null && e.EmpNo.Contains(term))              ||
                (e.EBSEmployeeNumber != null && e.EBSEmployeeNumber.Contains(term)));

        var employees = await query
            .OrderBy(e => e.EmployeeName)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(cancellationToken);

        // Collect distinct emails to batch-check registration status.
        var emails = employees
            .Where(e => e.EmailAddress != null)
            .Select(e => e.EmailAddress!)
            .Distinct()
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var registeredEmails = await _userManager.Users
            .Where(u => u.Email != null && emails.Contains(u.Email))
            .Select(u => u.Email!)
            .ToHashSetAsync(StringComparer.OrdinalIgnoreCase, cancellationToken);

        return employees.Select(e => new EmployeeSearchResultDto
        {
            EmployeeId          = e.EmpNo ?? string.Empty,
            EmpNo               = e.EmpNo,
            EbsEmployeeNumber   = e.EBSEmployeeNumber,
            FullName            = e.EmployeeName ?? string.Empty,
            EmployeeArabicName  = e.EmployeeArabicName,
            Email               = e.EmailAddress ?? string.Empty,
            Department          = e.Department,
            JobTitle            = e.Position,
            PhoneNumber         = null,   // ku_employee_info does not carry a phone number
            AlreadyRegistered   = e.EmailAddress != null &&
                                  registeredEmails.Contains(e.EmailAddress),
        }).ToArray();
    }

    // -------------------------------------------------------------------------
    // UpdateUserRoleAsync
    // -------------------------------------------------------------------------

    public async Task UpdateUserRoleAsync(
        UpdateUserRoleRequestDto request,
        CancellationToken        cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(request.UserId)
            ?? throw new KeyNotFoundException($"User '{request.UserId}' not found.");

        if (!await _roleManager.RoleExistsAsync(request.RoleName))
            throw new InvalidOperationException(
                $"Role '{request.RoleName}' does not exist.");

        var currentRoles = await _userManager.GetRolesAsync(user);
        if (currentRoles.Any())
        {
            var removeResult = await _userManager.RemoveFromRolesAsync(user, currentRoles);
            if (!removeResult.Succeeded)
            {
                var errors = string.Join("; ", removeResult.Errors.Select(e => e.Description));
                _logger.LogError("Failed to remove roles from user {UserId}: {Errors}", request.UserId, errors);
                throw new InvalidOperationException($"Failed to remove existing roles: {errors}");
            }
        }

        await AssignRoleAsync(user, request.RoleName);

        _logger.LogInformation(
            "User {UserId} role updated to '{Role}'", request.UserId, request.RoleName);
    }

    // -------------------------------------------------------------------------
    // DeactivateUserAsync
    // -------------------------------------------------------------------------

    public async Task DeactivateUserAsync(
        string            userId,
        CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(userId)
            ?? throw new KeyNotFoundException($"User '{userId}' not found.");

        user.IsActive = false;

        var result = await _userManager.UpdateAsync(user);
        if (!result.Succeeded)
        {
            var errors = string.Join("; ", result.Errors.Select(e => e.Description));
            _logger.LogError("Failed to deactivate user {UserId}: {Errors}", userId, errors);
            throw new InvalidOperationException($"Failed to deactivate user: {errors}");
        }

        _logger.LogInformation("User {UserId} deactivated", userId);
    }

    // -------------------------------------------------------------------------
    // ReactivateUserAsync
    // -------------------------------------------------------------------------

    public async Task ReactivateUserAsync(
        string            userId,
        CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(userId)
            ?? throw new KeyNotFoundException($"User '{userId}' not found.");

        user.IsActive = true;

        var result = await _userManager.UpdateAsync(user);
        if (!result.Succeeded)
        {
            var errors = string.Join("; ", result.Errors.Select(e => e.Description));
            _logger.LogError("Failed to reactivate user {UserId}: {Errors}", userId, errors);
            throw new InvalidOperationException($"Failed to reactivate user: {errors}");
        }

        _logger.LogInformation("User {UserId} reactivated", userId);
    }

    // -------------------------------------------------------------------------
    // Private helpers
    // -------------------------------------------------------------------------

    private async Task AssignRoleAsync(ApplicationUser user, string roleName)
    {
        var addResult = await _userManager.AddToRoleAsync(user, roleName);
        if (!addResult.Succeeded)
        {
            var errors = string.Join("; ", addResult.Errors.Select(e => e.Description));
            _logger.LogError(
                "Failed to assign role '{Role}' to user {Email}: {Errors}",
                roleName, user.Email, errors);
            throw new InvalidOperationException($"Role assignment failed: {errors}");
        }
    }

    private static (string firstName, string lastName) SplitFullName(string fullName)
    {
        var trimmed = fullName.Trim();
        var space   = trimmed.IndexOf(' ');
        return space < 0
            ? (trimmed, string.Empty)
            : (trimmed[..space], trimmed[(space + 1)..]);
    }

    private static UserListItemDto MapToListItem(
        ApplicationUser user, IList<string> roles) => new()
    {
        Id          = user.Id,
        UserName    = user.UserName ?? string.Empty,
        Email       = user.Email    ?? string.Empty,
        FullName    = user.FullName,
        PhoneNumber = user.PhoneNumber,
        IsADUser    = user.LoginProvider == "AzureAD",
        IsActive    = user.IsActive,
        Roles       = [.. roles],
        CreatedAt   = user.CreatedAt,
    };

    private static RegisterUserResponseDto MapToResponse(
        ApplicationUser user, IList<string> roles) => new()
    {
        UserId    = user.Id,
        UserName  = user.UserName ?? string.Empty,
        Email     = user.Email    ?? string.Empty,
        FullName  = user.FullName,
        Roles     = [.. roles],
        IsADUser  = user.LoginProvider == "AzureAD",
        CreatedAt = user.CreatedAt,
    };
}
