using Microsoft.AspNetCore.Mvc;
using RoomReservation.Api.Common;
using RoomReservation.Api.Dtos;
using RoomReservation.Api.Services;

namespace RoomReservation.Api.Controllers;

public sealed class RoomsController(IRoomService rooms) : ApiControllerBase
{
    /// <summary>Lists rooms (inactive ones included), ordered by floor then name. All filters are optional.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<RoomDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> List([FromQuery] RoomListQuery query, CancellationToken ct) =>
        ToActionResult(await rooms.ListAsync(query, ct));
}
