using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using RoomReservation.Api.Common;
using RoomReservation.Api.Configuration;
using RoomReservation.Api.Data;
using RoomReservation.Api.Domain;
using RoomReservation.Api.Dtos;
using RoomReservation.Api.Repositories;
using RoomReservation.Api.Services;
using RoomReservation.Tests.Mapping;
using RoomReservation.Tests.Repositories;

namespace RoomReservation.Tests.Services;

public class UsageReportServiceTests
{
    private static readonly TimeSpan Hour = TimeSpan.FromHours(1);

    private readonly AppDbContext _db = TestDb.Create();
    private readonly FakeTimeProvider _clock = new(TestDb.Now);
    private readonly Room _room = TestDb.Room("Atlas");
    private readonly User _user = TestDb.User();

    public UsageReportServiceTests()
    {
        _db.Rooms.Add(_room);
        _db.Users.Add(_user);
    }

    private UsageReportService Service(int minBookings = 4, int thresholdPercent = 50)
    {
        var reservations = new ReservationRepository(_db);
        return new UsageReportService(
            new RoomRepository(_db), reservations, new ReservationLifecycle(reservations, _clock),
            Options.Create(new UsageReportOptions { MinBookingsToFlag = minBookings, UnderusedThresholdPercent = thresholdPercent }),
            _clock, MappingProfileTests.NewConfiguration().CreateMapper());
    }

    /// <summary>Adds a reservation that starts <paramref name="startsIn"/> from now. Overlaps do not matter: the report never checks them.</summary>
    private Reservation Add(Room room, ReservationStatus status, TimeSpan startsIn, TimeSpan duration, TimeSpan? endedAfter = null)
    {
        var start = TestDb.Now + startsIn;
        var reservation = TestDb.Reservation(room, _user, start, duration, status, endedAfter is { } after ? start + after : null);
        _db.Reservations.Add(reservation);
        return reservation;
    }

    private Reservation Add(ReservationStatus status, TimeSpan? duration = null, TimeSpan? endedAfter = null) =>
        Add(_room, status, -Hour * 24, duration ?? Hour, endedAfter);

    private void Add(int count, ReservationStatus status, TimeSpan? duration = null, TimeSpan? endedAfter = null)
    {
        for (var i = 0; i < count; i++)
        {
            Add(status, duration, endedAfter);
        }
    }

    private async Task<UsageReportDto> ReportAsync(UsageReportQuery? query = null, int minBookings = 4, int thresholdPercent = 50)
    {
        await _db.SaveChangesAsync();
        var result = await Service(minBookings, thresholdPercent).GetAsync(query ?? new UsageReportQuery());
        result.Status.Should().Be(ResultStatus.Ok);
        return result.Value!;
    }

    private async Task<RoomUsageDto> AtlasAsync(UsageReportQuery? query = null, int minBookings = 4, int thresholdPercent = 50) =>
        (await ReportAsync(query, minBookings, thresholdPercent)).Rooms.Single(r => r.RoomId == _room.Id);

    // ----- the flag -----

    [Fact]
    public async Task A_room_with_exactly_the_minimum_resolved_bookings_and_low_use_is_flagged()
    {
        Add(4, ReservationStatus.Cancelled);

        var row = await AtlasAsync();

        row.IsOftenBookedRarelyUsed.Should().BeTrue();
        row.UtilizationPercent.Should().Be(0m);
    }

    [Fact]
    public async Task A_room_with_one_booking_too_few_is_not_flagged()
    {
        Add(3, ReservationStatus.Cancelled);

        (await AtlasAsync()).IsOftenBookedRarelyUsed.Should().BeFalse();
    }

    [Fact]
    public async Task A_room_at_exactly_the_threshold_is_not_flagged()
    {
        Add(2, ReservationStatus.Completed);
        Add(2, ReservationStatus.Cancelled);

        var row = await AtlasAsync();

        row.UtilizationPercent.Should().Be(50m);
        row.IsOftenBookedRarelyUsed.Should().BeFalse();
    }

    [Fact]
    public async Task A_threshold_met_exactly_with_durations_that_are_not_exact_in_decimal_hours_is_not_flagged()
    {
        var forty = TimeSpan.FromMinutes(40);
        Add(2, ReservationStatus.Completed, forty);
        Add(2, ReservationStatus.Cancelled, forty);

        var row = await AtlasAsync();

        row.UtilizationPercent.Should().Be(50m);
        row.IsOftenBookedRarelyUsed.Should().BeFalse();
    }

