using RoomReservation.Api.Domain;

namespace RoomReservation.Api.Dtos;

/// <summary>Response of every list endpoint: one page of items and the numbers needed to build a pager.</summary>
/// <param name="Items">The rows of this page. Empty when the page is past the end.</param>
/// <param name="Page">The 1-based page number that was served.</param>
/// <param name="PageSize">The page size that was used, after clamping to the configured maximum.</param>
/// <param name="TotalCount">Rows that match the filters over all pages.</param>
/// <param name="TotalPages"><c>ceil(TotalCount / PageSize)</c>, 0 when there is no row.</param>
public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount, int TotalPages)
{
    public static PagedResult<T> Create(IReadOnlyList<T> items, PageRequest request, int totalCount) =>
        new(items, request.Page, request.PageSize, totalCount,
            (int)Math.Ceiling(totalCount / (double)request.PageSize));
}
