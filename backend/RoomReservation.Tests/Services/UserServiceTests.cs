using FluentAssertions;
using Microsoft.Extensions.Time.Testing;
using RoomReservation.Api.Common;
using RoomReservation.Api.Data;
using RoomReservation.Api.Domain;
using RoomReservation.Api.Repositories;
using RoomReservation.Api.Services;
using RoomReservation.Tests.Mapping;
using RoomReservation.Tests.Repositories;

namespace RoomReservation.Tests.Services;

public class UserServiceTests
{
    private static readonly TimeSpan Hour = TimeSpan.FromHours(1);

    private readonly AppDbContext _db = TestDb.Create();
    private readonly FakeTimeProvider _clock = new(TestDb.Now);
    private readonly Room _room = TestDb.Room("Atlas", capacity: 10);

    public UserServiceTests() => _db.Rooms.Add(_room);

    private UserService Service()
    {
        var reservations = new ReservationRepository(_db);
        return new UserService(
            new UserRepository(_db), reservations, new ReservationLifecycle(reservations, _clock),
            MappingProfileTests.NewConfiguration().CreateMapper());
    }

    private User AddUser(string first = "Ann", string last = "Lee")
    {
        var user = TestDb.User(first, last);
        _db.Users.Add(user);
        return user;
    }

    private void Book(User user, TimeSpan startsIn, ReservationStatus status = ReservationStatus.Booked, TimeSpan? duration = null)
    {
        // Each reservation gets its own start so the data stays realistic; the service does not check overlaps.
        var endedAt = status == ReservationStatus.Completed ? TestDb.Now + startsIn + Hour : (DateTimeOffset?)null;
        _db.Reservations.Add(TestDb.Reservation(_room, user, TestDb.Now + startsIn, duration ?? Hour, status, endedAt));
    }

    private async Task<IReadOnlyList<RoomReservation.Api.Dtos.UserDto>> ListAsync()
    {
        await _db.SaveChangesAsync();
        var result = await Service().ListAsync();
        result.Status.Should().Be(ResultStatus.Ok);
        return result.Value!;
    }

    [Fact]
    public async Task A_user_without_reservations_is_listed_with_zero()
    {
        AddUser();

        var users = await ListAsync();

        users.Should().ContainSingle().Which.ActiveReservationCount.Should().Be(0);
    }

    [Fact]
    public async Task Only_booked_reservations_are_counted()
    {
        var ann = AddUser();
        Book(ann, Hour);
        Book(ann, Hour * 3);
        Book(ann, Hour * 5, ReservationStatus.Cancelled);
        Book(ann, -Hour * 10, ReservationStatus.Completed);

        var users = await ListAsync();

        users.Single().ActiveReservationCount.Should().Be(2);
    }

    [Fact]
    public async Task Counts_are_kept_per_organizer()
    {
        var ann = AddUser("Ann", "Lee");
        var bob = AddUser("Bob", "Kim");
        Book(ann, Hour);
        Book(ann, Hour * 3);
        Book(bob, Hour * 5);

        var users = await ListAsync();

        users.Single(u => u.Id == ann.Id).ActiveReservationCount.Should().Be(2);
        users.Single(u => u.Id == bob.Id).ActiveReservationCount.Should().Be(1);
    }

    [Fact]
    public async Task A_booking_stops_counting_once_the_clock_passes_its_end()
    {
        var ann = AddUser();
        Book(ann, Hour, duration: Hour);

        (await ListAsync()).Single().ActiveReservationCount.Should().Be(1);

        _clock.Advance(Hour * 3);

        (await ListAsync()).Single().ActiveReservationCount.Should().Be(0);
    }

    [Fact]
    public async Task A_meeting_that_has_started_but_not_ended_still_counts()
    {
        var ann = AddUser();
        Book(ann, -Hour, duration: Hour * 3);

        (await ListAsync()).Single().ActiveReservationCount.Should().Be(1);
    }

    [Fact]
    public async Task Users_are_ordered_by_last_name_then_first_name()
    {
        AddUser("Zoe", "Adams");
        AddUser("Anna", "Brown");
        AddUser("Ben", "Brown");

        var users = await ListAsync();

        users.Select(u => $"{u.FirstName} {u.LastName}").Should().Equal("Zoe Adams", "Anna Brown", "Ben Brown");
    }

    [Fact]
    public async Task No_users_gives_an_empty_list()
    {
        (await ListAsync()).Should().BeEmpty();
    }
}