    [Fact]
    public async Task A_room_below_the_threshold_is_flagged()
    {
        Add(1, ReservationStatus.Completed);
        Add(3, ReservationStatus.Cancelled);

        var row = await AtlasAsync();

        row.UtilizationPercent.Should().Be(25m);
        row.IsOftenBookedRarelyUsed.Should().BeTrue();
    }

    [Fact]
    public async Task A_room_above_the_threshold_is_not_flagged()
    {
        Add(3, ReservationStatus.Completed);
        Add(1, ReservationStatus.Cancelled);

        var row = await AtlasAsync();

        row.UtilizationPercent.Should().Be(75m);
        row.IsOftenBookedRarelyUsed.Should().BeFalse();
    }

    [Fact]
    public async Task The_flag_uses_the_unrounded_percentage()
    {
        // Four 1 h meetings; used = 3594 s + 1 h + 0 + 0 = 1.99833 h of 4 h = 49.958 %, shown as 50.0 but still below 50.
        Add(ReservationStatus.Completed, endedAfter: TimeSpan.FromSeconds(3594));
        Add(ReservationStatus.Completed);
        Add(2, ReservationStatus.Completed, endedAfter: TimeSpan.Zero);

        var row = await AtlasAsync();

        row.UtilizationPercent.Should().Be(50.0m);
        row.IsOftenBookedRarelyUsed.Should().BeTrue();
    }

    [Fact]
    public async Task Thresholds_are_read_from_the_options()
    {
        Add(2, ReservationStatus.Cancelled);

        (await AtlasAsync(minBookings: 4)).IsOftenBookedRarelyUsed.Should().BeFalse();
        (await AtlasAsync(minBookings: 2)).IsOftenBookedRarelyUsed.Should().BeTrue();
    }

    [Fact]
    public async Task The_percentage_threshold_is_read_from_the_options()
    {
        Add(2, ReservationStatus.Completed);
        Add(2, ReservationStatus.Cancelled);

        (await AtlasAsync(thresholdPercent: 50)).IsOftenBookedRarelyUsed.Should().BeFalse();
        (await AtlasAsync(thresholdPercent: 51)).IsOftenBookedRarelyUsed.Should().BeTrue();
    }

    [Fact]
    public async Task The_report_echoes_the_thresholds_it_used()
    {
        var report = await ReportAsync(minBookings: 6, thresholdPercent: 30);

        report.MinBookingsToFlag.Should().Be(6);
        report.UnderusedThresholdPercent.Should().Be(30);
    }

    // ----- hours -----

    [Fact]
    public async Task A_cancelled_booking_counts_as_planned_but_unused()
    {
        Add(ReservationStatus.Completed);
        Add(ReservationStatus.Cancelled, duration: Hour * 3);

        var row = await AtlasAsync();

        row.PlannedHours.Should().Be(4m);
        row.UsedHours.Should().Be(1m);
        row.UtilizationPercent.Should().Be(25m);
    }

    [Fact]
    public async Task A_meeting_ended_early_lowers_utilization()
    {
        Add(ReservationStatus.Completed, duration: Hour * 2, endedAfter: TimeSpan.FromMinutes(30));

        var row = await AtlasAsync();

        row.PlannedHours.Should().Be(2m);
        row.UsedHours.Should().Be(0.5m);
        row.UtilizationPercent.Should().Be(25m);
    }

    [Fact]
    public async Task A_completed_meeting_without_an_end_time_counts_fully()
    {
        Add(ReservationStatus.Completed, duration: Hour * 2);

        var row = await AtlasAsync();

        row.UsedHours.Should().Be(2m);
        row.UtilizationPercent.Should().Be(100m);
    }

    [Fact]
    public async Task An_end_time_before_the_start_is_clamped_to_zero_used_hours()
    {
        Add(ReservationStatus.Completed, duration: Hour * 2, endedAfter: -Hour);

        var row = await AtlasAsync();

        row.UsedHours.Should().Be(0m);
        row.UtilizationPercent.Should().Be(0m);
    }

    [Fact]
    public async Task An_end_time_after_the_planned_end_is_clamped_to_the_planned_hours()
    {
        Add(ReservationStatus.Completed, duration: Hour, endedAfter: Hour * 5);

        (await AtlasAsync()).UsedHours.Should().Be(1m);
    }

