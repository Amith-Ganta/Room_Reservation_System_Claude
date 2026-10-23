using System.ComponentModel.DataAnnotations;

namespace RoomReservation.Api.Configuration;

/// <summary>Limits behind rules R1 and R6. Bound from the "BookingRules" section; deliberately has no defaults.</summary>
public sealed class BookingRulesOptions
{
    public const string SectionName = "BookingRules";

    [Range(1, 365)]
    public int MaxDurationDays { get; set; }

    [Range(1, 3650)]
    public int MaxHorizonDays { get; set; }
}
