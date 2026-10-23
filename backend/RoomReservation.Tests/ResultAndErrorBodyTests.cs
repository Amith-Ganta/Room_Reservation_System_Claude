using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Routing;
using RoomReservation.Api.Common;

namespace RoomReservation.Tests;

public class ResultAndErrorBodyTests
{
    [Fact]
    public void Ok_result_carries_the_value_and_no_errors()
    {
        var result = Result<int>.Ok(42);

        result.IsSuccess.Should().BeTrue();
        result.Status.Should().Be(ResultStatus.Ok);
        result.Value.Should().Be(42);
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void Invalid_result_keeps_every_error()
    {
        var errors = new[]
        {
            new ApiError(ErrorCodes.MaxDuration, "too long"),
            new ApiError(ErrorCodes.PastStart, "in the past")
        };

        var result = Result<int>.Invalid(errors);

        result.IsSuccess.Should().BeFalse();
        result.Status.Should().Be(ResultStatus.Invalid);
        result.Errors.Should().HaveCount(2);
    }

    [Fact]
    public void NotFound_and_Conflict_carry_a_single_error()
    {
        var notFound = Result<int>.NotFound(new ApiError(ErrorCodes.RoomNotFound, "missing"));
        var conflict = Result<int>.Conflict(new ApiError(ErrorCodes.NotBooked, "not booked"));

        notFound.Status.Should().Be(ResultStatus.NotFound);
        notFound.Errors.Should().ContainSingle();
        conflict.Status.Should().Be(ResultStatus.Conflict);
        conflict.Errors.Should().ContainSingle();
    }

    [Fact]
    public void Invalid_model_state_becomes_a_400_with_INVALID_REQUEST()
    {
        var context = new ActionContext(new DefaultHttpContext(), new RouteData(), new ActionDescriptor());
        context.ModelState.AddModelError("Title", "The Title field is required.");

        var result = InvalidModelStateResponse.Create(context);

        var bad = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        bad.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        var body = bad.Value.Should().BeOfType<ApiErrorResponse>().Subject;
        body.Errors.Should().ContainSingle()
            .Which.Should().Be(new ApiError(ErrorCodes.InvalidRequest, "Title: The Title field is required."));
    }
}
