using AutoMapper;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using RoomReservation.Api.Domain;
using RoomReservation.Api.Dtos;
using RoomReservation.Api.Mapping;
using RoomReservation.Tests.Repositories;

namespace RoomReservation.Tests.Mapping;

public class MappingProfileTests
{
    public static MapperConfiguration NewConfiguration() =>
        new(config => config.AddProfile<MappingProfile>(), NullLoggerFactory.Instance);

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

    [Fact]
    public void Reservation_is_mapped_with_room_name_organizer_name_and_status_text()
    {
        var room = TestDb.Room("Atlas");
        var user = TestDb.User("Ann", "Lee");
        var endedAt = TestDb.Now.AddHours(1);
        var reservation = TestDb.Reservation(room, user, TestDb.Now, TimeSpan.FromHours(2), ReservationStatus.Completed, endedAt);
        reservation.Room = room;
        reservation.User = user;

        var dto = Mapper.Map<ReservationDto>(reservation);

        dto.Should().BeEquivalentTo(new ReservationDto(
            reservation.Id, room.Id, "Atlas", user.Id, "Ann Lee", "Meeting",
            reservation.Start, reservation.End, endedAt, 2, "Completed"));
    }

    [Fact]
    public void Reservation_query_is_mapped_to_a_filter()
    {
        var roomId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var from = TestDb.Now;
        var to = TestDb.Now.AddDays(1);

        var filter = Mapper.Map<ReservationFilter>(new ReservationListQuery
        {
            RoomId = roomId, UserId = userId, Status = "cancelled", From = from, To = to
        });

        filter.RoomId.Should().Be(roomId);
        filter.UserId.Should().Be(userId);
        filter.Status.Should().Be(ReservationStatus.Cancelled);
        filter.From.Should().Be(from);
        filter.To.Should().Be(to);
    }

    [Fact]
    public void Reservation_query_without_a_status_gives_no_status_filter()
    {
        Mapper.Map<ReservationFilter>(new ReservationListQuery()).Status.Should().BeNull();
    }

    [Fact]
    public void Usage_report_query_is_mapped_to_a_filter()
    {
        var from = TestDb.Now;
        var to = TestDb.Now.AddDays(7);

        var filter = Mapper.Map<UsageReportFilter>(new UsageReportQuery { From = from, To = to });

        filter.From.Should().Be(from);
        filter.To.Should().Be(to);
    }

    [Fact]
    public void Usage_report_query_without_a_window_gives_an_open_filter()
    {
        var filter = Mapper.Map<UsageReportFilter>(new UsageReportQuery());

        filter.From.Should().BeNull();
        filter.To.Should().BeNull();
    }

    [Fact]
    public void User_is_mapped_with_the_active_count_from_the_context_item()
    {
        var user = TestDb.User("Ann", "Lee");

        var dto = Mapper.Map<UserDto>(user, options => options.Items[MappingProfile.ActiveCountItem] = 3);

        dto.Should().Be(new UserDto(user.Id, "Ann", "Lee", 3));
    }

    [Fact]
    public void User_without_a_context_item_gets_a_zero_count()
    {
        var user = TestDb.User("Ann", "Lee");

        Mapper.Map<UserDto>(user).ActiveReservationCount.Should().Be(0);
    }
}
