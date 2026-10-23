using FluentAssertions;
using RoomReservation.Api.Domain;
using RoomReservation.Api.Repositories;

namespace RoomReservation.Tests.Repositories;

public class RoomRepositoryTests
{
    private static async Task<RoomRepository> RepoWithRoomsAsync()
    {
        var db = TestDb.Create();
        db.Rooms.AddRange(
            TestDb.Room("Small", capacity: 4, floor: 1, Equipment.Whiteboard),
            TestDb.Room("Medium", capacity: 10, floor: 2, Equipment.TvScreen | Equipment.VideoConferencing),
            TestDb.Room("Large", capacity: 30, floor: 2, Equipment.Projector | Equipment.TvScreen | Equipment.VideoConferencing),
            TestDb.Room("Bare", capacity: 8, floor: 0, Equipment.None, isActive: false));
        await db.SaveChangesAsync();
        return new RoomRepository(db);
    }

    [Fact]
    public async Task Empty_filter_returns_all_rooms_ordered_by_floor_then_name()
    {
        var repo = await RepoWithRoomsAsync();

        var rooms = await repo.ListAsync(new RoomFilter());

        rooms.Select(r => r.Name).Should().Equal("Bare", "Small", "Large", "Medium");
    }

    [Fact]
    public async Task Min_capacity_is_inclusive()
    {
        var repo = await RepoWithRoomsAsync();

        var rooms = await repo.ListAsync(new RoomFilter { MinCapacity = 10 });

        rooms.Select(r => r.Name).Should().BeEquivalentTo("Medium", "Large");
    }

    [Fact]
    public async Task Floor_zero_is_a_real_filter_not_ignored()
    {
        var repo = await RepoWithRoomsAsync();

        var rooms = await repo.ListAsync(new RoomFilter { Floor = 0 });

        rooms.Select(r => r.Name).Should().Equal("Bare");
    }

    [Fact]
    public async Task Floor_filter_matches_exactly()
    {
        var repo = await RepoWithRoomsAsync();

        var rooms = await repo.ListAsync(new RoomFilter { Floor = 2 });

        rooms.Select(r => r.Name).Should().BeEquivalentTo("Medium", "Large");
    }

    [Fact]
    public async Task Equipment_filter_requires_every_requested_flag()
    {
        var repo = await RepoWithRoomsAsync();

        var rooms = await repo.ListAsync(new RoomFilter { Equipment = Equipment.TvScreen | Equipment.Projector });

        rooms.Select(r => r.Name).Should().Equal("Large");
    }

    [Fact]
    public async Task Equipment_none_does_not_filter()
    {
        var repo = await RepoWithRoomsAsync();

        var rooms = await repo.ListAsync(new RoomFilter { Equipment = Equipment.None });

        rooms.Should().HaveCount(4);
    }

    [Fact]
    public async Task Filters_are_combined_with_and()
    {
        var repo = await RepoWithRoomsAsync();

        var rooms = await repo.ListAsync(new RoomFilter { MinCapacity = 5, Floor = 2, Equipment = Equipment.Projector });

        rooms.Select(r => r.Name).Should().Equal("Large");
    }

    [Fact]
    public async Task Inactive_rooms_are_still_listed()
    {
        var repo = await RepoWithRoomsAsync();

        var rooms = await repo.ListAsync(new RoomFilter());

        rooms.Should().Contain(r => !r.IsActive);
    }

    [Fact]
    public async Task Add_get_and_update_round_trip()
    {
        var name = TestDb.NewName();
        var repo = new RoomRepository(TestDb.Create(name));
        var room = TestDb.Room("Round trip", capacity: 5);

        await repo.AddAsync(room);
        room.Capacity = 9;
        await repo.UpdateAsync(room);

        // A second context on the same database only sees what was actually saved.
        var loaded = await new RoomRepository(TestDb.Create(name)).GetByIdAsync(room.Id);

        loaded.Should().NotBeNull();
        loaded!.Capacity.Should().Be(9);
        (await repo.GetByIdAsync(Guid.NewGuid())).Should().BeNull();
    }
}
