using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using RoomReservation.Api.Common;
using RoomReservation.Api.Domain;

namespace RoomReservation.Api.Dtos;

/// <summary>Query string of <c>GET /api/reservations</c>. Invalid values are rejected by model validation (HTTP 400).</summary>
public sealed class ReservationListQuery : PageQuery, IValidatableObject
{
    /// <summary>Keep reservations of this room. An unknown id gives an empty list.</summary>
    public Guid? RoomId { get; set; }

    /// <summary>Keep reservations of this organizer. An unknown id gives an empty list.</summary>
    public Guid? UserId { get; set; }

    /// <summary><c>Booked</c>, <c>Completed</c> or <c>Cancelled</c>, case-insensitive.</summary>
    [ReservationStatusName]
    public string? Status { get; set; }

    /// <summary>Start of the window (inclusive), an ISO 8601 timestamp with an offset.</summary>
    [ModelBinder(typeof(OffsetDateTimeBinder))]
    public DateTimeOffset? From { get; set; }

    /// <summary>End of the window (exclusive), an ISO 8601 timestamp with an offset.</summary>
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

/// <summary>Accepts an empty value or a status name. Numbers are rejected, unlike the default enum binding.</summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class ReservationStatusNameAttribute : ValidationAttribute
{
    public override bool IsValid(object? value) =>
        value is null || (value is string text && TryParse(text, out _));

    public override string FormatErrorMessage(string name) =>
        $"{name} must be one of: {string.Join(", ", Enum.GetNames<ReservationStatus>())}.";

    /// <summary>Blank means "no filter" (null result). Only exact names, any casing, are accepted.</summary>
    public static bool TryParse(string? text, out ReservationStatus? status)
    {
        status = null;
        if (string.IsNullOrWhiteSpace(text))
        {
            return true;
        }

        var name = Enum.GetNames<ReservationStatus>()
            .FirstOrDefault(candidate => string.Equals(candidate, text.Trim(), StringComparison.OrdinalIgnoreCase));
        if (name is null)
        {
            return false;
        }

        status = Enum.Parse<ReservationStatus>(name);
        return true;
    }
}
