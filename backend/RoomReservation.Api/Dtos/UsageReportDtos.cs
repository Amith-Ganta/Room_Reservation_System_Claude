using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using RoomReservation.Api.Common;

namespace RoomReservation.Api.Dtos;

/// <summary>Query string of <c>GET /api/reports/usage</c>. Invalid values are rejected by model validation (HTTP 400).</summary>
public sealed class UsageReportQuery : IValidatableObject
{
    /// <summary>Count reservations that start on or after this instant, an ISO 8601 timestamp with an offset.</summary>
    [ModelBinder(typeof(OffsetDateTimeBinder))]
    public DateTimeOffset? From { get; set; }

    /// <summary>Count reservations that start on or before this instant, an ISO 8601 timestamp with an offset.</summary>
    [ModelBinder(typeof(OffsetDateTimeBinder))]
    public DateTimeOffset? To { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (From is { } from && To is { } to && from > to)
        {
            yield return new ValidationResult("from must not be later than to.", [nameof(From)]);
        }
    }
}

/// <summary>Usage of one room in the report window. All figures are calculated in the back end.</summary>
public sealed record RoomUsageDto(
    Guid RoomId,
    string RoomName,
    int Floor,
    int TotalBookings,
    int CompletedBookings,
    int CancelledBookings,
    int UpcomingBookings,
    decimal PlannedHours,
    decimal UsedHours,
    decimal? UtilizationPercent,
    bool IsOftenBookedRarelyUsed);

/// <summary>The usage report: one row per room, flagged rooms first, plus the thresholds that produced the flags.</summary>
public sealed record UsageReportDto(
    DateTimeOffset GeneratedAt,
    DateTimeOffset? From,
    DateTimeOffset? To,
    int MinBookingsToFlag,
    int UnderusedThresholdPercent,
    IReadOnlyList<RoomUsageDto> Rooms);
