namespace RoomReservation.Api.Common;

/// <summary>Stable machine-readable codes returned to the client. R1..R7 map to the booking rules in the task.</summary>
public static class ErrorCodes
{
    public const string MaxDuration = "R1_MAX_DURATION";
    public const string RoomOverlap = "R2_ROOM_OVERLAP";
    public const string UserOverlap = "R3_USER_OVERLAP";
    public const string PastStart = "R4_PAST_START";
    public const string CapacityExceeded = "R5_CAPACITY_EXCEEDED";
    public const string BeyondHorizon = "R6_BEYOND_HORIZON";
    public const string RoomInactive = "R7_ROOM_INACTIVE";

    public const string InvalidTimeRange = "INVALID_TIME_RANGE";
    public const string TitleRequired = "TITLE_REQUIRED";
    public const string TitleTooLong = "TITLE_TOO_LONG";
    public const string InvalidAttendees = "INVALID_ATTENDEES";
    public const string RoomNotFound = "ROOM_NOT_FOUND";
    public const string UserNotFound = "USER_NOT_FOUND";
    public const string ReservationNotFound = "RESERVATION_NOT_FOUND";

    public const string NotBooked = "RESERVATION_NOT_BOOKED";
    public const string AlreadyStarted = "RESERVATION_ALREADY_STARTED";
    public const string NotStarted = "RESERVATION_NOT_STARTED";

    public const string InvalidRequest = "INVALID_REQUEST";
    public const string InternalError = "INTERNAL_ERROR";
}
