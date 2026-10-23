using FluentAssertions;
using RoomReservation.Api.Domain;

namespace RoomReservation.Tests.Domain;

public class EquipmentFlagsTests
{
    [Fact]
    public void Expand_lists_each_set_flag_and_skips_none()
    {
        EquipmentFlags.Expand(Equipment.Projector | Equipment.Whiteboard)
            .Should().Equal(Equipment.Projector, Equipment.Whiteboard);
        EquipmentFlags.Expand(Equipment.None).Should().BeEmpty();
    }

    [Fact]
    public void Combine_is_the_inverse_of_expand()
    {
        var all = Equipment.Projector | Equipment.TvScreen | Equipment.VideoConferencing | Equipment.Whiteboard;

        EquipmentFlags.Combine(EquipmentFlags.Expand(all)).Should().Be(all);
        EquipmentFlags.Combine([]).Should().Be(Equipment.None);
        EquipmentFlags.Combine([Equipment.TvScreen, Equipment.TvScreen]).Should().Be(Equipment.TvScreen);
    }
}
