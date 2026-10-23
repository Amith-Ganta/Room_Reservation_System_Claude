using FluentAssertions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using RoomReservation.Api.Common;
using RoomReservation.Api.Configuration;
using RoomReservation.Api.Data;
using RoomReservation.Api.Domain;
using RoomReservation.Api.Dtos;
using RoomReservation.Api.Repositories;
using RoomReservation.Api.Services;
using RoomReservation.Tests.Repositories;

namespace RoomReservation.Tests.Services;

public class ReservationValidatorTests
{
    private static readonly DateTimeOffset Now = TestDb.Now;
    private static readonly TimeSpan Hour = TimeSpan.FromHours(1);
    private static readonly TimeSpan Second = TimeSpan.FromSeconds(1);

    private readonly AppDbContext _db = TestDb.Create();
    private readonly Room _room = TestDb.Room("Atlas", capacity: 10);
    private readonly User _user = TestDb.User();
    private BookingRulesOptions _limits = new() { MaxDurationDays = 7, MaxHorizonDays = 90 };

    public ReservationValidatorTests()
    {
        _db.Rooms.Add(_room);
        _db.Users.Add(_user);
        _db.SaveChanges();
    }

    private ReservationValidator Validator() =>
        new(new ReservationRepository(_db), Options.Create(_limits), new FakeTimeProvider(Now));

    private CreateReservationRequest Request(
        DateTimeOffset? start = null, TimeSpan? duration = null, int attendees = 2, string title = "Planning",
        User? user = null)
    {
        var from = start ?? Now + Hour;
        return new CreateReservationRequest(_room.Id, (user ?? _user).Id, title, from, from + (duration ?? Hour), attendees);
    }

    private async Task<string[]> CodesAsync(CreateReservationRequest request, Room? room = null, Guid? exclude = null) =>
        (await Validator().ValidateAsync(request, room ?? _room, exclude)).Select(e => e.Code).ToArray();

    private async Task<Reservation> AddReservationAsync(
        DateTimeOffset start, TimeSpan duration, User? user = null, Room? room = null,
        ReservationStatus status = ReservationStatus.Booked, DateTimeOffset? endedAt = null)
    {
        var reservation = TestDb.Reservation(room ?? _room, user ?? _user, start, duration, status, endedAt);
        _db.Reservations.Add(reservation);
        await _db.SaveChangesAsync();
        return reservation;
    }

    [Fact]
    public async Task A_valid_request_has_no_errors()
    {
        (await CodesAsync(Request())).Should().BeEmpty();
    }

    // R1

    [Fact]
    public async Task R1_exactly_seven_days_is_allowed_and_one_second_more_is_rejected()
    {
        (await CodesAsync(Request(duration: TimeSpan.FromDays(7)))).Should().BeEmpty();
        (await CodesAsync(Request(duration: TimeSpan.FromDays(7) + Second))).Should().Equal(ErrorCodes.MaxDuration);
    }

    [Fact]
    public async Task R1_message_quotes_the_configured_limit()
    {
        _limits = new BookingRulesOptions { MaxDurationDays = 3, MaxHorizonDays = 90 };

        var errors = await Validator().ValidateAsync(Request(duration: TimeSpan.FromDays(4)), _room);

        errors.Should().ContainSingle().Which.Message.Should().Be("A reservation can last at most 3 days.");
    }

    [Fact]
    public async Task R1_and_R6_messages_use_the_singular_for_one_day()
    {
        _limits = new BookingRulesOptions { MaxDurationDays = 1, MaxHorizonDays = 1 };

        var errors = await Validator().ValidateAsync(
            Request(start: Now + TimeSpan.FromDays(2), duration: TimeSpan.FromDays(2)), _room);

        errors.Select(e => e.Message).Should().Equal(
            "A reservation can last at most 1 day.",
            "A reservation cannot start more than 1 day ahead.");
    }

    // R2

    [Fact]
    public async Task R2_partial_overlap_in_the_same_room_is_rejected()
    {
        await AddReservationAsync(Now + Hour, 2 * Hour, user: TestDb.User("Bob", "Ray"));

        (await CodesAsync(Request(start: Now + 2 * Hour))).Should().Equal(ErrorCodes.RoomOverlap);
    }

