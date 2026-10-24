using AutoMapper;
using Microsoft.Extensions.Options;
using RoomReservation.Api.Common;
using RoomReservation.Api.Configuration;
using RoomReservation.Api.Domain;
using RoomReservation.Api.Dtos;
using RoomReservation.Api.Repositories;

namespace RoomReservation.Api.Services;

/// <summary>
/// Orchestrates the reservation use cases. Every write runs inside one static lock, so the overlap checks (R2/R3)
/// and the save cannot interleave with another request. Reads take no lock: completing elapsed reservations is idempotent.
/// </summary>
public sealed class ReservationService(
    IReservationRepository reservations,
    IRoomRepository rooms,
    IUserRepository users,
    IReservationValidator validator,
    ReservationLifecycle lifecycle,
    IMapper mapper,
    TimeProvider timeProvider,
    IOptions<PagingOptions> paging) : IReservationService
{
    // Static on purpose: the service is scoped, so an instance lock would not guard parallel requests.
    private static readonly SemaphoreSlim WriteLock = new(1, 1);

    public async Task<Result<PagedResult<ReservationDto>>> ListAsync(ReservationListQuery query, CancellationToken ct = default)
    {
        await lifecycle.CompleteElapsedAsync(ct);
        var page = query.ToRequest(paging.Value);
        var found = await reservations.ListPagedAsync(mapper.Map<ReservationFilter>(query), page, ct);
        return Result<PagedResult<ReservationDto>>.Ok(
            PagedResult<ReservationDto>.Create(mapper.Map<List<ReservationDto>>(found.Items), page, found.TotalCount));
    }

    public async Task<Result<ReservationDto>> GetAsync(Guid id, CancellationToken ct = default)
    {
        await lifecycle.CompleteElapsedAsync(ct);
        var reservation = await reservations.GetWithDetailsAsync(id, ct);
        return reservation is null ? NotFoundReservation() : Result<ReservationDto>.Ok(mapper.Map<ReservationDto>(reservation));
    }

    public Task<Result<ReservationDto>> CreateAsync(CreateReservationRequest request, CancellationToken ct = default) =>
        WithWriteLockAsync(async () =>
        {
            var target = await LoadTargetAsync(request, ct);
            if (target.Failure is { } failure)
            {
                return failure;
            }

            var (room, organizer) = (target.Room!, target.Organizer!);
            var errors = await validator.ValidateAsync(request, room, excludeReservationId: null, ct);
            if (errors.Count > 0)
            {
                return Result<ReservationDto>.Invalid(errors);
            }

            var reservation = new Reservation
            {
                Id = Guid.NewGuid(),
                RoomId = room.Id,
                Room = room,
                UserId = organizer.Id,
                User = organizer,
                Title = request.Title!.Trim(),
                Start = request.Start,
                End = request.End,
                AttendeeCount = request.AttendeeCount,
                Status = ReservationStatus.Booked
            };
            await reservations.AddAsync(reservation, ct);
            return Result<ReservationDto>.Ok(mapper.Map<ReservationDto>(reservation));
        }, ct);

    public Task<Result<ReservationDto>> UpdateAsync(Guid id, CreateReservationRequest request, CancellationToken ct = default) =>
        WithWriteLockAsync(async () =>
        {
            var reservation = await reservations.GetWithDetailsAsync(id, ct);
            if (reservation is null)
            {
                return NotFoundReservation();
            }

            if (CheckCanChangeBeforeStart(reservation) is { } stateError)
            {
                return stateError;
            }

            var target = await LoadTargetAsync(request, ct);
            if (target.Failure is { } failure)
            {
                return failure;
            }

            var (room, organizer) = (target.Room!, target.Organizer!);
            var errors = await validator.ValidateAsync(request, room, excludeReservationId: id, ct);
            if (errors.Count > 0)
            {
                return Result<ReservationDto>.Invalid(errors);
            }

            reservation.Room = room;
            reservation.RoomId = room.Id;
            reservation.User = organizer;
            reservation.UserId = organizer.Id;
            reservation.Title = request.Title!.Trim();
            reservation.Start = request.Start;
            reservation.End = request.End;
            reservation.AttendeeCount = request.AttendeeCount;
            await reservations.UpdateAsync(reservation, ct);
            return Result<ReservationDto>.Ok(mapper.Map<ReservationDto>(reservation));
        }, ct);

    public Task<Result<ReservationDto>> CancelAsync(Guid id, CancellationToken ct = default) =>
        WithWriteLockAsync(async () =>
        {
            var reservation = await reservations.GetWithDetailsAsync(id, ct);
            if (reservation is null)
            {
                return NotFoundReservation();
            }

            // A running meeting is ended, not cancelled.
            if (CheckCanChangeBeforeStart(reservation) is { } stateError)
            {
                return stateError;
            }

            reservation.Status = ReservationStatus.Cancelled;
            await reservations.UpdateAsync(reservation, ct);
            return Result<ReservationDto>.Ok(mapper.Map<ReservationDto>(reservation));
        }, ct);

    public Task<Result<ReservationDto>> EndEarlyAsync(Guid id, CancellationToken ct = default) =>
        WithWriteLockAsync(async () =>
        {
            var reservation = await reservations.GetWithDetailsAsync(id, ct);
            if (reservation is null)
            {
                return NotFoundReservation();
            }

            if (reservation.Status != ReservationStatus.Booked)
            {
                return NotBooked();
            }

            var now = timeProvider.GetUtcNow();
            if (reservation.Start > now)
            {
                return Result<ReservationDto>.Conflict(new ApiError(ErrorCodes.NotStarted,
                    "The reservation has not started yet. Cancel it instead."));
            }

            // After the elapsed pass a Booked reservation that has started is still running, so no other case remains.
            // The clock may have passed End since that pass; EndedAt must never be later than the planned end.
            reservation.EndedAt = now < reservation.End ? now : reservation.End;
            reservation.Status = ReservationStatus.Completed;
            await reservations.UpdateAsync(reservation, ct);
            return Result<ReservationDto>.Ok(mapper.Map<ReservationDto>(reservation));
        }, ct);

    /// <summary>
    /// Runs a write under the static lock, after completing elapsed reservations. The lock is always released.
    /// </summary>
    private async Task<Result<ReservationDto>> WithWriteLockAsync(Func<Task<Result<ReservationDto>>> action, CancellationToken ct)
    {
        await WriteLock.WaitAsync(ct);
        try
        {
            await lifecycle.CompleteElapsedAsync(ct);
            return await action();
        }
        finally
        {
            WriteLock.Release();
        }
    }

    /// <summary>Loads the room first, then the organizer; only one not-found error is returned.</summary>
    private async Task<(Room? Room, User? Organizer, Result<ReservationDto>? Failure)> LoadTargetAsync(
        CreateReservationRequest request, CancellationToken ct)
    {
        var room = await rooms.GetByIdAsync(request.RoomId, ct);
        if (room is null)
        {
            return (null, null, Result<ReservationDto>.NotFound(
                new ApiError(ErrorCodes.RoomNotFound, "The room does not exist.")));
        }

        var organizer = await users.GetByIdAsync(request.UserId, ct);
        if (organizer is null)
        {
            return (null, null, Result<ReservationDto>.NotFound(
                new ApiError(ErrorCodes.UserNotFound, "The organizer does not exist.")));
        }

        return (room, organizer, null);
    }

    /// <summary>Edit and cancel are only allowed for a Booked reservation that has not started (Start &gt; now).</summary>
    private Result<ReservationDto>? CheckCanChangeBeforeStart(Reservation reservation)
    {
        if (reservation.Status != ReservationStatus.Booked)
        {
            return NotBooked();
        }

        if (reservation.Start <= timeProvider.GetUtcNow())
        {
            return Result<ReservationDto>.Conflict(new ApiError(ErrorCodes.AlreadyStarted,
                "The reservation has already started. End it early instead."));
        }

        return null;
    }

    private static Result<ReservationDto> NotFoundReservation() =>
        Result<ReservationDto>.NotFound(new ApiError(ErrorCodes.ReservationNotFound, "The reservation does not exist."));

    private static Result<ReservationDto> NotBooked() =>
        Result<ReservationDto>.Conflict(new ApiError(ErrorCodes.NotBooked,
            "Only a booked reservation can be changed. This one is already completed or cancelled."));
}
