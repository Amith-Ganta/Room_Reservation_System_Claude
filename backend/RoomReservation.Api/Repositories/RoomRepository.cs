using Microsoft.EntityFrameworkCore;
using RoomReservation.Api.Data;
using RoomReservation.Api.Domain;

namespace RoomReservation.Api.Repositories;

public interface IRoomRepository : IRepository<Room>
{
    Task<IReadOnlyList<Room>> ListAsync(RoomFilter filter, CancellationToken ct = default);
}

public sealed class RoomRepository(AppDbContext db) : Repository<Room>(db), IRoomRepository
{
    public async Task<IReadOnlyList<Room>> ListAsync(RoomFilter filter, CancellationToken ct = default)
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

        return await query.OrderBy(room => room.Floor).ThenBy(room => room.Name).ToListAsync(ct);
    }
}
