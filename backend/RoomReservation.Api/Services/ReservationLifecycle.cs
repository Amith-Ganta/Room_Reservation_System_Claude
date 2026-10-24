using RoomReservation.Api.Repositories;

namespace RoomReservation.Api.Services;

/// <summary>
/// Completes reservations whose planned end has passed. There is no background job: every read that shows a status
/// or an active count calls this first (reservation list, users list, usage report).
/// Reads call it without the write lock on purpose: it only moves Booked to Completed once the planned end has passed,
/// so running it twice, or beside a write that does the same first, gives the same result. Writes call it inside the lock.
/// </summary>
public sealed class ReservationLifecycle(IReservationRepository reservations, TimeProvider timeProvider)
{
    public Task CompleteElapsedAsync(CancellationToken ct = default) =>
        reservations.CompleteElapsedAsync(timeProvider.GetUtcNow(), ct);
}