    [Fact]
    public async Task Hours_are_rounded_to_two_decimals_and_the_percentage_to_one()
    {
        // 20 minutes of a 1 h meeting: 0.3333 h, 33.333 %.
        Add(ReservationStatus.Completed, endedAfter: TimeSpan.FromMinutes(20));

        var row = await AtlasAsync();

        row.UsedHours.Should().Be(0.33m);
        row.UtilizationPercent.Should().Be(33.3m);
    }

    [Fact]
    public async Task Rounding_goes_away_from_zero_on_a_midpoint()
    {
        // 18 s of a 1 h meeting is exactly 0.005 h, which must round up to 0.01 (not to even).
        Add(ReservationStatus.Completed, endedAfter: TimeSpan.FromSeconds(18));

        (await AtlasAsync()).UsedHours.Should().Be(0.01m);
    }

    // ----- booked and elapsed -----

    [Fact]
    public async Task Booked_reservations_are_counted_but_not_in_the_hours()
    {
        Add(_room, ReservationStatus.Booked, Hour * 5, Hour * 2);

        var row = await AtlasAsync();

        row.TotalBookings.Should().Be(1);
        row.UpcomingBookings.Should().Be(1);
        row.CompletedBookings.Should().Be(0);
        row.PlannedHours.Should().Be(0m);
        row.UsedHours.Should().Be(0m);
        row.UtilizationPercent.Should().BeNull();
        row.IsOftenBookedRarelyUsed.Should().BeFalse();
    }

    [Fact]
    public async Task Booked_reservations_do_not_count_towards_the_minimum_number_of_bookings()
    {
        Add(3, ReservationStatus.Cancelled);
        Add(_room, ReservationStatus.Booked, Hour * 5, Hour);

        (await AtlasAsync()).IsOftenBookedRarelyUsed.Should().BeFalse();
    }

    [Fact]
    public async Task An_elapsed_booked_reservation_becomes_completed_after_the_clock_advances()
    {
        Add(_room, ReservationStatus.Booked, Hour, Hour);

        var before = await AtlasAsync();
        before.UpcomingBookings.Should().Be(1);
        before.PlannedHours.Should().Be(0m);

        _clock.Advance(Hour * 3);

        var after = await AtlasAsync();
        after.UpcomingBookings.Should().Be(0);
        after.CompletedBookings.Should().Be(1);
        after.PlannedHours.Should().Be(1m);
        after.UsedHours.Should().Be(1m);
    }

    [Fact]
    public async Task Counts_are_split_by_status()
    {
        Add(2, ReservationStatus.Completed);
        Add(1, ReservationStatus.Cancelled);
        Add(_room, ReservationStatus.Booked, Hour * 5, Hour);

        var row = await AtlasAsync();

        row.TotalBookings.Should().Be(4);
        row.CompletedBookings.Should().Be(2);
        row.CancelledBookings.Should().Be(1);
        row.UpcomingBookings.Should().Be(1);
    }

    // ----- window -----

    [Fact]
    public async Task From_and_to_are_inclusive_and_filter_by_start()
    {
        Add(_room, ReservationStatus.Cancelled, TimeSpan.FromDays(-3), Hour);
        Add(_room, ReservationStatus.Cancelled, TimeSpan.FromDays(-2), Hour);
        Add(_room, ReservationStatus.Cancelled, TimeSpan.FromDays(-1), Hour);
        Add(_room, ReservationStatus.Cancelled, TimeSpan.FromDays(1), Hour);

        var both = await AtlasAsync(new UsageReportQuery { From = TestDb.Now.AddDays(-2), To = TestDb.Now.AddDays(-1) });
        var onlyFrom = await AtlasAsync(new UsageReportQuery { From = TestDb.Now.AddDays(-1) });
        var onlyTo = await AtlasAsync(new UsageReportQuery { To = TestDb.Now.AddDays(-3) });
        var single = await AtlasAsync(new UsageReportQuery { From = TestDb.Now.AddDays(-2), To = TestDb.Now.AddDays(-2) });

        both.TotalBookings.Should().Be(2);
        onlyFrom.TotalBookings.Should().Be(2);
        onlyTo.TotalBookings.Should().Be(1);
        single.TotalBookings.Should().Be(1);
    }

    [Fact]
    public async Task A_long_meeting_belongs_to_the_period_in_which_it_starts()
    {
        Add(_room, ReservationStatus.Completed, TimeSpan.FromDays(-3) - Hour, Hour * 2);

        var row = await AtlasAsync(new UsageReportQuery { From = TestDb.Now.AddDays(-3) });

        row.TotalBookings.Should().Be(0);
    }

