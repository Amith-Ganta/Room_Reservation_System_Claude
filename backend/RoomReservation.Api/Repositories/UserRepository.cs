using Microsoft.EntityFrameworkCore;
using RoomReservation.Api.Data;
using RoomReservation.Api.Domain;

namespace RoomReservation.Api.Repositories;

public interface IUserRepository : IRepository<User>
{
    /// <summary>One page of users ordered by last name, first name, then id.</summary>
    Task<PagedItems<User>> ListPagedAsync(PageRequest page, CancellationToken ct = default);
}

public sealed class UserRepository(AppDbContext db) : Repository<User>(db), IUserRepository
{
    public Task<PagedItems<User>> ListPagedAsync(PageRequest page, CancellationToken ct = default) =>
        Db.Users.AsNoTracking().OrderBy(u => u.LastName).ThenBy(u => u.FirstName).ThenBy(u => u.Id).ToPagedAsync(page, ct);
}
