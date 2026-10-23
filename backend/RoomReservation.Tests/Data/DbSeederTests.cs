using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Time.Testing;
using RoomReservation.Api.Data;
using RoomReservation.Api.Domain;
using RoomReservation.Tests.Repositories;

namespace RoomReservation.Tests.Data;

public class DbSeederTests
{
    private static FakeTimeProvider Clock() => new(TestDb.Now);

    private static async Task<AppDbContext> SeededAsync()
    {
        var db = TestDb.Create();
        await DbSeeder.SeedAsync(db, Clock());
        return db;
    }

    [Fact]
    public async Task Seeds_rooms_users_and_reservations()
    {
        var db = await SeededAsync();

        (await db.Rooms.CountAsync()).Should().Be(8);
        (await db.Users.CountAsync()).Should().Be(6);
        (await db.Reservations.CountAsync()).Should().BeGreaterThan(20);
    }

    [Fact]
    public async Task Seeding_twice_does_not_duplicate_data()
    {
        var db = await SeededAsync();
        var rooms = await db.Rooms.CountAsync();
        var reservations = await db.Reservations.CountAsync();

        await DbSeeder.SeedAsync(db, Clock());

        (await db.Rooms.CountAsync()).Should().Be(rooms);
        (await db.Reservations.CountAsync()).Should().Be(reservations);
    }

    [Fact]
    public async Task Seeds_one_inactive_room_and_covers_every_equipment_type()
    {
        var db = await SeededAsync();
        var rooms = await db.Rooms.ToListAsync();

        rooms.Where(r => !r.IsActive).Select(r => r.Name).Should().Equal("Summit");
        var combined = EquipmentFlags.Combine(rooms.Select(r => r.Equipment));
        combined.Should().Be(Equipment.Projector | Equipment.TvScreen | Equipment.VideoConferencing | Equipment.Whiteboard);
    }

    [Fact]
    public async Task Seeded_reservations_never_exceed_room_capacity_or_the_week_limit()
    {
        var db = await SeededAsync();

        var reservations = await db.Reservations.Include(r => r.Room).ToListAsync();

        reservations.Should().OnlyContain(r => r.AttendeeCount <= r.Room.Capacity);
        reservations.Should().OnlyContain(r => r.End - r.Start <= TimeSpan.FromDays(7));
    }

    [Fact]
    public async Task Seeded_reservations_do_not_overlap_in_a_room_or_for_an_organizer()
    {
        var db = await SeededAsync();
        var live = (await db.Reservations.ToListAsync()).Where(r => r.Status != ReservationStatus.Cancelled).ToList();

        static bool Overlaps(Reservation a, Reservation b) => a.Start < (b.EndedAt ?? b.End) && b.Start < (a.EndedAt ?? a.End);

        foreach (var group in live.GroupBy(r => r.RoomId).Concat(live.GroupBy(r => r.UserId)))
        {
            var items = group.ToList();
            for (var i = 0; i < items.Count; i++)
            {
                for (var j = i + 1; j < items.Count; j++)
                {
                    Overlaps(items[i], items[j]).Should().BeFalse($"{items[i].Title} / {items[j].Title}");
                }
            }
        }
    }

    [Fact]
    public async Task Booked_reservations_are_in_the_future_and_within_the_horizon()
    {
        var db = await SeededAsync();

        var booked = await db.Reservations.Where(r => r.Status == ReservationStatus.Booked).ToListAsync();

        booked.Should().NotBeEmpty();
        booked.Should().OnlyContain(r => r.Start > TestDb.Now && r.Start <= TestDb.Now.AddDays(90));
    }

    [Fact]
    public async Task Past_reservations_are_not_left_booked()
    {
        var db = await SeededAsync();

        (await db.Reservations.CountAsync(r => r.Status == ReservationStatus.Booked && r.End <= TestDb.Now))
            .Should().Be(0);
    }

    [Fact]
    public async Task Boardroom_is_booked_often_but_rarely_used()
    {
        var db = await SeededAsync();
        var boardroom = await db.Reservations.Where(r => r.Room.Name == "Boardroom" && r.Start < TestDb.Now).ToListAsync();

        boardroom.Should().HaveCountGreaterThanOrEqualTo(4);
        var plannedMinutes = boardroom.Sum(r => (r.End - r.Start).TotalMinutes);
        var usedMinutes = boardroom
            .Where(r => r.Status == ReservationStatus.Completed)
            .Sum(r => ((r.EndedAt ?? r.End) - r.Start).TotalMinutes);
        usedMinutes.Should().BeLessThan(plannedMinutes * 0.5);
    }
}
