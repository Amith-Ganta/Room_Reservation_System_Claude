namespace RoomReservation.Api.Domain;

public class Reservation
{
    public Guid Id { get; set; }
    public Guid RoomId { get; set; }
    public Room Room { get; set; } = null!;
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
    public string Title { get; set; } = string.Empty;

    /// <summary>Planned start (UTC).</summary>
    public DateTimeOffset Start { get; set; }

    /// <summary>Planned end (UTC). Never rewritten, so usage reports can compare plan vs. reality.</summary>
    public DateTimeOffset End { get; set; }

    /// <summary>Set when a meeting is ended early; null otherwise.</summary>
    public DateTimeOffset? EndedAt { get; set; }

    public int AttendeeCount { get; set; }
    public ReservationStatus Status { get; set; }
}
