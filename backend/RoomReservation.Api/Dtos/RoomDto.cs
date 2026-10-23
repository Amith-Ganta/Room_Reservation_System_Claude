using RoomReservation.Api.Domain;

namespace RoomReservation.Api.Dtos;

/// <summary>A room as shown to clients. Equipment is a list of names, never the stored flags integer.</summary>
public sealed record RoomDto(
    Guid Id,
    string Name,
    int Capacity,
    int Floor,
    IReadOnlyList<Equipment> Equipment,
    bool IsActive);