    [Fact]
    public async Task R2_back_to_back_reservations_do_not_overlap()
    {
        await AddReservationAsync(Now + Hour, Hour, user: TestDb.User("Bob", "Ray"));

        (await CodesAsync(Request(start: Now + 2 * Hour))).Should().BeEmpty();
        (await CodesAsync(Request(start: Now))).Should().Contain(ErrorCodes.PastStart).And.NotContain(ErrorCodes.RoomOverlap);
    }

    [Fact]
    public async Task R2_cancelled_reservations_do_not_block()
    {
        await AddReservationAsync(Now + Hour, Hour, user: TestDb.User("Bob", "Ray"), status: ReservationStatus.Cancelled);

        (await CodesAsync(Request())).Should().BeEmpty();
    }

    [Fact]
    public async Task R2_a_reservation_ended_early_frees_the_room_from_the_end_time()
    {
        var other = TestDb.User("Bob", "Ray");
        await AddReservationAsync(Now + Hour, 4 * Hour, user: other, endedAt: Now + 2 * Hour);

        (await CodesAsync(Request(start: Now + 3 * Hour))).Should().BeEmpty();
        (await CodesAsync(Request(start: Now + 90 * TimeSpan.FromMinutes(1)))).Should().Equal(ErrorCodes.RoomOverlap);
    }

    [Fact]
    public async Task R2_completed_reservations_still_block_the_room()
    {
        await AddReservationAsync(Now + Hour, 2 * Hour, user: TestDb.User("Bob", "Ray"), status: ReservationStatus.Completed);

        (await CodesAsync(Request(start: Now + 90 * TimeSpan.FromMinutes(1)))).Should().Equal(ErrorCodes.RoomOverlap);
    }

    [Fact]
    public async Task R2_another_room_is_not_affected()
    {
        var otherRoom = TestDb.Room("Boreas");
        _db.Rooms.Add(otherRoom);
        await AddReservationAsync(Now + Hour, Hour, user: TestDb.User("Bob", "Ray"), room: otherRoom);

        (await CodesAsync(Request())).Should().BeEmpty();
    }

    [Fact]
    public async Task Editing_a_reservation_does_not_overlap_with_itself()
    {
        var existing = await AddReservationAsync(Now + Hour, Hour);

        (await CodesAsync(Request(start: Now + 90 * TimeSpan.FromMinutes(1)))).Should().Equal(ErrorCodes.RoomOverlap, ErrorCodes.UserOverlap);
        (await CodesAsync(Request(start: Now + 90 * TimeSpan.FromMinutes(1)), exclude: existing.Id)).Should().BeEmpty();
    }

    // R3

    [Fact]
    public async Task R3_the_organizer_cannot_be_in_two_rooms_at_once()
    {
        var otherRoom = TestDb.Room("Boreas");
        _db.Rooms.Add(otherRoom);
        await AddReservationAsync(Now + Hour, 2 * Hour, room: otherRoom);

        (await CodesAsync(Request(start: Now + 2 * Hour))).Should().Equal(ErrorCodes.UserOverlap);
    }

    [Fact]
    public async Task R3_back_to_back_and_cancelled_reservations_do_not_block_the_organizer()
    {
        var otherRoom = TestDb.Room("Boreas");
        _db.Rooms.Add(otherRoom);
        await AddReservationAsync(Now + Hour, Hour, room: otherRoom);
        await AddReservationAsync(Now + 5 * Hour, Hour, room: otherRoom, status: ReservationStatus.Cancelled);

        (await CodesAsync(Request(start: Now + 2 * Hour))).Should().BeEmpty();
        (await CodesAsync(Request(start: Now + 5 * Hour))).Should().BeEmpty();
    }

