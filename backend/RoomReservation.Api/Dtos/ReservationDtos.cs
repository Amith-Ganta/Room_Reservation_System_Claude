namespace RoomReservation.Api.Dtos;

/// <summary>Input of a new reservation. Checked by <c>IReservationValidator</c>; Spec 05 adds the endpoint.</summary>
public sealed record CreateReservationRequest(
    Guid RoomId,
    Guid UserId,
    string Title,
    DateTimeOffset Start,
    DateTimeOffset End,
    int AttendeeCount);
