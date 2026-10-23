using System.Net;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
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
    public void A_zero_limit_is_rejected_by_options_validation(string key)
    {
        // Same registration as Program.cs, resolved without a web host. Booting a host that is meant to fail
        // races with its own teardown and sometimes surfaces an ObjectDisposedException instead of the real error.
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["BookingRules:MaxDurationDays"] = "7",
                ["BookingRules:MaxHorizonDays"] = "90",
                ["UsageReport:MinBookingsToFlag"] = "4",
                ["UsageReport:UnderusedThresholdPercent"] = "50",
                [key] = "0"
            })
            .Build();
        var services = new ServiceCollection();
        services.AddOptions<BookingRulesOptions>()
            .Bind(configuration.GetSection(BookingRulesOptions.SectionName)).ValidateDataAnnotations();
        services.AddOptions<UsageReportOptions>()
            .Bind(configuration.GetSection(UsageReportOptions.SectionName)).ValidateDataAnnotations();
        using var provider = services.BuildServiceProvider();

        var resolve = () =>
        {
            _ = provider.GetRequiredService<IOptions<BookingRulesOptions>>().Value;
            _ = provider.GetRequiredService<IOptions<UsageReportOptions>>().Value;
        };

        resolve.Should().Throw<OptionsValidationException>().WithMessage($"*{key.Split(':')[1]}*");
    }
}
