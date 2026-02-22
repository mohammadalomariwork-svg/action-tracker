using ActionTracker.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ActionTracker.Application.Common.Interfaces;

/// <summary>
/// Abstraction over AppDbContext used by Application services to avoid
/// a circular dependency with ActionTracker.Infrastructure.
/// </summary>
public interface IAppDbContext
{
    DbSet<ActionItem> ActionItems { get; }
    DbSet<RefreshToken> RefreshTokens { get; }
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
