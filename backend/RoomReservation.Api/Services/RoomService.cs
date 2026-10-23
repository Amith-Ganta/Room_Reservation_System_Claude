using AutoMapper;
using RoomReservation.Api.Common;
using RoomReservation.Api.Domain;
using RoomReservation.Api.Dtos;
using RoomReservation.Api.Repositories;

namespace RoomReservation.Api.Services;

public interface IRoomService
{
    Task<Result<IReadOnlyList<RoomDto>>> ListAsync(RoomListQuery query, CancellationToken ct = default);
}

public sealed class RoomService(IRoomRepository rooms, IMapper mapper) : IRoomService
{
    public async Task<Result<IReadOnlyList<RoomDto>>> ListAsync(RoomListQuery query, CancellationToken ct = default)
    {
        var filter = mapper.Map<RoomFilter>(query);
        var found = await rooms.ListAsync(filter, ct);
        return Result<IReadOnlyList<RoomDto>>.Ok(mapper.Map<List<RoomDto>>(found));
    }
}
