namespace RoomReservation.Api.Domain;

/// <summary>Room list filters. All supplied filters are AND-combined.</summary>
public class RoomFilter
{
    public int? MinCapacity { get; set; }
    public int? Floor { get; set; }

    /// <summary>A room must have every flag requested here.</summary>
    public Equipment? Equipment { get; set; }
}

/// <summary>Reservation list filters. All supplied filters are AND-combined.</summary>
public class ReservationFilter
{
    public Guid? RoomId { get; set; }
    public Guid? UserId { get; set; }
    public ReservationStatus? Status { get; set; }

    /// <summary>Keeps reservations that overlap the [From, To] window.</summary>
    public DateTimeOffset? From { get; set; }
    public DateTimeOffset? To { get; set; }
}

public class UsageReportFilter
{
    /// <summary>Only reservations starting on or after this instant are counted.</summary>
    public DateTimeOffset? From { get; set; }

    /// <summary>Only reservations starting on or before this instant are counted.</summary>
    public DateTimeOffset? To { get; set; }
}
