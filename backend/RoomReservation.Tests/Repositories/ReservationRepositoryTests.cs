using FluentAssertions;
using RoomReservation.Api.Data;
using RoomReservation.Api.Domain;
using RoomReservation.Api.Repositories;

namespace RoomReservation.Tests.Repositories;

public class ReservationRepositoryTests
{
    private static readonly DateTimeOffset T = TestDb.Now.AddDays(1); // a day from "now", 10:00
    private static readonly TimeSpan OneHour = TimeSpan.FromHours(1);

    private sealed class Fixture
    {
        public string DbName { get; } = TestDb.NewName();
        public AppDbContext Db { get; }
        public ReservationRepository Repo { get; }
        public Room Room { get; } = TestDb.Room("Atlas");
        public Room OtherRoom { get; } = TestDb.Room("Boardroom");
        public User Ann { get; } = TestDb.User("Ann", "Lee");
        public User Bob { get; } = TestDb.User("Bob", "Ray");

        public Fixture()
        {
            Db = TestDb.Create(DbName);
            Db.Rooms.AddRange(Room, OtherRoom);
            Db.Users.AddRange(Ann, Bob);
            Db.SaveChanges();
            Repo = new ReservationRepository(Db);
        }

        /// <summary>Status as stored, read through a fresh context so only saved changes are visible.</summary>
        public async Task<ReservationStatus> SavedStatusAsync(Guid id)
        {
            await using var other = TestDb.Create(DbName);
            return (await other.Reservations.FindAsync(id))!.Status;
        }

        public async Task<Reservation> AddAsync(Room room, User user, DateTimeOffset start, TimeSpan duration,
            ReservationStatus status = ReservationStatus.Booked, DateTimeOffset? endedAt = null)
        {
            var reservation = TestDb.Reservation(room, user, start, duration, status, endedAt);
            await Repo.AddAsync(reservation);
            return reservation;
        }
    }

    [Fact]
    public async Task Room_overlap_detects_a_partial_overlap()
    {
        var f = new Fixture();
        await f.AddAsync(f.Room, f.Ann, T, OneHour);

        (await f.Repo.HasRoomOverlapAsync(f.Room.Id, T.AddMinutes(30), T.AddMinutes(90), null)).Should().BeTrue();
    }

    [Fact]
    public async Task Room_overlap_allows_back_to_back_meetings()
    {
        var f = new Fixture();
        await f.AddAsync(f.Room, f.Ann, T, OneHour);

        (await f.Repo.HasRoomOverlapAsync(f.Room.Id, T.AddHours(1), T.AddHours(2), null)).Should().BeFalse();
        (await f.Repo.HasRoomOverlapAsync(f.Room.Id, T.AddHours(-1), T, null)).Should().BeFalse();
    }

    [Fact]
    public async Task Room_overlap_ignores_other_rooms()
    {
        var f = new Fixture();
        await f.AddAsync(f.Room, f.Ann, T, OneHour);

        (await f.Repo.HasRoomOverlapAsync(f.OtherRoom.Id, T, T.AddHours(1), null)).Should().BeFalse();
    }

    [Fact]
    public async Task Cancelled_reservations_do_not_block_the_room_or_the_organizer()
    {
        var f = new Fixture();
        await f.AddAsync(f.Room, f.Ann, T, OneHour, ReservationStatus.Cancelled);

        (await f.Repo.HasRoomOverlapAsync(f.Room.Id, T, T.AddHours(1), null)).Should().BeFalse();
        (await f.Repo.HasUserOverlapAsync(f.Ann.Id, T, T.AddHours(1), null)).Should().BeFalse();
    }

    [Fact]
    public async Task Room_overlap_can_exclude_the_reservation_being_changed()
    {
        var f = new Fixture();
        var existing = await f.AddAsync(f.Room, f.Ann, T, OneHour);

        (await f.Repo.HasRoomOverlapAsync(f.Room.Id, T, T.AddHours(1), existing.Id)).Should().BeFalse();
    }

