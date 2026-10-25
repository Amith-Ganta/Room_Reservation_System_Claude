using AutoMapper;
using Microsoft.Extensions.Options;
using RoomReservation.Api.Common;
using RoomReservation.Api.Configuration;
using RoomReservation.Api.Domain;
using RoomReservation.Api.Dtos;
using RoomReservation.Api.Repositories;

namespace RoomReservation.Api.Services;

public interface IRoomService
{
    Task<Result<PagedResult<RoomDto>>> ListAsync(RoomListQuery query, CancellationToken ct = default);
}

public sealed class RoomService(IRoomRepository rooms, IMapper mapper, IOptions<PagingOptions> paging) : IRoomService
{
    public async Task<Result<PagedResult<RoomDto>>> ListAsync(RoomListQuery query, CancellationToken ct = default)
    {
        var filter = mapper.Map<RoomFilter>(query);
        var page = query.ToRequest(paging.Value);
        var found = await rooms.ListPagedAsync(filter, page, ct);
        return Result<PagedResult<RoomDto>>.Ok(
            PagedResult<RoomDto>.Create(mapper.Map<List<RoomDto>>(found.Items), page, found.TotalCount));
    }
}
