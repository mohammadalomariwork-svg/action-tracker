using Microsoft.EntityFrameworkCore;

namespace ActionTracker.Application.Helpers;

public class PagedResult<T>
{
    public List<T> Items          { get; set; } = new();
    public int     TotalCount     { get; set; }
    public int     PageNumber     { get; set; }
    public int     PageSize       { get; set; }
    public int     TotalPages     => (int)Math.Ceiling(TotalCount / (double)PageSize);
    public bool    HasNextPage     => PageNumber < TotalPages;
    public bool    HasPreviousPage => PageNumber > 1;

    /// <summary>
    /// Counts the full query, then fetches the requested page, and returns a PagedResult.
    /// </summary>
    public static async Task<PagedResult<T>> CreateAsync(
        IQueryable<T> query,
        int pageNumber,
        int pageSize,
        CancellationToken ct = default)
    {
        var totalCount = await query.CountAsync(ct);

        var items = await query
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return new PagedResult<T>
        {
            Items      = items,
            TotalCount = totalCount,
            PageNumber = pageNumber,
            PageSize   = pageSize,
        };
    }
}
