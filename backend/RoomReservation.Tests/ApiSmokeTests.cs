using System.Net;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using RoomReservation.Api.Configuration;

namespace RoomReservation.Tests;

public class ApiSmokeTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public ApiSmokeTests(WebApplicationFactory<Program> factory) => _factory = factory;

    [Fact]
    public async Task Swagger_document_is_served()
    {
        var client = _factory.WithWebHostBuilder(b => b.UseEnvironment("Development")).CreateClient();

        var response = await client.GetAsync("/swagger/v1/swagger.json");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public void Booking_limits_are_bound_from_configuration()
    {
        var rules = _factory.Services.GetRequiredService<IOptions<BookingRulesOptions>>().Value;

        // These literals intentionally mirror appsettings.json: the test proves the binding, not the rule.
        rules.MaxDurationDays.Should().Be(7);
        rules.MaxHorizonDays.Should().Be(90);
    }

    [Fact]
    public void Usage_report_thresholds_are_bound_from_configuration()
    {
        var report = _factory.Services.GetRequiredService<IOptions<UsageReportOptions>>().Value;

        report.MinBookingsToFlag.Should().Be(4);
        report.UnderusedThresholdPercent.Should().Be(50);
    }

    [Theory]
    [InlineData("BookingRules:MaxDurationDays")]
    [InlineData("BookingRules:MaxHorizonDays")]
    [InlineData("UsageReport:MinBookingsToFlag")]
    [InlineData("UsageReport:UnderusedThresholdPercent")]
    public void Startup_fails_when_a_limit_is_zero(string key)
    {
        // A dedicated factory per case: the shared fixture host must stay healthy for the other tests.
        using var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder => builder.UseSetting(key, "0"));

        var start = () => factory.CreateClient();

        start.Should().Throw<OptionsValidationException>().WithMessage($"*{key.Split(':')[1]}*");
    }
}
