using FluentAssertions;
using RoomReservation.Api.Common;
using RoomReservation.Api.Domain;
using RoomReservation.Api.Dtos;
using RoomReservation.Api.Repositories;
using RoomReservation.Api.Services;
using RoomReservation.Tests.Mapping;
using RoomReservation.Tests.Repositories;

namespace RoomReservation.Tests.Services;

public class RoomServiceTests
{
    private static async Task<RoomService> ServiceAsync()
    {
        var db = TestDb.Create();
        db.Rooms.AddRange(
            TestDb.Room("Small", capacity: 4, floor: 1, Equipment.Whiteboard),
            TestDb.Room("Medium", capacity: 10, floor: 2, Equipment.TvScreen | Equipment.VideoConferencing),
            TestDb.Room("Large", capacity: 30, floor: 2, Equipment.Projector | Equipment.TvScreen),
            TestDb.Room("Closed", capacity: 8, floor: 0, Equipment.None, isActive: false));
        await db.SaveChangesAsync();
        var mapper = MappingProfileTests.NewConfiguration().CreateMapper();
        return new RoomService(new RoomRepository(db), mapper, TestDb.Paging());
    }

    private static async Task<IReadOnlyList<RoomDto>> ListAsync(RoomListQuery query)
    {
        var result = await (await ServiceAsync()).ListAsync(query);
        result.Status.Should().Be(ResultStatus.Ok);
        return result.Value!.Items;
    }

    [Fact]
    public async Task No_filters_returns_every_room_including_inactive_ordered_by_floor_then_name()
    {
        var rooms = await ListAsync(new RoomListQuery());

        rooms.Select(r => r.Name).Should().Equal("Closed", "Small", "Large", "Medium");
        rooms.Single(r => r.Name == "Closed").IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task Equipment_text_is_turned_into_a_filter_and_returned_as_names()
    {
        var rooms = await ListAsync(new RoomListQuery { Equipment = "tvscreen,projector" });

        rooms.Should().ContainSingle().Which.Equipment.Should().Equal(Equipment.Projector, Equipment.TvScreen);
    }

    [Fact]
    public async Task Filters_are_combined()
    {
        var rooms = await ListAsync(new RoomListQuery { MinCapacity = 5, Floor = 2, Equipment = "VideoConferencing" });

        rooms.Select(r => r.Name).Should().Equal("Medium");
    }

    [Fact]
    public async Task Floor_zero_is_a_real_filter()
    {
        var rooms = await ListAsync(new RoomListQuery { Floor = 0 });

        rooms.Select(r => r.Name).Should().Equal("Closed");
    }

    [Fact]
    public async Task No_match_returns_an_empty_list_not_an_error()
    {
        var rooms = await ListAsync(new RoomListQuery { MinCapacity = 1000 });

        rooms.Should().BeEmpty();
    }
}
