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
using RoomReservation.Tests.Mapping;
using RoomReservation.Tests.Repositories;

namespace RoomReservation.Tests.Services;

/// <summary>
/// Service-level tests that the endpoint tests cannot reach directly: the write lock is released on every path,
/// the not-found order, and the exact times written by "end early".
/// </summary>
public class ReservationServiceTests
{
    private static readonly TimeSpan Hour = TimeSpan.FromHours(1);

    private readonly AppDbContext _db = TestDb.Create();
    private readonly Room _room = TestDb.Room("Atlas", capacity: 10);
    private readonly User _user = TestDb.User();

    public ReservationServiceTests()
    {
        _db.Rooms.Add(_room);
        _db.Users.Add(_user);
        _db.SaveChanges();
    }

    private ReservationService Service()
    {
        var clock = new FakeTimeProvider(TestDb.Now);
        var repository = new ReservationRepository(_db);
        var validator = new ReservationValidator(repository, Options.Create(new BookingRulesOptions { MaxDurationDays = 7, MaxHorizonDays = 90 }), clock);
        return new ReservationService(
            repository, new RoomRepository(_db), new UserRepository(_db), validator,
            new ReservationLifecycle(repository, clock), MappingProfileTests.NewConfiguration().CreateMapper(), clock);
    }

    private CreateReservationRequest Request(
        Guid? roomId = null, Guid? userId = null, string? title = "Planning", TimeSpan? startsIn = null) =>
        new(roomId ?? _room.Id, userId ?? _user.Id, title,
            TestDb.Now + (startsIn ?? Hour), TestDb.Now + (startsIn ?? Hour) + Hour, 2);

    private async Task<Reservation> SeedAsync(TimeSpan startsIn, ReservationStatus status = ReservationStatus.Booked)
    {
        var reservation = TestDb.Reservation(_room, _user, TestDb.Now + startsIn, Hour * 2, status);
        _db.Reservations.Add(reservation);
        await _db.SaveChangesAsync();
        return reservation;
    }

    [Fact]
    public async Task A_valid_request_is_saved_as_booked_with_a_trimmed_title()
    {
        var result = await Service().CreateAsync(Request(title: "  Planning  "));

        result.Status.Should().Be(ResultStatus.Ok);
        result.Value!.Title.Should().Be("Planning");
        result.Value.Status.Should().Be("Booked");
        _db.Reservations.Should().ContainSingle();
    }

    [Fact]
    public async Task The_room_is_looked_up_before_the_organizer()
    {
        var result = await Service().CreateAsync(Request(roomId: Guid.NewGuid(), userId: Guid.NewGuid()));

        result.Status.Should().Be(ResultStatus.NotFound);
        result.Errors.Should().ContainSingle().Which.Code.Should().Be(ErrorCodes.RoomNotFound);
    }

    [Fact]
    public async Task An_unknown_organizer_is_not_found()
    {
        var result = await Service().CreateAsync(Request(userId: Guid.NewGuid()));

        result.Status.Should().Be(ResultStatus.NotFound);
        result.Errors.Should().ContainSingle().Which.Code.Should().Be(ErrorCodes.UserNotFound);
    }

    [Fact]
    public async Task A_missing_title_is_a_validation_error_and_saves_nothing()
    {
        var result = await Service().CreateAsync(Request(title: null));

        result.Status.Should().Be(ResultStatus.Invalid);
        result.Errors.Should().ContainSingle().Which.Code.Should().Be(ErrorCodes.TitleRequired);
        _db.Reservations.Should().BeEmpty();
    }

    [Fact]
    public async Task The_write_lock_is_released_after_every_kind_of_failure()
    {
        var service = Service();

        // Not found, invalid and conflict results all leave the static lock free for the next write.
        (await service.CreateAsync(Request(roomId: Guid.NewGuid()))).Status.Should().Be(ResultStatus.NotFound);
        (await service.CreateAsync(Request(title: " "))).Status.Should().Be(ResultStatus.Invalid);
        (await service.CancelAsync(Guid.NewGuid())).Status.Should().Be(ResultStatus.NotFound);
        (await service.EndEarlyAsync(Guid.NewGuid())).Status.Should().Be(ResultStatus.NotFound);

        var create = service.CreateAsync(Request());
        (await create.WaitAsync(TimeSpan.FromSeconds(5))).Status.Should().Be(ResultStatus.Ok);
    }

    [Fact]
    public async Task A_cancelled_wait_for_the_lock_does_not_break_later_writes()
    {
        var service = Service();
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        var act = () => service.CreateAsync(Request(), cancelled.Token);
        await act.Should().ThrowAsync<OperationCanceledException>();

        var next = service.CreateAsync(Request());
        (await next.WaitAsync(TimeSpan.FromSeconds(5))).Status.Should().Be(ResultStatus.Ok);
    }

    [Fact]
    public async Task Cancelling_twice_gives_a_conflict_the_second_time()
    {
        var reservation = await SeedAsync(Hour);
        var service = Service();

        (await service.CancelAsync(reservation.Id)).Value!.Status.Should().Be("Cancelled");
        var second = await service.CancelAsync(reservation.Id);

        second.Status.Should().Be(ResultStatus.Conflict);
        second.Errors.Single().Code.Should().Be(ErrorCodes.NotBooked);
    }

    [Fact]
    public async Task A_started_reservation_cannot_be_edited_or_cancelled()
    {
        var running = await SeedAsync(-Hour);
        var service = Service();

        var edit = await service.UpdateAsync(running.Id, Request());
        var cancel = await service.CancelAsync(running.Id);

        edit.Status.Should().Be(ResultStatus.Conflict);
        edit.Errors.Single().Code.Should().Be(ErrorCodes.AlreadyStarted);
        cancel.Status.Should().Be(ResultStatus.Conflict);
        cancel.Errors.Single().Code.Should().Be(ErrorCodes.AlreadyStarted);
    }

    [Fact]
    public async Task Ending_early_sets_ended_at_to_now_and_keeps_the_planned_end()
    {
        var running = await SeedAsync(-Hour);

        var result = await Service().EndEarlyAsync(running.Id);

        result.Status.Should().Be(ResultStatus.Ok);
        result.Value!.Status.Should().Be("Completed");
        result.Value.EndedAt.Should().Be(TestDb.Now);
        result.Value.End.Should().Be(running.End);
    }

    [Fact]
    public async Task Ending_a_reservation_that_has_not_started_is_a_conflict()
    {
        var future = await SeedAsync(Hour);

        var result = await Service().EndEarlyAsync(future.Id);

        result.Status.Should().Be(ResultStatus.Conflict);
        result.Errors.Single().Code.Should().Be(ErrorCodes.NotStarted);
    }

    [Fact]
    public async Task A_window_with_equal_from_and_to_is_empty_even_when_a_meeting_spans_it()
    {
        await SeedAsync(-Hour);

        var result = await Service().ListAsync(new ReservationListQuery { From = TestDb.Now, To = TestDb.Now });

        result.Status.Should().Be(ResultStatus.Ok);
        result.Value.Should().BeEmpty();
    }
}
