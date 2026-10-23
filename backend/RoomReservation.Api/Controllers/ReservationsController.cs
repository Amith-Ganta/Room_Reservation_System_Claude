using Microsoft.AspNetCore.Mvc;
using RoomReservation.Api.Common;
using RoomReservation.Api.Dtos;
using RoomReservation.Api.Services;

namespace RoomReservation.Api.Controllers;

public sealed class ReservationsController(IReservationService reservations) : ApiControllerBase
{
    /// <summary>Lists reservations, newest start first. All filters are optional and combine with AND.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<ReservationDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> List([FromQuery] ReservationListQuery query, CancellationToken ct) =>
        ToActionResult(await reservations.ListAsync(query, ct));

    [HttpGet("{id}")]
    [ProducesResponseType(typeof(ReservationDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct) =>
        ToActionResult(await reservations.GetAsync(id, ct));

    /// <summary>Creates a reservation after checking rules R1-R7 in the back end.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(ReservationDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Create([FromBody] CreateReservationRequest request, CancellationToken ct) =>
        ToCreatedResult(await reservations.CreateAsync(request, ct), nameof(GetById), created => new { id = created.Id });

    /// <summary>Changes a booked reservation that has not started. The same rules apply as for a new one.</summary>
    [HttpPut("{id}")]
    [ProducesResponseType(typeof(ReservationDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Update(Guid id, [FromBody] CreateReservationRequest request, CancellationToken ct) =>
        ToActionResult(await reservations.UpdateAsync(id, request, ct));

    /// <summary>Cancels a booked reservation that has not started.</summary>
    [HttpPost("{id}/cancel")]
    [ProducesResponseType(typeof(ReservationDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Cancel(Guid id, CancellationToken ct) =>
        ToActionResult(await reservations.CancelAsync(id, ct));

    /// <summary>Ends a running reservation now. The room is free from this moment.</summary>
    [HttpPost("{id}/end")]
    [ProducesResponseType(typeof(ReservationDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> EndEarly(Guid id, CancellationToken ct) =>
        ToActionResult(await reservations.EndEarlyAsync(id, ct));
}
