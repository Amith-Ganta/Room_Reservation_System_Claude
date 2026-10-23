namespace RoomReservation.Api.Domain;

/// <summary>Converts between the stored <see cref="Equipment"/> bit set and the list shown to clients.</summary>
public static class EquipmentFlags
{
    public static IReadOnlyList<Equipment> Expand(Equipment equipment) =>
        Enum.GetValues<Equipment>()
            .Where(item => item != Equipment.None && equipment.HasFlag(item))
            .ToList();

    public static Equipment Combine(IEnumerable<Equipment> items) =>
        items.Aggregate(Equipment.None, (all, item) => all | item);
}
