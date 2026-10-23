using AutoMapper;
using FluentAssertions;
using RoomReservation.Api.Domain;
using RoomReservation.Api.Dtos;
using RoomReservation.Api.Mapping;
using RoomReservation.Tests.Repositories;

namespace RoomReservation.Tests.Mapping;

public class MappingProfileTests
{
    public static MapperConfiguration NewConfiguration() =>
        new(config => config.AddProfile<MappingProfile>());

    private static readonly IMapper Mapper = NewConfiguration().CreateMapper();

    [Fact]
    public void Configuration_is_valid()
    {
        NewConfiguration().AssertConfigurationIsValid();
    }

    [Fact]
    public void Room_is_mapped_with_equipment_as_a_list()
    {
        var room = TestDb.Room("Atlas", capacity: 12, floor: 2, Equipment.Projector | Equipment.Whiteboard, isActive: false);

        var dto = Mapper.Map<RoomDto>(room);

        dto.Should().BeEquivalentTo(new RoomDto(room.Id, "Atlas", 12, 2, [Equipment.Projector, Equipment.Whiteboard], false));
    }

    [Fact]
    public void Room_without_equipment_has_an_empty_list()
    {
        Mapper.Map<RoomDto>(TestDb.Room()).Equipment.Should().BeEmpty();
    }

    [Fact]
    public void Query_is_mapped_to_a_filter()
    {
        var filter = Mapper.Map<RoomFilter>(new RoomListQuery { MinCapacity = 5, Floor = 0, Equipment = "projector,Whiteboard" });

        filter.MinCapacity.Should().Be(5);
        filter.Floor.Should().Be(0);
        filter.Equipment.Should().Be(Equipment.Projector | Equipment.Whiteboard);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void Blank_equipment_means_no_equipment_filter(string? equipment)
    {
        Mapper.Map<RoomFilter>(new RoomListQuery { Equipment = equipment }).Equipment.Should().BeNull();
    }
}
