using Microsoft.AspNetCore.Mvc;
using RoomReservation.Api.Dtos;
using RoomReservation.Api.Services;

namespace RoomReservation.Api.Controllers;

public sealed class UsersController(IUserService users) : ApiControllerBase
{
    /// <summary>Lists all users, ordered by last name then first name, with their number of active (Booked) reservations.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<UserDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List(CancellationToken ct) =>
        ToActionResult(await users.ListAsync(ct));
}
