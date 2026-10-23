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

        // Time is summed as whole ticks, so the percentage and the flag are decided without any rounding noise
        // (20 minutes is not exact in decimal hours). Hours are only derived, and rounded, for display.
        var plannedTicks = found
            .Where(r => r.Status != ReservationStatus.Booked)
            .Sum(r => (r.End - r.Start).Ticks);
        var usedTicks = completed.Sum(UsedTicks);

        decimal? utilization = plannedTicks > 0 ? (decimal)usedTicks * 100m / plannedTicks : null;
        var flagged = resolved >= thresholds.MinBookingsToFlag
                      && utilization is { } percent
                      && percent < thresholds.UnderusedThresholdPercent;

        return new RoomUsageDto(
            room.Id, room.Name, room.Floor,
            found.Count, completed.Count, cancelled, upcoming,
            Round(Hours(plannedTicks), 2), Round(Hours(usedTicks), 2), utilization is { } value ? Round(value, 1) : null,
            flagged);
    }

    /// <summary>Ticks a completed meeting really took: until it was ended early (or its planned end), clamped to 0..planned.</summary>
    private static long UsedTicks(Reservation reservation)
    {
        var planned = (reservation.End - reservation.Start).Ticks;
        var actual = ((reservation.EndedAt ?? reservation.End) - reservation.Start).Ticks;
        return Math.Clamp(actual, 0, planned);
    }

    private static decimal Round(decimal value, int decimals) =>
        Math.Round(value, decimals, MidpointRounding.AwayFromZero);

    private static decimal Hours(long ticks) => (decimal)ticks / TimeSpan.TicksPerHour;
}
