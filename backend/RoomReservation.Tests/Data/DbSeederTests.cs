using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Time.Testing;
using RoomReservation.Api.Configuration;
using RoomReservation.Api.Data;
using RoomReservation.Api.Domain;
using RoomReservation.Tests.Repositories;

namespace RoomReservation.Tests.Data;

public class DbSeederTests
{
    // The limits come from the API's own appsettings.json, so the seeder is checked against the real configuration.
    private static readonly BookingRulesOptions Rules = LoadRules();

    private static BookingRulesOptions LoadRules()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
        var options = new BookingRulesOptions();
        new ConfigurationBuilder()
            .AddJsonFile(path, optional: false)
            .Build()
            .GetSection(BookingRulesOptions.SectionName)
            .Bind(options);
        return options;
    }

    private static FakeTimeProvider Clock() => new(TestDb.Now);

    private static async Task<AppDbContext> SeededAsync(DateTimeOffset? now = null)
    {
        var db = TestDb.Create();
        await DbSeeder.SeedAsync(db, now is { } at ? new FakeTimeProvider(at) : Clock());
        return db;
    }

    private static bool Overlaps(Reservation a, Reservation b) =>
        a.Start < (b.EndedAt ?? b.End) && b.Start < (a.EndedAt ?? a.End);

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
    public async Task Seeds_every_reservation_status_the_demo_needs()
    {
        var db = await SeededAsync();
        var all = await db.Reservations.ToListAsync();

        all.Should().Contain(r => r.Status == ReservationStatus.Completed && r.EndedAt == null);
        all.Should().Contain(r => r.Status == ReservationStatus.Completed && r.EndedAt != null);
        all.Should().Contain(r => r.Status == ReservationStatus.Cancelled);
        all.Should().Contain(r => r.Status == ReservationStatus.Booked && r.Start.Date == TestDb.Now.Date);
        all.Should().Contain(r => r.Status == ReservationStatus.Booked && r.Start.Date > TestDb.Now.Date);
    }

    [Fact]
    public async Task Ended_early_reservations_end_inside_their_planned_slot()
    {
        var db = await SeededAsync();

        var early = await db.Reservations.Where(r => r.EndedAt != null).ToListAsync();

        early.Should().NotBeEmpty();
        early.Should().OnlyContain(r => r.EndedAt > r.Start && r.EndedAt < r.End);
    }

    [Fact]
    public async Task Seeded_reservations_never_exceed_room_capacity_or_the_duration_limit()
    {
        var db = await SeededAsync();

        var reservations = await db.Reservations.Include(r => r.Room).ToListAsync();

        reservations.Should().OnlyContain(r => r.AttendeeCount <= r.Room.Capacity);
        reservations.Should().OnlyContain(r => r.End - r.Start <= TimeSpan.FromDays(Rules.MaxDurationDays));
    }

    [Fact]
    public async Task Seeded_reservations_do_not_overlap_in_a_room_or_for_an_organizer()
    {
        var db = await SeededAsync();

        AssertNoOverlaps(await db.Reservations.ToListAsync());
    }

    [Fact]
    public async Task Booked_reservations_are_in_the_future_and_within_the_horizon()
    {
        var db = await SeededAsync();

        var booked = await db.Reservations.Where(r => r.Status == ReservationStatus.Booked).ToListAsync();

        booked.Should().NotBeEmpty();
        booked.Should().OnlyContain(r => r.Start > TestDb.Now && r.Start <= TestDb.Now.AddDays(Rules.MaxHorizonDays));
    }

    [Fact]
    public async Task Past_reservations_are_not_left_booked()
    {
        var db = await SeededAsync();

        (await db.Reservations.CountAsync(r => r.Status == ReservationStatus.Booked && r.End <= TestDb.Now))
            .Should().Be(0);
    }

    // The "later today" reservation depends on the clock, so check the invariants across the day, including midnight.
    [Theory]
    [InlineData(0, 0)]
    [InlineData(10, 0)]
    [InlineData(22, 59)]
    [InlineData(23, 30)]
    [InlineData(23, 59)]
    public async Task Seed_invariants_hold_at_any_time_of_day(int hour, int minute)
    {
        var now = new DateTimeOffset(2026, 6, 1, hour, minute, 0, TimeSpan.Zero);
        var db = await SeededAsync(now);
        var all = await db.Reservations.Include(r => r.Room).ToListAsync();

        AssertNoOverlaps(all);
        all.Should().OnlyContain(r => r.AttendeeCount <= r.Room.Capacity);
        all.Should().OnlyContain(r => r.End - r.Start <= TimeSpan.FromDays(Rules.MaxDurationDays));
        var booked = all.Where(r => r.Status == ReservationStatus.Booked).ToList();
        booked.Should().NotBeEmpty();
        booked.Should().OnlyContain(r => r.Start > now && r.Start <= now.AddDays(Rules.MaxHorizonDays));
        booked.Should().OnlyContain(r => r.Room.IsActive);
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

    private static void AssertNoOverlaps(IEnumerable<Reservation> reservations)
    {
        var live = reservations.Where(r => r.Status != ReservationStatus.Cancelled).ToList();

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
}
