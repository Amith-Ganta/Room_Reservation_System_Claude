using Microsoft.AspNetCore.Mvc;
using RoomReservation.Api.Common;
using RoomReservation.Api.Dtos;
using RoomReservation.Api.Services;

namespace RoomReservation.Api.Controllers;

public sealed class UsersController(IUserService users) : ApiControllerBase
{
    /// <summary>Lists one page of users, ordered by last name then first name, with their number of active (Booked) reservations.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<UserDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> List([FromQuery] UserListQuery query, CancellationToken ct) =>
        ToActionResult(await users.ListAsync(query, ct));
}
