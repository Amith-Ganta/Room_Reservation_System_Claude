namespace RoomReservation.Api.Dtos;

/// <summary>
/// Input of a new or changed reservation. Checked by <c>IReservationValidator</c>.
/// <c>Title</c> is nullable on purpose: a missing title must reach the validator (422 TITLE_REQUIRED), not model binding (400).
/// </summary>
public sealed record CreateReservationRequest(
    Guid RoomId,
    Guid UserId,
    string? Title,
    DateTimeOffset Start,
    DateTimeOffset End,
    int AttendeeCount);

/// <summary>
/// A reservation as shown to clients. <c>End</c> is the planned end and is never rewritten;
/// <c>EndedAt</c> is set only when the meeting was ended early. <c>Status</c> is the name, never a number.
/// </summary>
public sealed record ReservationDto(
    Guid Id,
    Guid RoomId,
    string RoomName,
    Guid UserId,
    string OrganizerName,
    string Title,
    DateTimeOffset Start,
    DateTimeOffset End,
    DateTimeOffset? EndedAt,
    int AttendeeCount,
    string Status);