    [Fact]
    public async Task R3_a_reservation_ended_early_frees_the_organizer_from_the_end_time()
    {
        var otherRoom = TestDb.Room("Boreas");
        _db.Rooms.Add(otherRoom);
        await AddReservationAsync(Now + Hour, 4 * Hour, room: otherRoom, endedAt: Now + 2 * Hour);

        (await CodesAsync(Request(start: Now + 3 * Hour))).Should().BeEmpty();
        (await CodesAsync(Request(start: Now + 90 * TimeSpan.FromMinutes(1)))).Should().Equal(ErrorCodes.UserOverlap);
    }

    [Fact]
    public async Task R3_a_reservation_of_another_organizer_does_not_block()
    {
        var otherRoom = TestDb.Room("Boreas");
        _db.Rooms.Add(otherRoom);
        await AddReservationAsync(Now + Hour, Hour, user: TestDb.User("Bob", "Ray"), room: otherRoom);

        (await CodesAsync(Request())).Should().BeEmpty();
    }

    // R4

    [Fact]
    public async Task R4_start_equal_to_now_is_rejected_and_one_second_later_is_allowed()
    {
        (await CodesAsync(Request(start: Now))).Should().Equal(ErrorCodes.PastStart);
        (await CodesAsync(Request(start: Now + Second))).Should().BeEmpty();
        (await CodesAsync(Request(start: Now - Hour))).Should().Equal(ErrorCodes.PastStart);
    }

    [Fact]
    public async Task R4_compares_instants_not_wall_clock_times()
    {
        // One hour in the past, written at +14:00: the wall clock reads 13 hours AFTER "now".
        var past = (Now - Hour).ToOffset(TimeSpan.FromHours(14));
        past.DateTime.Should().BeAfter(Now.UtcDateTime);
        (await CodesAsync(Request(start: past))).Should().Equal(ErrorCodes.PastStart);

        // One hour ahead, written at -12:00: the wall clock reads 11 hours BEFORE "now".
        var future = (Now + Hour).ToOffset(TimeSpan.FromHours(-12));
        future.DateTime.Should().BeBefore(Now.UtcDateTime);
        (await CodesAsync(Request(start: future))).Should().BeEmpty();
    }

    // R5

    [Fact]
    public async Task R5_attendees_equal_to_capacity_is_allowed_and_one_more_is_rejected()
    {
        (await CodesAsync(Request(attendees: 10))).Should().BeEmpty();
        (await CodesAsync(Request(attendees: 11))).Should().Equal(ErrorCodes.CapacityExceeded);
    }

    // R6

    [Fact]
    public async Task R6_exactly_ninety_days_ahead_is_allowed_and_one_second_more_is_rejected()
    {
        (await CodesAsync(Request(start: Now + TimeSpan.FromDays(90)))).Should().BeEmpty();
        (await CodesAsync(Request(start: Now + TimeSpan.FromDays(90) + Second))).Should().Equal(ErrorCodes.BeyondHorizon);
    }

    [Fact]
    public async Task R6_message_quotes_the_configured_limit()
    {
        _limits = new BookingRulesOptions { MaxDurationDays = 7, MaxHorizonDays = 30 };

        var errors = await Validator().ValidateAsync(Request(start: Now + TimeSpan.FromDays(31)), _room);

        errors.Should().ContainSingle().Which.Message.Should().Contain("30 days");
    }

    [Fact]
    public async Task R6_compares_instants_not_wall_clock_times()
    {
        // One second past the limit, written at -12:00: the wall clock reads 12 hours before the limit.
        var tooFar = (Now + TimeSpan.FromDays(90) + Second).ToOffset(TimeSpan.FromHours(-12));
        tooFar.DateTime.Should().BeBefore((Now + TimeSpan.FromDays(90)).UtcDateTime);

        (await CodesAsync(Request(start: tooFar))).Should().Equal(ErrorCodes.BeyondHorizon);
    }

    [Fact]
    public async Task R1_compares_instants_not_wall_clock_times()
    {
        var start = (Now + Hour).ToOffset(TimeSpan.FromHours(14));
        var end = (start + TimeSpan.FromDays(7) + Second).ToOffset(TimeSpan.FromHours(-12));

        var request = new CreateReservationRequest(_room.Id, _user.Id, "Long", start, end, 2);

        (await CodesAsync(request)).Should().Equal(ErrorCodes.MaxDuration);
    }