    [Fact]
    public async Task The_report_echoes_the_window_and_the_clock()
    {
        var from = TestDb.Now.AddDays(-5);
        var to = TestDb.Now.AddDays(-1);

        var report = await ReportAsync(new UsageReportQuery { From = from, To = to });

        report.From.Should().Be(from);
        report.To.Should().Be(to);
        report.GeneratedAt.Should().Be(TestDb.Now);
    }

    [Fact]
    public async Task Without_a_window_from_and_to_are_null()
    {
        var report = await ReportAsync();

        report.From.Should().BeNull();
        report.To.Should().BeNull();
    }

    // ----- rooms -----

    [Fact]
    public async Task A_room_without_bookings_is_listed_with_zeros_and_no_utilization()
    {
        var row = await AtlasAsync();

        row.Should().BeEquivalentTo(new RoomUsageDto(_room.Id, "Atlas", _room.Floor, 0, 0, 0, 0, 0m, 0m, null, false));
    }

    [Fact]
    public async Task An_inactive_room_is_listed()
    {
        var closed = TestDb.Room("Summit", isActive: false);
        _db.Rooms.Add(closed);

        var report = await ReportAsync();

        report.Rooms.Select(r => r.RoomName).Should().BeEquivalentTo("Atlas", "Summit");
    }

    [Fact]
    public async Task Reservations_are_kept_per_room()
    {
        var other = TestDb.Room("Cedar");
        _db.Rooms.Add(other);
        Add(2, ReservationStatus.Cancelled);
        Add(other, ReservationStatus.Completed, -Hour * 24, Hour);

        var report = await ReportAsync();

        report.Rooms.Single(r => r.RoomId == _room.Id).TotalBookings.Should().Be(2);
        report.Rooms.Single(r => r.RoomId == other.Id).TotalBookings.Should().Be(1);
    }

    [Fact]
    public async Task Rooms_are_ordered_flagged_first_then_by_total_bookings_then_by_name()
    {
        var busy = TestDb.Room("Busy");
        var quiet = TestDb.Room("Alpha");
        var flagged = TestDb.Room("Zeta");
        var tie = TestDb.Room("Beta");
        _db.Rooms.AddRange(busy, quiet, flagged, tie);
        for (var i = 0; i < 4; i++)
        {
            Add(flagged, ReservationStatus.Cancelled, -Hour * 24, Hour);
        }

        for (var i = 0; i < 5; i++)
        {
            Add(busy, ReservationStatus.Completed, -Hour * 24, Hour);
        }

        Add(quiet, ReservationStatus.Completed, -Hour * 24, Hour);
        Add(tie, ReservationStatus.Completed, -Hour * 24, Hour);

        var report = await ReportAsync();

        // Atlas (no bookings) is last of the unflagged rooms with 0 bookings.
        report.Rooms.Select(r => r.RoomName).Should().Equal("Zeta", "Busy", "Alpha", "Beta", "Atlas");
    }

    // ----- seeded data -----

    [Fact]
    public async Task Seeded_data_flags_exactly_one_room_the_boardroom()
    {
        var db = TestDb.Create();
        await DbSeeder.SeedAsync(db, _clock);
        var reservations = new ReservationRepository(db);
        var service = new UsageReportService(
            new RoomRepository(db), reservations, new ReservationLifecycle(reservations, _clock),
            Options.Create(LoadOptions()), _clock, MappingProfileTests.NewConfiguration().CreateMapper());

        var report = (await service.GetAsync(new UsageReportQuery())).Value!;

        var flagged = report.Rooms.Where(r => r.IsOftenBookedRarelyUsed).ToList();
        flagged.Should().ContainSingle().Which.RoomName.Should().Be("Boardroom");
        var boardroom = flagged.Single();
        (boardroom.CompletedBookings + boardroom.CancelledBookings).Should().Be(6);
        boardroom.PlannedHours.Should().Be(12m);
        boardroom.UtilizationPercent.Should().Be(3.5m);
        report.Rooms.Single(r => r.RoomName == "Echo").IsOftenBookedRarelyUsed.Should().BeFalse();
        report.Rooms.First().RoomName.Should().Be("Boardroom");
    }

    // The thresholds come from the API's own appsettings.json, so the seeder is checked against the real configuration.
    private static UsageReportOptions LoadOptions()
    {
        var options = new UsageReportOptions();
        new ConfigurationBuilder()
            .AddJsonFile(Path.Combine(AppContext.BaseDirectory, "appsettings.json"), optional: false)
            .Build()
            .GetSection(UsageReportOptions.SectionName)
            .Bind(options);
        return options;
    }
}
