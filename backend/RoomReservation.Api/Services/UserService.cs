using AutoMapper;
using Microsoft.Extensions.Options;
using RoomReservation.Api.Common;
using RoomReservation.Api.Configuration;
using RoomReservation.Api.Dtos;
using RoomReservation.Api.Mapping;
using RoomReservation.Api.Repositories;

namespace RoomReservation.Api.Services;

public interface IUserService
{
    Task<Result<PagedResult<UserDto>>> ListAsync(UserListQuery query, CancellationToken ct = default);
}

/// <summary>
/// Lists users with their active (Booked) reservation count. Elapsed meetings are completed first, so a meeting that
/// is over no longer counts. The counts come from one grouped query limited to the users on the page, never one query per user.
/// </summary>
public sealed class UserService(
    IUserRepository users,
    IReservationRepository reservations,
    ReservationLifecycle lifecycle,
    IMapper mapper,
    IOptions<PagingOptions> paging) : IUserService
{
    public async Task<Result<PagedResult<UserDto>>> ListAsync(UserListQuery query, CancellationToken ct = default)
    {
        await lifecycle.CompleteElapsedAsync(ct);
        var page = query.ToRequest(paging.Value);
        var found = await users.ListPagedAsync(page, ct);
        var counts = await reservations.CountActiveByUserAsync(found.Items.Select(user => user.Id).ToList(), ct);

        // Users without a Booked reservation are absent from the counts, so they map to 0.
        var dtos = found.Items
            .Select(user => mapper.Map<UserDto>(user, options =>
                options.Items[MappingProfile.ActiveCountItem] = counts.GetValueOrDefault(user.Id)))
            .ToList();
        return Result<PagedResult<UserDto>>.Ok(PagedResult<UserDto>.Create(dtos, page, found.TotalCount));
    }
}
