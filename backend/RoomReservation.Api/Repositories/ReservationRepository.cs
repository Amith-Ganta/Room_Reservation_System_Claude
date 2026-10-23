using Microsoft.EntityFrameworkCore;
using RoomReservation.Api.Data;
using RoomReservation.Api.Domain;

namespace RoomReservation.Api.Repositories;

public interface IReservationRepository : IRepository<Reservation>
{
    Task<Reservation?> GetWithDetailsAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<Reservation>> ListAsync(ReservationFilter filter, CancellationToken ct = default);
    Task<IReadOnlyList<Reservation>> ListForReportAsync(UsageReportFilter filter, CancellationToken ct = default);

    /// <summary>
    /// Another non-cancelled reservation in the room overlaps [start, end). Back-to-back meetings do not overlap,
    /// and a meeting ended early frees the room from <c>EndedAt</c>.
    /// </summary>
    Task<bool> HasRoomOverlapAsync(Guid roomId, DateTimeOffset start, DateTimeOffset end, Guid? excludeId, CancellationToken ct = default);

    /// <summary>The organizer already has another non-cancelled reservation overlapping [start, end).</summary>
    Task<bool> HasUserOverlapAsync(Guid userId, DateTimeOffset start, DateTimeOffset end, Guid? excludeId, CancellationToken ct = default);

    /// <summary>
    /// Number of Booked reservations per organizer. Users without any are absent from the result.
    /// A booking whose end has passed still counts until <see cref="CompleteElapsedAsync"/> runs, so callers
    /// should complete elapsed reservations first.
    /// </summary>
    Task<IReadOnlyDictionary<Guid, int>> CountActiveByUserAsync(CancellationToken ct = default);

    /// <summary>Marks every Booked reservation whose planned end has passed as Completed.</summary>
    Task CompleteElapsedAsync(DateTimeOffset now, CancellationToken ct = default);
}

public sealed class ReservationRepository(AppDbContext db) : Repository<Reservation>(db), IReservationRepository
{
    public Task<Reservation?> GetWithDetailsAsync(Guid id, CancellationToken ct = default) =>
        Db.Reservations.Include(r => r.Room).Include(r => r.User).FirstOrDefaultAsync(r => r.Id == id, ct);

    // Read-only: the list is AsNoTracking, so never mutate these entities and pass them to UpdateAsync
    // (that would re-attach the Room/User graph). Load via GetWithDetailsAsync or GetByIdAsync to change one.
    public async Task<IReadOnlyList<Reservation>> ListAsync(ReservationFilter filter, CancellationToken ct = default)
    {
        var query = Db.Reservations.AsNoTracking().Include(r => r.Room).Include(r => r.User).AsQueryable();

        if (filter.RoomId is { } roomId)
        {
            query = query.Where(r => r.RoomId == roomId);
        }

        if (filter.UserId is { } userId)
        {
            query = query.Where(r => r.UserId == userId);
        }

        if (filter.Status is { } status)
        {
            query = query.Where(r => r.Status == status);
        }

        // Half-open window [From, To): same interval semantics as the overlap checks (an early end shortens the meeting).
        // From == To is an empty window, so nothing matches (the spec allows equal values).
        if (filter is { From: { } start, To: { } stop } && start >= stop)
        {
            return [];
        }

        if (filter.From is { } from)
        {
            query = query.Where(r => (r.EndedAt ?? r.End) > from);
        }

        if (filter.To is { } to)
        {
            query = query.Where(r => r.Start < to);
        }

        return await query.OrderByDescending(r => r.Start).ToListAsync(ct);
    }

    public async Task<IReadOnlyList<Reservation>> ListForReportAsync(UsageReportFilter filter, CancellationToken ct = default)
    {
        var query = Db.Reservations.AsNoTracking().Include(r => r.Room).AsQueryable();

        if (filter.From is { } from)
        {
            query = query.Where(r => r.Start >= from);
        }

        if (filter.To is { } to)
        {
            query = query.Where(r => r.Start <= to);
        }

        return await query.ToListAsync(ct);
    }

    public Task<bool> HasRoomOverlapAsync(Guid roomId, DateTimeOffset start, DateTimeOffset end, Guid? excludeId, CancellationToken ct = default) =>
        Db.Reservations.AnyAsync(r =>
            r.RoomId == roomId
            && r.Status != ReservationStatus.Cancelled
            && r.Id != excludeId
            && r.Start < end
            && start < (r.EndedAt ?? r.End), ct);

    public Task<bool> HasUserOverlapAsync(Guid userId, DateTimeOffset start, DateTimeOffset end, Guid? excludeId, CancellationToken ct = default) =>
        Db.Reservations.AnyAsync(r =>
            r.UserId == userId
            && r.Status != ReservationStatus.Cancelled
            && r.Id != excludeId
            && r.Start < end
            && start < (r.EndedAt ?? r.End), ct);

    public async Task<IReadOnlyDictionary<Guid, int>> CountActiveByUserAsync(CancellationToken ct = default)
    {
        var counts = await Db.Reservations
            .Where(r => r.Status == ReservationStatus.Booked)
            .GroupBy(r => r.UserId)
            .Select(group => new { UserId = group.Key, Count = group.Count() })
            .ToListAsync(ct);

        return counts.ToDictionary(c => c.UserId, c => c.Count);
    }

    public async Task CompleteElapsedAsync(DateTimeOffset now, CancellationToken ct = default)
    {
        var elapsed = await Db.Reservations
            .Where(r => r.Status == ReservationStatus.Booked && r.End <= now)
            .ToListAsync(ct);

        if (elapsed.Count == 0)
        {
            return;
        }

        foreach (var reservation in elapsed)
        {
            reservation.Status = ReservationStatus.Completed;
        }

        await Db.SaveChangesAsync(ct);
    }
}
