using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Time.Testing;
using RoomReservation.Api.Domain;
using RoomReservation.Api.Repositories;
using RoomReservation.Api.Services;
using RoomReservation.Tests.Repositories;

namespace RoomReservation.Tests.Services;

public class ReservationLifecycleTests
{
    private static readonly TimeSpan Hour = TimeSpan.FromHours(1);

    [Fact]
    public async Task Elapsed_booked_reservations_become_completed_and_the_rest_is_untouched()
    {
        var name = TestDb.NewName();
        var db = TestDb.Create(name);
        var room = TestDb.Room();
        var user = TestDb.User();
        db.Rooms.Add(room);
        db.Users.Add(user);
        var elapsed = TestDb.Reservation(room, user, TestDb.Now - 3 * Hour, Hour);
        var running = TestDb.Reservation(room, user, TestDb.Now - Hour, 2 * Hour);
        var future = TestDb.Reservation(room, user, TestDb.Now + Hour, Hour);
        var cancelled = TestDb.Reservation(room, user, TestDb.Now - 5 * Hour, Hour, ReservationStatus.Cancelled);
        db.Reservations.AddRange(elapsed, running, future, cancelled);
        await db.SaveChangesAsync();
        var lifecycle = new ReservationLifecycle(new ReservationRepository(db), new FakeTimeProvider(TestDb.Now));

        await lifecycle.CompleteElapsedAsync();

        using var check = TestDb.Create(name);
        var statuses = await check.Reservations.ToDictionaryAsync(r => r.Id, r => r.Status);
        statuses[elapsed.Id].Should().Be(ReservationStatus.Completed);
        statuses[running.Id].Should().Be(ReservationStatus.Booked);
        statuses[future.Id].Should().Be(ReservationStatus.Booked);
        statuses[cancelled.Id].Should().Be(ReservationStatus.Cancelled);
    }

    [Fact]
    public async Task Running_it_twice_changes_nothing_more()
    {
        var name = TestDb.NewName();
        var db = TestDb.Create(name);
        var room = TestDb.Room();
        var user = TestDb.User();
        db.Rooms.Add(room);
        db.Users.Add(user);
        var elapsed = TestDb.Reservation(room, user, TestDb.Now - 3 * Hour, Hour);
        db.Reservations.Add(elapsed);
        await db.SaveChangesAsync();
        var lifecycle = new ReservationLifecycle(new ReservationRepository(db), new FakeTimeProvider(TestDb.Now));

        await lifecycle.CompleteElapsedAsync();
        await lifecycle.CompleteElapsedAsync();

        using var check = TestDb.Create(name);
        (await check.Reservations.SingleAsync()).Status.Should().Be(ReservationStatus.Completed);
    }

    [Fact]
    public async Task A_reservation_whose_end_is_exactly_now_is_completed()
    {
        var name = TestDb.NewName();
        var db = TestDb.Create(name);
        var room = TestDb.Room();
        var user = TestDb.User();
        db.Rooms.Add(room);
        db.Users.Add(user);
        db.Reservations.Add(TestDb.Reservation(room, user, TestDb.Now - Hour, Hour));
        await db.SaveChangesAsync();
        var lifecycle = new ReservationLifecycle(new ReservationRepository(db), new FakeTimeProvider(TestDb.Now));

        await lifecycle.CompleteElapsedAsync();

        using var check = TestDb.Create(name);
        (await check.Reservations.SingleAsync()).Status.Should().Be(ReservationStatus.Completed);
    }
}
