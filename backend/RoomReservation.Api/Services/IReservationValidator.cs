using RoomReservation.Api.Common;
using RoomReservation.Api.Domain;
using RoomReservation.Api.Dtos;

namespace RoomReservation.Api.Services;

public interface IReservationValidator
{
    /// <summary>
    /// Checks a reservation against the basic input rules and R1-R7 and returns every violation (empty = valid).
    /// Basic input errors are returned alone. The check never throws for expected failures and never writes.
    /// </summary>
    /// <remarks>
    /// The check and the later insert are separate steps. The caller must run both under the one static write lock,
    /// otherwise two parallel requests can both pass R2/R3 and double-book. The caller must also load the room and
    /// the organizer first (404 when missing).
    /// </remarks>
    /// <param name="room">The room named by <c>request.RoomId</c>; the caller has already loaded it. Overlaps are checked against <c>room.Id</c>.</param>
    /// <param name="excludeReservationId">A reservation that must not count as an overlap (used when one is changed).</param>
    Task<IReadOnlyList<ApiError>> ValidateAsync(
        CreateReservationRequest request, Room room, Guid? excludeReservationId = null, CancellationToken ct = default);
}
