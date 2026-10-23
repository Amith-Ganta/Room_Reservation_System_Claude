using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace RoomReservation.Api.Common;

/// <summary>Turns a failed model binding or DataAnnotations check into the standard error body (HTTP 400).</summary>
public static class InvalidModelStateResponse
{
    public static IActionResult Create(ActionContext context)
    {
        var errors = context.ModelState
            .Where(entry => entry.Value is { Errors.Count: > 0 })
            .SelectMany(entry => entry.Value!.Errors.Select(error => new ApiError(
                ErrorCodes.InvalidRequest,
                string.IsNullOrWhiteSpace(entry.Key)
                    ? Describe(error)
                    : $"{entry.Key}: {Describe(error)}")))
            .ToList();

        return new BadRequestObjectResult(new ApiErrorResponse(errors));
    }

    // Binding failures carry the raw System.Text.Json message (type names, byte positions); never show it.
    private static string Describe(ModelError error) =>
        error.Exception is not null || string.IsNullOrEmpty(error.ErrorMessage)
            ? "The value is invalid."
            : error.ErrorMessage;
}
