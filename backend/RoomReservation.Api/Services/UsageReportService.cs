using AutoMapper;
using Microsoft.Extensions.Options;
using RoomReservation.Api.Common;
using RoomReservation.Api.Configuration;
using RoomReservation.Api.Domain;
using RoomReservation.Api.Dtos;
using RoomReservation.Api.Repositories;

namespace RoomReservation.Api.Services;

public interface IUsageReportService
{
    Task<Result<UsageReportDto>> GetAsync(UsageReportQuery query, CancellationToken ct = default);
}

/// <summary>
/// Compares planned hours with used hours per room and flags rooms that are booked often but rarely used.
/// Elapsed meetings are completed first, so they are judged as Completed. Only resolved reservations (Completed or
/// Cancelled) count towards the hours, because the outcome of a Booked one is not known yet.
/// </summary>
public sealed class UsageReportService(
    IRoomRepository rooms,
    IReservationRepository reservations,
    ReservationLifecycle lifecycle,
    IOptions<UsageReportOptions> options,
    TimeProvider clock,
    IMapper mapper) : IUsageReportService
{
    public async Task<Result<UsageReportDto>> GetAsync(UsageReportQuery query, CancellationToken ct = default)
    {
        await lifecycle.CompleteElapsedAsync(ct);

        var thresholds = options.Value;
        var allRooms = await rooms.ListAsync(new RoomFilter(), ct);
        var inWindow = await reservations.ListForReportAsync(mapper.Map<UsageReportFilter>(query), ct);
        var byRoom = inWindow.ToLookup(reservation => reservation.RoomId);

        var rows = allRooms
            .Select(room => BuildRow(room, byRoom[room.Id].ToList(), thresholds))
            .OrderByDescending(row => row.IsOftenBookedRarelyUsed)
            .ThenByDescending(row => row.TotalBookings)
            .ThenBy(row => row.RoomName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return Result<UsageReportDto>.Ok(new UsageReportDto(
            clock.GetUtcNow(), query.From, query.To,
            thresholds.MinBookingsToFlag, thresholds.UnderusedThresholdPercent, rows));
    }

    private static RoomUsageDto BuildRow(Room room, List<Reservation> found, UsageReportOptions thresholds)
    {
        var completed = found.Where(r => r.Status == ReservationStatus.Completed).ToList();
        var cancelled = found.Count(r => r.Status == ReservationStatus.Cancelled);
        var upcoming = found.Count(r => r.Status == ReservationStatus.Booked);
        var resolved = completed.Count + cancelled;

        // Unrounded figures decide the percentage and the flag; rounding happens only when the DTO is built.
        var planned = found
            .Where(r => r.Status != ReservationStatus.Booked)
            .Sum(r => Hours(r.End - r.Start));
        var used = completed.Sum(UsedHours);

        decimal? utilization = planned > 0 ? used / planned * 100m : null;
        var flagged = resolved >= thresholds.MinBookingsToFlag
                      && utilization is { } percent
                      && percent < thresholds.UnderusedThresholdPercent;

        return new RoomUsageDto(
            room.Id, room.Name, room.Floor,
            found.Count, completed.Count, cancelled, upcoming,
            Round(planned, 2), Round(used, 2), utilization is { } value ? Round(value, 1) : null,
            flagged);
    }

    /// <summary>Hours a completed meeting really took: until it was ended early (or its planned end), clamped to 0..planned.</summary>
    private static decimal UsedHours(Reservation reservation)
    {
        var planned = reservation.End - reservation.Start;
        var actual = (reservation.EndedAt ?? reservation.End) - reservation.Start;
        var clamped = actual < TimeSpan.Zero ? TimeSpan.Zero : actual > planned ? planned : actual;
        return Hours(clamped);
    }

    private static decimal Round(decimal value, int decimals) =>
        Math.Round(value, decimals, MidpointRounding.AwayFromZero);

    // Ticks keep the conversion exact; a double would add binary rounding noise before the percentage is taken.
    private static decimal Hours(TimeSpan span) => (decimal)span.Ticks / TimeSpan.TicksPerHour;
}
