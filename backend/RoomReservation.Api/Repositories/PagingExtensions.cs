using Microsoft.EntityFrameworkCore;
using RoomReservation.Api.Domain;

namespace RoomReservation.Api.Repositories;

internal static class PagingExtensions
{
    /// <summary>Counts the filtered rows, then reads one page. The query must already be ordered by a total order.</summary>
    public static async Task<PagedItems<T>> ToPagedAsync<T>(
        this IQueryable<T> query, PageRequest page, CancellationToken ct)
    {
        var total = await query.CountAsync(ct);
        var items = await query.Skip(page.Skip).Take(page.PageSize).ToListAsync(ct);
        return new PagedItems<T>(items, total);
    }
}
