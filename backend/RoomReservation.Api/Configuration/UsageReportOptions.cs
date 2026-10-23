using System.ComponentModel.DataAnnotations;

namespace RoomReservation.Api.Configuration;

/// <summary>Thresholds that decide when a room is flagged as "booked often but rarely used".</summary>
public sealed class UsageReportOptions
{
    public const string SectionName = "UsageReport";

    /// <summary>A room needs at least this many resolved (completed or cancelled) bookings to be judged.</summary>
    [Range(1, 1000)]
    public int MinBookingsToFlag { get; set; }

    /// <summary>Rooms with a utilization below this percentage are flagged.</summary>
    [Range(1, 100)]
    public int UnderusedThresholdPercent { get; set; }
}
