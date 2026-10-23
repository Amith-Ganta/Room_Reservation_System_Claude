using Microsoft.AspNetCore.Mvc;
using RoomReservation.Api.Common;

namespace RoomReservation.Api.Controllers;

/// <summary>Base of every controller: the single place where a service <see cref="Result{T}"/> becomes an HTTP response.</summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public abstract class ApiControllerBase : ControllerBase
{
    protected IActionResult ToActionResult<T>(Result<T> result) => result.Status switch
    {
        ResultStatus.Ok => Ok(result.Value),
        ResultStatus.Invalid => UnprocessableEntity(new ApiErrorResponse(result.Errors)),
        ResultStatus.NotFound => NotFound(new ApiErrorResponse(result.Errors)),
        ResultStatus.Conflict => Conflict(new ApiErrorResponse(result.Errors)),
        _ => throw new InvalidOperationException($"Unhandled result status {result.Status}.")
    };

    /// <summary>Like <see cref="ToActionResult{T}"/>, but success is 201 Created with a <c>Location</c> header.</summary>
    protected IActionResult ToCreatedResult<T>(Result<T> result, string actionName, Func<T, object> routeValues) =>
        result.Status == ResultStatus.Ok
            ? CreatedAtAction(actionName, routeValues(result.Value!), result.Value)
            : ToActionResult(result);
}