    [Fact]
    public async Task Ending_early_frees_the_room_from_the_ended_at_time()
    {
        var f = new Fixture();
        await f.AddAsync(f.Room, f.Ann, T, TimeSpan.FromHours(2), endedAt: T.AddMinutes(30));

        (await f.Repo.HasRoomOverlapAsync(f.Room.Id, T.AddMinutes(45), T.AddMinutes(75), null)).Should().BeFalse();
        (await f.Repo.HasRoomOverlapAsync(f.Room.Id, T.AddMinutes(15), T.AddMinutes(45), null)).Should().BeTrue();
    }

    [Fact]
    public async Task User_overlap_detects_the_same_organizer_in_a_different_room()
    {
        var f = new Fixture();
        await f.AddAsync(f.Room, f.Ann, T, OneHour);

        (await f.Repo.HasUserOverlapAsync(f.Ann.Id, T.AddMinutes(30), T.AddMinutes(90), null)).Should().BeTrue();
        (await f.Repo.HasUserOverlapAsync(f.Bob.Id, T.AddMinutes(30), T.AddMinutes(90), null)).Should().BeFalse();
    }

    [Fact]
    public async Task Count_active_by_user_counts_only_booked_reservations()
    {
        var f = new Fixture();
        await f.AddAsync(f.Room, f.Ann, T, OneHour);
        await f.AddAsync(f.Room, f.Ann, T.AddHours(2), OneHour);
        await f.AddAsync(f.Room, f.Ann, T.AddHours(4), OneHour, ReservationStatus.Cancelled);
        await f.AddAsync(f.Room, f.Bob, T.AddHours(6), OneHour, ReservationStatus.Completed);

        var counts = await f.Repo.CountActiveByUserAsync([f.Ann.Id, f.Bob.Id]);

        counts.Should().ContainSingle().Which.Should().Be(new KeyValuePair<Guid, int>(f.Ann.Id, 2));
    }

    [Fact]
    public async Task Count_active_by_user_ignores_users_that_were_not_asked_for()
    {
        var f = new Fixture();
        await f.AddAsync(f.Room, f.Ann, T, OneHour);
        await f.AddAsync(f.Room, f.Bob, T.AddHours(2), OneHour);

        (await f.Repo.CountActiveByUserAsync([f.Bob.Id])).Should().ContainSingle().Which.Key.Should().Be(f.Bob.Id);
        (await f.Repo.CountActiveByUserAsync([])).Should().BeEmpty();
    }

    [Fact]
    public async Task Complete_elapsed_marks_only_booked_reservations_that_have_ended()
    {
        var f = new Fixture();
        var past = await f.AddAsync(f.Room, f.Ann, TestDb.Now.AddHours(-3), OneHour);
        var runningNow = await f.AddAsync(f.Room, f.Bob, TestDb.Now.AddMinutes(-30), OneHour);
        var future = await f.AddAsync(f.OtherRoom, f.Ann, T, OneHour);
        var cancelledPast = await f.AddAsync(f.OtherRoom, f.Bob, TestDb.Now.AddHours(-5), OneHour, ReservationStatus.Cancelled);

        await f.Repo.CompleteElapsedAsync(TestDb.Now);

        (await f.SavedStatusAsync(past.Id)).Should().Be(ReservationStatus.Completed);
        (await f.SavedStatusAsync(runningNow.Id)).Should().Be(ReservationStatus.Booked);
        (await f.SavedStatusAsync(future.Id)).Should().Be(ReservationStatus.Booked);
        (await f.SavedStatusAsync(cancelledPast.Id)).Should().Be(ReservationStatus.Cancelled);
    }

    [Fact]
    public async Task Complete_elapsed_treats_an_end_equal_to_now_as_elapsed()
    {
        var f = new Fixture();
        var endsNow = await f.AddAsync(f.Room, f.Ann, TestDb.Now.AddHours(-1), OneHour);

        await f.Repo.CompleteElapsedAsync(TestDb.Now);

        (await f.SavedStatusAsync(endsNow.Id)).Should().Be(ReservationStatus.Completed);
    }

