namespace RoomReservation.Api.Domain;

/// <summary>A resolved page: both numbers are already checked and clamped, so repositories can use them directly.</summary>
public sealed record PageRequest(int Page, int PageSize)
{
    public int Skip => (Page - 1) * PageSize;
}

/// <summary>One page of rows plus the number of rows that match the filters over all pages.</summary>
public sealed record PagedItems<T>(IReadOnlyList<T> Items, int TotalCount);
