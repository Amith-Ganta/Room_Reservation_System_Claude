using System.ComponentModel.DataAnnotations;
using RoomReservation.Api.Domain;

namespace RoomReservation.Api.Dtos;

/// <summary>Query string of <c>GET /api/rooms</c>. Invalid values are rejected by model validation (HTTP 400).</summary>
public sealed class RoomListQuery
{
    /// <summary>Keep rooms that seat at least this many people.</summary>
    [Range(0, int.MaxValue, ErrorMessage = "minCapacity must be zero or greater.")]
    public int? MinCapacity { get; set; }

    /// <summary>Comma-separated equipment names, for example <c>Projector,Whiteboard</c>. A room must have all of them.</summary>
    [EquipmentList]
    public string? Equipment { get; set; }

    /// <summary>Keep rooms on this floor (0 = ground floor).</summary>
    public int? Floor { get; set; }
}

/// <summary>Accepts an empty value or a comma-separated list of real equipment names.</summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class EquipmentListAttribute : ValidationAttribute
{
    public override bool IsValid(object? value) =>
        value is null || (value is string text && EquipmentFlags.TryParseList(text, out _));

    public override string FormatErrorMessage(string name) =>
        $"{name} must be a comma-separated list of: {string.Join(", ", EquipmentFlags.Names)}.";
}