    [Fact]
    public async Task List_filters_by_room_user_and_status_and_orders_newest_first()
    {
        var f = new Fixture();
        var first = await f.AddAsync(f.Room, f.Ann, T, OneHour);
        var second = await f.AddAsync(f.Room, f.Ann, T.AddHours(3), OneHour);
        await f.AddAsync(f.OtherRoom, f.Ann, T.AddHours(6), OneHour);
        await f.AddAsync(f.Room, f.Bob, T.AddHours(9), OneHour);
        await f.AddAsync(f.Room, f.Ann, T.AddHours(12), OneHour, ReservationStatus.Cancelled);

        var list = (await f.Repo.ListPagedAsync(new ReservationFilter
        {
            RoomId = f.Room.Id, UserId = f.Ann.Id, Status = ReservationStatus.Booked
        }, new PageRequest(1, 50))).Items;

        list.Select(r => r.Id).Should().Equal(second.Id, first.Id);
        list.Should().OnlyContain(r => r.Room != null && r.User != null);
    }

    [Fact]
    public async Task List_date_range_keeps_reservations_that_overlap_the_window()
    {
        var f = new Fixture();
        var spanning = await f.AddAsync(f.Room, f.Ann, T, TimeSpan.FromHours(4));
        await f.AddAsync(f.OtherRoom, f.Bob, T.AddDays(5), OneHour);

        var list = (await f.Repo.ListPagedAsync(new ReservationFilter { From = T.AddHours(1), To = T.AddHours(2) }, new PageRequest(1, 50))).Items;

        list.Select(r => r.Id).Should().Equal(spanning.Id);
    }

    [Fact]
    public async Task List_for_report_filters_by_start_and_includes_the_room()
    {
        var f = new Fixture();
        await f.AddAsync(f.Room, f.Ann, T.AddDays(-3), OneHour);
        var inside = await f.AddAsync(f.Room, f.Bob, T, OneHour);
        await f.AddAsync(f.Room, f.Ann, T.AddDays(3), OneHour);

        var list = await f.Repo.ListForReportAsync(new UsageReportFilter { From = T.AddDays(-1), To = T.AddDays(1) });

        list.Should().ContainSingle().Which.Id.Should().Be(inside.Id);
        list[0].Room.Name.Should().Be("Atlas");
    }

    [Fact]
    public async Task User_overlap_can_exclude_the_reservation_being_changed()
    {
        var f = new Fixture();
        var existing = await f.AddAsync(f.Room, f.Ann, T, OneHour);

        (await f.Repo.HasUserOverlapAsync(f.Ann.Id, T, T.AddHours(1), existing.Id)).Should().BeFalse();
        (await f.Repo.HasUserOverlapAsync(f.Ann.Id, T, T.AddHours(1), Guid.NewGuid())).Should().BeTrue();
    }

    [Fact]
    public async Task Overlap_detects_containing_and_contained_intervals()
    {
        var f = new Fixture();
        await f.AddAsync(f.Room, f.Ann, T, TimeSpan.FromHours(4));

        (await f.Repo.HasRoomOverlapAsync(f.Room.Id, T.AddHours(1), T.AddHours(2), null)).Should().BeTrue("contained");
        (await f.Repo.HasRoomOverlapAsync(f.Room.Id, T.AddHours(-1), T.AddHours(6), null)).Should().BeTrue("containing");
        (await f.Repo.HasUserOverlapAsync(f.Ann.Id, T.AddHours(1), T.AddHours(2), null)).Should().BeTrue("contained");
        (await f.Repo.HasUserOverlapAsync(f.Ann.Id, T.AddHours(-1), T.AddHours(6), null)).Should().BeTrue("containing");
    }

    [Fact]
    public async Task A_new_meeting_may_start_exactly_when_an_early_end_happened()
    {
        var f = new Fixture();
        await f.AddAsync(f.Room, f.Ann, T, TimeSpan.FromHours(2), ReservationStatus.Completed, endedAt: T.AddMinutes(30));

        (await f.Repo.HasRoomOverlapAsync(f.Room.Id, T.AddMinutes(30), T.AddMinutes(60), null)).Should().BeFalse();
        (await f.Repo.HasUserOverlapAsync(f.Ann.Id, T.AddMinutes(30), T.AddMinutes(60), null)).Should().BeFalse();
    }

