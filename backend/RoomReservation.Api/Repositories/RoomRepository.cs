using Microsoft.EntityFrameworkCore;
using RoomReservation.Api.Data;
using RoomReservation.Api.Domain;

namespace RoomReservation.Api.Repositories;

public interface IRoomRepository : IRepository<Room>
{
    /// <summary>Every room that matches the filter, unpaged. Used by the usage report, which must see all rooms.</summary>
    Task<IReadOnlyList<Room>> ListAsync(RoomFilter filter, CancellationToken ct = default);

    /// <summary>One page of the rooms that match the filter, ordered by floor, name, then id.</summary>
    Task<PagedItems<Room>> ListPagedAsync(RoomFilter filter, PageRequest page, CancellationToken ct = default);
}

public sealed class RoomRepository(AppDbContext db) : Repository<Room>(db), IRoomRepository
{
    public async Task<IReadOnlyList<Room>> ListAsync(RoomFilter filter, CancellationToken ct = default) =>
        await Filtered(filter).OrderBy(room => room.Floor).ThenBy(room => room.Name).ThenBy(room => room.Id).ToListAsync(ct);

    public Task<PagedItems<Room>> ListPagedAsync(RoomFilter filter, PageRequest page, CancellationToken ct = default) =>
        Filtered(filter).OrderBy(room => room.Floor).ThenBy(room => room.Name).ThenBy(room => room.Id).ToPagedAsync(page, ct);

    private IQueryable<Room> Filtered(RoomFilter filter)
    {
        var query = Db.Rooms.AsNoTracking();

        if (filter.MinCapacity is { } minCapacity)
        {
            query = query.Where(room => room.Capacity >= minCapacity);
        }

        if (filter.Floor is { } floor)
        {
            query = query.Where(room => room.Floor == floor);
        }

        if (filter.Equipment is { } required && required != Equipment.None)
        {
            query = query.Where(room => (room.Equipment & required) == required);
        }

        return query;
    }
}
