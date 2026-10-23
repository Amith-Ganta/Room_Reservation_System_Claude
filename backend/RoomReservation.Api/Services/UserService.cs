using AutoMapper;
using RoomReservation.Api.Common;
using RoomReservation.Api.Dtos;
using RoomReservation.Api.Mapping;
using RoomReservation.Api.Repositories;

namespace RoomReservation.Api.Services;

public interface IUserService
{
    Task<Result<IReadOnlyList<UserDto>>> ListAsync(CancellationToken ct = default);
}

/// <summary>
/// Lists users with their active (Booked) reservation count. Elapsed meetings are completed first, so a meeting that
/// is over no longer counts. The counts come from one grouped query, never one query per user.
/// </summary>
public sealed class UserService(
    IUserRepository users,
    IReservationRepository reservations,
    ReservationLifecycle lifecycle,
    IMapper mapper) : IUserService
{
    public async Task<Result<IReadOnlyList<UserDto>>> ListAsync(CancellationToken ct = default)
    {
        await lifecycle.CompleteElapsedAsync(ct);
        var found = await users.ListAsync(ct);
        var counts = await reservations.CountActiveByUserAsync(ct);

        // Users without a Booked reservation are absent from the counts, so they map to 0.
        var dtos = found
            .Select(user => mapper.Map<UserDto>(user, options =>
                options.Items[MappingProfile.ActiveCountItem] = counts.GetValueOrDefault(user.Id)))
            .ToList();
        return Result<IReadOnlyList<UserDto>>.Ok(dtos);
    }
}