    // R7

    [Fact]
    public async Task R7_an_inactive_room_cannot_be_booked()
    {
        var closed = TestDb.Room("Closed", isActive: false);

        (await CodesAsync(Request(), closed)).Should().Equal(ErrorCodes.RoomInactive);
    }

    // Limits come from options

    [Fact]
    public async Task Changing_the_limits_changes_the_outcome()
    {
        var request = Request(start: Now + TimeSpan.FromDays(10), duration: TimeSpan.FromDays(8));
        (await CodesAsync(request)).Should().Equal(ErrorCodes.MaxDuration);

        _limits = new BookingRulesOptions { MaxDurationDays = 10, MaxHorizonDays = 5 };
        (await CodesAsync(request)).Should().Equal(ErrorCodes.BeyondHorizon);
    }

    // Several violations, fixed order

    [Fact]
    public async Task Every_violation_is_returned_together_in_rule_order()
    {
        var closed = TestDb.Room("Closed", capacity: 2, isActive: false);
        _db.Rooms.Add(closed);
        await AddReservationAsync(Now - Hour, TimeSpan.FromDays(120), room: closed, user: TestDb.User("Bob", "Ray"));
        await AddReservationAsync(Now - Hour, TimeSpan.FromDays(120));
        var request = new CreateReservationRequest(closed.Id, _user.Id, "Offsite", Now - Hour, Now + TimeSpan.FromDays(100), 5);

        var errors = await Validator().ValidateAsync(request, closed);

        errors.Select(e => e.Code).Should().Equal(
            ErrorCodes.MaxDuration, ErrorCodes.RoomOverlap, ErrorCodes.UserOverlap, ErrorCodes.PastStart,
            ErrorCodes.CapacityExceeded, ErrorCodes.RoomInactive);
        errors.Should().OnlyContain(e => e.Message.Length > 0);
    }

    [Fact]
    public async Task Start_beyond_the_horizon_and_over_capacity_are_both_reported()
    {
        var request = Request(start: Now + TimeSpan.FromDays(91), attendees: 99);

        (await CodesAsync(request)).Should().Equal(ErrorCodes.CapacityExceeded, ErrorCodes.BeyondHorizon);
    }

    // Basic input errors

    [Fact]
    public async Task End_before_or_equal_to_start_is_an_invalid_time_range()
    {
        var start = Now + Hour;
        (await CodesAsync(new CreateReservationRequest(_room.Id, _user.Id, "T", start, start - Hour, 2)))
            .Should().Equal(ErrorCodes.InvalidTimeRange);
        (await CodesAsync(new CreateReservationRequest(_room.Id, _user.Id, "T", start, start, 2)))
            .Should().Equal(ErrorCodes.InvalidTimeRange);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task A_blank_title_is_rejected(string? title)
    {
        (await CodesAsync(Request(title: title!))).Should().Equal(ErrorCodes.TitleRequired);
    }

    [Fact]
    public async Task A_title_of_the_maximum_length_is_allowed_and_one_more_character_is_rejected()
    {
        var max = Reservation.TitleMaxLength;

        (await CodesAsync(Request(title: new string('a', max)))).Should().BeEmpty();
        (await CodesAsync(Request(title: new string('a', max + 1)))).Should().Equal(ErrorCodes.TitleTooLong);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-3)]
    public async Task Fewer_than_one_attendee_is_rejected(int attendees)
    {
        (await CodesAsync(Request(attendees: attendees))).Should().Equal(ErrorCodes.InvalidAttendees);
    }

    [Fact]
    public async Task Basic_errors_are_returned_alone_without_the_rules()
    {
        var closed = TestDb.Room("Closed", capacity: 1, isActive: false);
        var start = Now - Hour;
        var request = new CreateReservationRequest(closed.Id, _user.Id, " ", start, start - Hour, 0);

        (await CodesAsync(request, closed)).Should().Equal(
            ErrorCodes.InvalidTimeRange, ErrorCodes.TitleRequired, ErrorCodes.InvalidAttendees);
    }
}
