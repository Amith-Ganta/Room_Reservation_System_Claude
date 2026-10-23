using Microsoft.EntityFrameworkCore;
using RoomReservation.Api.Data;

namespace RoomReservation.Api.Repositories;

public abstract class Repository<T>(AppDbContext context) : IRepository<T> where T : class
{
    protected AppDbContext Db { get; } = context;

    public async Task<T?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        await Db.Set<T>().FindAsync([id], ct);

    public virtual async Task<IReadOnlyList<T>> ListAsync(CancellationToken ct = default) =>
        await Db.Set<T>().AsNoTracking().ToListAsync(ct);

    public async Task AddAsync(T entity, CancellationToken ct = default)
    {
        Db.Set<T>().Add(entity);
        await Db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Saves changes to an entity. Mutate an entity loaded with GetByIdAsync / GetWithDetailsAsync, never one from an
    /// AsNoTracking list. Note: the InMemory provider enforces neither HasMaxLength nor foreign keys, so those
    /// constraints must be validated in the services.
    /// </summary>
    public async Task UpdateAsync(T entity, CancellationToken ct = default)
    {
        Db.Set<T>().Update(entity);
        await Db.SaveChangesAsync(ct);
    }
}