    [Fact]
    public async Task Completed_reservations_still_block_the_room_and_the_organizer()
    {
        var f = new Fixture();
        await f.AddAsync(f.Room, f.Ann, T, OneHour, ReservationStatus.Completed);

        (await f.Repo.HasRoomOverlapAsync(f.Room.Id, T, T.AddHours(1), null)).Should().BeTrue();
        (await f.Repo.HasUserOverlapAsync(f.Ann.Id, T, T.AddHours(1), null)).Should().BeTrue();
    }

    [Fact]
    public async Task List_filters_by_status_alone()
    {
        var f = new Fixture();
        await f.AddAsync(f.Room, f.Ann, T, OneHour);
        var cancelled = await f.AddAsync(f.OtherRoom, f.Bob, T, OneHour, ReservationStatus.Cancelled);

        var list = (await f.Repo.ListPagedAsync(new ReservationFilter { Status = ReservationStatus.Cancelled }, new PageRequest(1, 50))).Items;

        list.Select(r => r.Id).Should().Equal(cancelled.Id);
    }

    [Fact]
    public async Task List_date_range_is_half_open_at_both_ends()
    {
        var f = new Fixture();
        var endsAtFrom = await f.AddAsync(f.Room, f.Ann, T.AddHours(-1), OneHour);      // ends exactly at From
        var startsAtTo = await f.AddAsync(f.OtherRoom, f.Bob, T.AddHours(2), OneHour);   // starts exactly at To
        var inside = await f.AddAsync(f.Room, f.Bob, T.AddMinutes(30), OneHour);

        var list = (await f.Repo.ListPagedAsync(new ReservationFilter { From = T, To = T.AddHours(2) }, new PageRequest(1, 50))).Items;

        list.Select(r => r.Id).Should().Equal(inside.Id);
        list.Select(r => r.Id).Should().NotContain([endsAtFrom.Id, startsAtTo.Id]);
    }

    [Fact]
    public async Task List_date_range_uses_the_early_end_time()
    {
        var f = new Fixture();
        // Planned for 4 hours but ended 30 minutes in: it no longer overlaps a window that starts an hour after T.
        await f.AddAsync(f.Room, f.Ann, T, TimeSpan.FromHours(4), ReservationStatus.Completed, endedAt: T.AddMinutes(30));

        var list = (await f.Repo.ListPagedAsync(new ReservationFilter { From = T.AddHours(1), To = T.AddHours(2) }, new PageRequest(1, 50))).Items;

        list.Should().BeEmpty();
    }

    [Fact]
    public async Task List_for_report_bounds_are_inclusive()
    {
        var f = new Fixture();
        var atFrom = await f.AddAsync(f.Room, f.Ann, T, OneHour);
        var atTo = await f.AddAsync(f.OtherRoom, f.Bob, T.AddDays(1), OneHour);
        await f.AddAsync(f.Room, f.Bob, T.AddDays(1).AddMinutes(1), OneHour);
        await f.AddAsync(f.OtherRoom, f.Ann, T.AddMinutes(-1), OneHour);

        var list = await f.Repo.ListForReportAsync(new UsageReportFilter { From = T, To = T.AddDays(1) });

        list.Select(r => r.Id).Should().BeEquivalentTo([atFrom.Id, atTo.Id]);
    }

    [Fact]
    public async Task Get_with_details_loads_room_and_user()
    {
        var f = new Fixture();
        var reservation = await f.AddAsync(f.Room, f.Ann, T, OneHour);

        var loaded = await f.Repo.GetWithDetailsAsync(reservation.Id);

        loaded!.Room.Name.Should().Be("Atlas");
        loaded.User.LastName.Should().Be("Lee");
        (await f.Repo.GetWithDetailsAsync(Guid.NewGuid())).Should().BeNull();
    }
}
