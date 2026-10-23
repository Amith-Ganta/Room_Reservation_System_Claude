using Microsoft.EntityFrameworkCore;
using RoomReservation.Api.Data;
using RoomReservation.Api.Domain;

namespace RoomReservation.Api.Repositories;

public interface IUserRepository : IRepository<User>;

public sealed class UserRepository(AppDbContext db) : Repository<User>(db), IUserRepository
{
    /// <summary>Users ordered by last name, then first name.</summary>
    public override async Task<IReadOnlyList<User>> ListAsync(CancellationToken ct = default) =>
        await Db.Users.AsNoTracking().OrderBy(u => u.LastName).ThenBy(u => u.FirstName).ToListAsync(ct);
}
