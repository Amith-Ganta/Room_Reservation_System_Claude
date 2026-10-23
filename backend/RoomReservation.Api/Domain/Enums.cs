namespace RoomReservation.Api.Domain;

[Flags]
public enum Equipment
{
    None = 0,
    Projector = 1,
    TvScreen = 2,
    VideoConferencing = 4,
    Whiteboard = 8
}

public enum ReservationStatus
{
    Booked,
    Completed,
    Cancelled
}
