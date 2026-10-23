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
        public AppDbContext Db { get; } = TestDb.Create();
        public ReservationRepository Repo { get; }
        public Room Room { get; } = TestDb.Room("Atlas");
        public Room OtherRoom { get; } = TestDb.Room("Boardroom");
        public User Ann { get; } = TestDb.User("Ann", "Lee");
        public User Bob { get; } = TestDb.User("Bob", "Ray");

        public Fixture()
        {
            Db.Rooms.AddRange(Room, OtherRoom);
            Db.Users.AddRange(Ann, Bob);
            Db.SaveChanges();
            Repo = new ReservationRepository(Db);
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

        var counts = await f.Repo.CountActiveByUserAsync();

        counts.Should().ContainSingle().Which.Should().Be(new KeyValuePair<Guid, int>(f.Ann.Id, 2));
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

        (await f.Repo.GetByIdAsync(past.Id))!.Status.Should().Be(ReservationStatus.Completed);
        (await f.Repo.GetByIdAsync(runningNow.Id))!.Status.Should().Be(ReservationStatus.Booked);
        (await f.Repo.GetByIdAsync(future.Id))!.Status.Should().Be(ReservationStatus.Booked);
        (await f.Repo.GetByIdAsync(cancelledPast.Id))!.Status.Should().Be(ReservationStatus.Cancelled);
    }

    [Fact]
    public async Task Complete_elapsed_treats_an_end_equal_to_now_as_elapsed()
    {
        var f = new Fixture();
        var endsNow = await f.AddAsync(f.Room, f.Ann, TestDb.Now.AddHours(-1), OneHour);

        await f.Repo.CompleteElapsedAsync(TestDb.Now);

        (await f.Repo.GetByIdAsync(endsNow.Id))!.Status.Should().Be(ReservationStatus.Completed);
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

        var list = await f.Repo.ListAsync(new ReservationFilter
        {
            RoomId = f.Room.Id, UserId = f.Ann.Id, Status = ReservationStatus.Booked
        });

        list.Select(r => r.Id).Should().Equal(second.Id, first.Id);
        list.Should().OnlyContain(r => r.Room != null && r.User != null);
    }

    [Fact]
    public async Task List_date_range_keeps_reservations_that_overlap_the_window()
    {
        var f = new Fixture();
        var spanning = await f.AddAsync(f.Room, f.Ann, T, TimeSpan.FromHours(4));
        await f.AddAsync(f.OtherRoom, f.Bob, T.AddDays(5), OneHour);

        var list = await f.Repo.ListAsync(new ReservationFilter { From = T.AddHours(1), To = T.AddHours(2) });

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
