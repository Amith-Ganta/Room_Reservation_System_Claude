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

    [Theory]
    [InlineData(null, Equipment.None)]
    [InlineData("", Equipment.None)]
    [InlineData("   ", Equipment.None)]
    [InlineData("Projector", Equipment.Projector)]
    [InlineData("projector, WHITEBOARD", Equipment.Projector | Equipment.Whiteboard)]
    [InlineData("TvScreen,TvScreen", Equipment.TvScreen)]
    public void TryParseList_accepts_names_case_insensitively(string? text, Equipment expected)
    {
        EquipmentFlags.TryParseList(text, out var parsed).Should().BeTrue();
        parsed.Should().Be(expected);
    }

    [Theory]
    [InlineData("Laser")]
    [InlineData("None")]
    [InlineData("1")]
    [InlineData("15")]
    [InlineData("Projector,")]
    [InlineData("Projector,Laser")]
    [InlineData(",")]
    public void TryParseList_rejects_unknown_names_numbers_and_none(string text)
    {
        EquipmentFlags.TryParseList(text, out var parsed).Should().BeFalse();
        parsed.Should().Be(Equipment.None);
    }
}
