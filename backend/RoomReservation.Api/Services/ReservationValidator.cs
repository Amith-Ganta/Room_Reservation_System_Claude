using Microsoft.Extensions.Options;
using RoomReservation.Api.Common;
using RoomReservation.Api.Configuration;
using RoomReservation.Api.Domain;
using RoomReservation.Api.Dtos;
using RoomReservation.Api.Repositories;

namespace RoomReservation.Api.Services;

public sealed class ReservationValidator(
    IReservationRepository reservations,
    IOptions<BookingRulesOptions> options,
    TimeProvider timeProvider) : IReservationValidator
{
    public async Task<IReadOnlyList<ApiError>> ValidateAsync(
        CreateReservationRequest request, Room room, Guid? excludeReservationId = null, CancellationToken ct = default)
    {
        var basic = CheckInput(request);
        if (basic.Count > 0)
        {
            return basic;
        }

        var limits = options.Value;
        var now = timeProvider.GetUtcNow();
        var errors = new List<ApiError>();

        var maxDuration = TimeSpan.FromDays(limits.MaxDurationDays);
        if (request.End - request.Start > maxDuration)
        {
            errors.Add(new ApiError(ErrorCodes.MaxDuration,
                $"A reservation can last at most {Days(limits.MaxDurationDays)}."));
        }

        if (await reservations.HasRoomOverlapAsync(room.Id, request.Start, request.End, excludeReservationId, ct))
        {
            errors.Add(new ApiError(ErrorCodes.RoomOverlap,
                $"Room '{room.Name}' is already booked during part of this time."));
        }

        if (await reservations.HasUserOverlapAsync(request.UserId, request.Start, request.End, excludeReservationId, ct))
        {
            errors.Add(new ApiError(ErrorCodes.UserOverlap,
                "The organizer already has another reservation during part of this time."));
        }

        if (request.Start <= now)
        {
            errors.Add(new ApiError(ErrorCodes.PastStart, "The reservation must start in the future."));
        }

        if (request.AttendeeCount > room.Capacity)
        {
            errors.Add(new ApiError(ErrorCodes.CapacityExceeded,
                $"Room '{room.Name}' seats {room.Capacity} people, but {request.AttendeeCount} attendees were given."));
        }

        if (request.Start > now + TimeSpan.FromDays(limits.MaxHorizonDays))
        {
            errors.Add(new ApiError(ErrorCodes.BeyondHorizon,
                $"A reservation cannot start more than {Days(limits.MaxHorizonDays)} ahead."));
        }

        if (!room.IsActive)
        {
            errors.Add(new ApiError(ErrorCodes.RoomInactive, $"Room '{room.Name}' is inactive and cannot be booked."));
        }

        return errors;
    }

    private static string Days(int count) => count == 1 ? "1 day" : $"{count} days";

    // The rules mean nothing for a broken range, title or head count, so these come back on their own.
    private static List<ApiError> CheckInput(CreateReservationRequest request)
    {
        var errors = new List<ApiError>();
        if (request.End <= request.Start)
        {
            errors.Add(new ApiError(ErrorCodes.InvalidTimeRange, "The end time must be after the start time."));
        }

        if (string.IsNullOrWhiteSpace(request.Title))
        {
            errors.Add(new ApiError(ErrorCodes.TitleRequired, "A title is required."));
        }
        else if (request.Title.Length > Reservation.TitleMaxLength)
        {
            errors.Add(new ApiError(ErrorCodes.TitleTooLong,
                $"The title can have at most {Reservation.TitleMaxLength} characters."));
        }

        if (request.AttendeeCount < 1)
        {
            errors.Add(new ApiError(ErrorCodes.InvalidAttendees, "There must be at least one attendee."));
        }

        return errors;
    }
}
