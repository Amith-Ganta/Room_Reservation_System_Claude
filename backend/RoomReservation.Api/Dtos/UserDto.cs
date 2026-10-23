namespace RoomReservation.Api.Dtos;

/// <summary>A user as shown on the Users page. The count is the number of Booked reservations the user organizes.</summary>
public sealed record UserDto(
    Guid Id,
    string FirstName,
    string LastName,
    int ActiveReservationCount);
