namespace RoomReservation.Api.Domain;

/// <summary>Converts between the stored <see cref="Equipment"/> bit set and the list shown to clients.</summary>
public static class EquipmentFlags
{
    /// <summary>The names a client may use: every equipment type except <see cref="Equipment.None"/>.</summary>
    public static IReadOnlyList<Equipment> Names { get; } =
        Enum.GetValues<Equipment>().Where(item => item != Equipment.None).ToList();

    public static IReadOnlyList<Equipment> Expand(Equipment equipment) =>
        Names.Where(item => equipment.HasFlag(item)).ToList();

    public static Equipment Combine(IEnumerable<Equipment> items) =>
        items.Aggregate(Equipment.None, (all, item) => all | item);

    /// <summary>
    /// Parses names such as <c>"projector, Whiteboard"</c> (case-insensitive). Blank text means no filter.
    /// Numbers, <c>None</c> and unknown names are rejected, so a raw flags value can never be passed in.
    /// </summary>
    public static bool TryParseList(string? text, out Equipment equipment)
    {
        equipment = Equipment.None;
        if (string.IsNullOrWhiteSpace(text))
        {
            return true;
        }

        foreach (var part in text.Split(',', StringSplitOptions.TrimEntries))
        {
            var known = Names.Cast<Equipment?>()
                .FirstOrDefault(item => string.Equals(item.ToString(), part, StringComparison.OrdinalIgnoreCase));
            if (known is null)
            {
                equipment = Equipment.None;
                return false;
            }

            equipment |= known.Value;
        }

        return true;
    }
}
