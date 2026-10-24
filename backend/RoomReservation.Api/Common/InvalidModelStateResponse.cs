using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace RoomReservation.Api.Common;

/// <summary>Turns a failed model binding or DataAnnotations check into the standard error body (HTTP 400).</summary>
public static partial class InvalidModelStateResponse
{
    public static IActionResult Create(ActionContext context)
    {
        var errors = context.ModelState
            .Where(entry => entry.Value is { Errors.Count: > 0 })
            .SelectMany(entry => entry.Value!.Errors.Select(error => new ApiError(
                ErrorCodes.InvalidRequest,
                IsSafeKey(entry.Key)
                    ? $"{entry.Key}: {Describe(error)}"
                    : Describe(error))))
            .ToList();

        return new BadRequestObjectResult(new ApiErrorResponse(errors));
    }

    // The key comes from the request (a property path). Echo it only when it looks like a plain path, never free text.
    private static bool IsSafeKey(string key) => key.Length is > 0 and <= 64 && KeyPattern().IsMatch(key);

    [GeneratedRegex(@"^[A-Za-z$][A-Za-z0-9_.\[\]]*$")]
    private static partial Regex KeyPattern();

    // Binding failures carry the raw System.Text.Json message (type names, byte positions); never show it.
    private static string Describe(ModelError error) =>
        error.Exception is not null || string.IsNullOrEmpty(error.ErrorMessage)
            ? "The value is invalid."
            : error.ErrorMessage;
}
