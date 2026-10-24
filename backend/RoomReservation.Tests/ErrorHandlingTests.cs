using System.Net;
using System.Text;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using RoomReservation.Api.Common;

namespace RoomReservation.Tests;

public enum Mood { Happy, Sad }

public sealed record EchoRequest(Mood Mood);

[ApiController]
[Route("test")]
public class ErrorProbeController : ControllerBase
{
    [HttpGet("boom")]
    public IActionResult Boom() => throw new InvalidOperationException("secret internal detail");

    [HttpPost("echo")]
    public IActionResult Echo(EchoRequest request) => Ok(request);
}

public class ErrorHandlingTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public ErrorHandlingTests(WebApplicationFactory<Program> factory)
    {
        // The probe controller lives in the test assembly, so register it as an extra application part.
        _client = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            services.AddControllers().AddApplicationPart(typeof(ErrorProbeController).Assembly))).CreateClient();
    }

    private static StringContent Json(string body) => new(body, Encoding.UTF8, "application/json");

    [Fact]
    public async Task Unhandled_exception_returns_a_500_in_the_standard_body_without_details()
    {
        var response = await _client.GetAsync("/test/boom");

        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
        var text = await response.Content.ReadAsStringAsync();
        text.Should().Contain(ErrorCodes.InternalError).And.NotContain("secret internal detail");
    }

    [Fact]
    public async Task Malformed_json_returns_400_without_technical_details()
    {
        var response = await _client.PostAsync("/test/echo", Json("{ \"mood\": "));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var text = await response.Content.ReadAsStringAsync();
        text.Should().Contain(ErrorCodes.InvalidRequest)
            .And.NotContain("LineNumber").And.NotContain("System.Text.Json").And.NotContain("BytePosition");
    }

    [Theory]
    [InlineData("{ \"mood\": 99 }")]
    [InlineData("{ \"mood\": \"Furious\" }")]
    [InlineData("{ \"mood\": 1 }")]
    public async Task Unknown_or_numeric_enum_values_are_rejected_with_400(string body)
    {
        var response = await _client.PostAsync("/test/echo", Json(body));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).Should().Contain(ErrorCodes.InvalidRequest);
    }

    [Fact]
    public async Task Responses_carry_security_headers_and_api_responses_are_not_cached()
    {
        var response = await _client.GetAsync("/api/rooms");

        response.Headers.GetValues("X-Content-Type-Options").Should().Equal("nosniff");
        response.Headers.GetValues("X-Frame-Options").Should().Equal("DENY");
        response.Headers.GetValues("Referrer-Policy").Should().Equal("no-referrer");
        response.Headers.GetValues("Cache-Control").Should().Equal("no-store");
    }

    [Fact]
    public async Task A_hostile_property_name_is_not_echoed_in_the_error_message()
    {
        var response = await _client.PostAsync("/test/echo", Json("{ \"<script>alert(1)</script>\": 1, \"mood\": \"Furious\" }"));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).Should().NotContain("<script>");
    }

    [Fact]
    public async Task A_valid_string_enum_is_accepted()
    {
        var response = await _client.PostAsync("/test/echo", Json("{ \"mood\": \"Sad\" }"));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync()).Should().Contain("Sad");
    }
}
