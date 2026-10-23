using RoomReservation.Api.Repositories;

namespace RoomReservation.Api.Services;

/// <summary>
/// Completes reservations whose planned end has passed. There is no background job: every read that shows a status
/// or an active count calls this first (reservation list, users list, usage report).
/// </summary>
public sealed class ReservationLifecycle(IReservationRepository reservations, TimeProvider timeProvider)
{
    public Task CompleteElapsedAsync(CancellationToken ct = default) =>
        reservations.CompleteElapsedAsync(timeProvider.GetUtcNow(), ct);
}
