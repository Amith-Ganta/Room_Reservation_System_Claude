using Microsoft.AspNetCore.Mvc;
using RoomReservation.Api.Common;
using RoomReservation.Api.Dtos;
using RoomReservation.Api.Services;

namespace RoomReservation.Api.Controllers;

public sealed class ReportsController(IUsageReportService usageReport) : ApiControllerBase
{
    /// <summary>
    /// Planned versus used hours per room, with the "often booked, rarely used" flag. Every room is listed; flagged
    /// rooms come first. Reservations belong to the period in which they start.
    /// </summary>
    [HttpGet("usage")]
    [ProducesResponseType(typeof(UsageReportDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Usage([FromQuery] UsageReportQuery query, CancellationToken ct) =>
        ToActionResult(await usageReport.GetAsync(query, ct));
}
