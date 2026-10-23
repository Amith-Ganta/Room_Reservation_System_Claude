using System.Net;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using RoomReservation.Api.Data;
using RoomReservation.Api.Domain;
using RoomReservation.Tests.Repositories;

namespace RoomReservation.Tests.Controllers;

public class ReportsEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private static readonly TimeSpan Hour = TimeSpan.FromHours(1);

    private readonly WebApplicationFactory<Program> _factory;

    public ReportsEndpointTests(WebApplicationFactory<Program> factory) => _factory = factory;

    private (HttpClient Client, FakeTimeProvider Clock) NewApp(params object[] entities)
    {
        var clock = new FakeTimeProvider(TestDb.Now);
        var factory = _factory.WithWebHostBuilder(builder => builder
            .UseSetting("Database:Name", TestDb.NewName())
            .UseSetting("Seed:Enabled", "false")
            .ConfigureTestServices(services => services.AddSingleton<TimeProvider>(clock)));
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.AddRange(entities);
            db.SaveChanges();
        }

        return (factory.CreateClient(), clock);
    }

    private static async Task<JsonDocument> GetJsonAsync(HttpClient client, string url, HttpStatusCode expected)
    {
        var response = await client.GetAsync(url);
        response.StatusCode.Should().Be(expected);
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync());
    }

    private static string Encode(DateTimeOffset value) => Uri.EscapeDataString(value.ToString("O"));

    [Fact]
    public async Task Returns_the_documented_shape_with_the_flag_and_the_thresholds()
    {
        var boardroom = TestDb.Room("Boardroom");
        var quiet = TestDb.Room("Atlas");
        var ann = TestDb.User();
        var (client, _) = NewApp(boardroom, quiet, ann,
            TestDb.Reservation(boardroom, ann, TestDb.Now - Hour * 20, Hour, ReservationStatus.Cancelled),
            TestDb.Reservation(boardroom, ann, TestDb.Now - Hour * 18, Hour, ReservationStatus.Cancelled),
            TestDb.Reservation(boardroom, ann, TestDb.Now - Hour * 16, Hour, ReservationStatus.Cancelled),
            TestDb.Reservation(boardroom, ann, TestDb.Now - Hour * 14, Hour, ReservationStatus.Cancelled),
            TestDb.Reservation(quiet, ann, TestDb.Now + Hour * 2, Hour));

        using var json = await GetJsonAsync(client, "/api/reports/usage", HttpStatusCode.OK);
        var root = json.RootElement;

        root.GetProperty("minBookingsToFlag").GetInt32().Should().Be(4);
        root.GetProperty("underusedThresholdPercent").GetInt32().Should().Be(50);
        root.GetProperty("from").ValueKind.Should().Be(JsonValueKind.Null);
        root.GetProperty("to").ValueKind.Should().Be(JsonValueKind.Null);
        root.GetProperty("generatedAt").GetDateTimeOffset().Should().Be(TestDb.Now);

        var rooms = root.GetProperty("rooms").EnumerateArray().ToList();
        rooms.Select(r => r.GetProperty("roomName").GetString()).Should().Equal("Boardroom", "Atlas");

        var first = rooms[0];
        first.GetProperty("roomId").GetGuid().Should().Be(boardroom.Id);
        first.GetProperty("floor").GetInt32().Should().Be(boardroom.Floor);
        first.GetProperty("totalBookings").GetInt32().Should().Be(4);
        first.GetProperty("completedBookings").GetInt32().Should().Be(0);
        first.GetProperty("cancelledBookings").GetInt32().Should().Be(4);
        first.GetProperty("upcomingBookings").GetInt32().Should().Be(0);
        first.GetProperty("plannedHours").GetDecimal().Should().Be(4m);
        first.GetProperty("usedHours").GetDecimal().Should().Be(0m);
        first.GetProperty("utilizationPercent").GetDecimal().Should().Be(0m);
        first.GetProperty("isOftenBookedRarelyUsed").GetBoolean().Should().BeTrue();

        var second = rooms[1];
        second.GetProperty("upcomingBookings").GetInt32().Should().Be(1);
        second.GetProperty("utilizationPercent").ValueKind.Should().Be(JsonValueKind.Null);
        second.GetProperty("isOftenBookedRarelyUsed").GetBoolean().Should().BeFalse();
    }

    [Fact]
    public async Task No_rooms_gives_an_empty_list()
    {
        var (client, _) = NewApp();

        using var json = await GetJsonAsync(client, "/api/reports/usage", HttpStatusCode.OK);

        json.RootElement.GetProperty("rooms").GetArrayLength().Should().Be(0);
    }

    [Fact]
    public async Task The_window_filters_reservations_by_their_start_and_is_echoed()
    {
        var room = TestDb.Room("Atlas");
        var ann = TestDb.User();
        var from = TestDb.Now.AddDays(-2);
        var to = TestDb.Now.AddDays(-1);
        var (client, _) = NewApp(room, ann,
            TestDb.Reservation(room, ann, TestDb.Now.AddDays(-3), Hour, ReservationStatus.Completed),
            TestDb.Reservation(room, ann, from, Hour, ReservationStatus.Completed),
            TestDb.Reservation(room, ann, to, Hour, ReservationStatus.Completed),
            TestDb.Reservation(room, ann, TestDb.Now.AddDays(1), Hour));

        using var json = await GetJsonAsync(client, $"/api/reports/usage?from={Encode(from)}&to={Encode(to)}", HttpStatusCode.OK);
        var root = json.RootElement;

        root.GetProperty("rooms")[0].GetProperty("totalBookings").GetInt32().Should().Be(2);
        root.GetProperty("from").GetDateTimeOffset().Should().Be(from);
        root.GetProperty("to").GetDateTimeOffset().Should().Be(to);
    }

    [Fact]
    public async Task From_equal_to_to_is_accepted()
    {
        var (client, _) = NewApp();
        var instant = Encode(TestDb.Now);

        using var json = await GetJsonAsync(client, $"/api/reports/usage?from={instant}&to={instant}", HttpStatusCode.OK);

        json.RootElement.GetProperty("rooms").GetArrayLength().Should().Be(0);
    }

    [Fact]
    public async Task From_later_than_to_is_a_bad_request()
    {
        var (client, _) = NewApp();

        using var json = await GetJsonAsync(
            client, $"/api/reports/usage?from={Encode(TestDb.Now)}&to={Encode(TestDb.Now.AddDays(-1))}", HttpStatusCode.BadRequest);

        json.RootElement.GetProperty("errors")[0].GetProperty("code").GetString().Should().Be("INVALID_REQUEST");
    }

    [Theory]
    [InlineData("from=not-a-date")]
    [InlineData("to=2026-06-01")]
    public async Task A_malformed_or_offsetless_date_is_a_bad_request(string query)
    {
        var (client, _) = NewApp();

        using var json = await GetJsonAsync(client, $"/api/reports/usage?{query}", HttpStatusCode.BadRequest);

        json.RootElement.GetProperty("errors")[0].GetProperty("code").GetString().Should().Be("INVALID_REQUEST");
    }

    [Fact]
    public async Task An_elapsed_booking_is_reported_as_completed_after_the_clock_advances()
    {
        var room = TestDb.Room("Atlas");
        var ann = TestDb.User();
        var (client, clock) = NewApp(room, ann, TestDb.Reservation(room, ann, TestDb.Now + Hour, Hour));

        using (var before = await GetJsonAsync(client, "/api/reports/usage", HttpStatusCode.OK))
        {
            before.RootElement.GetProperty("rooms")[0].GetProperty("upcomingBookings").GetInt32().Should().Be(1);
        }

        clock.Advance(Hour * 3);

        using var after = await GetJsonAsync(client, "/api/reports/usage", HttpStatusCode.OK);
        var row = after.RootElement.GetProperty("rooms")[0];
        row.GetProperty("upcomingBookings").GetInt32().Should().Be(0);
        row.GetProperty("completedBookings").GetInt32().Should().Be(1);
        row.GetProperty("usedHours").GetDecimal().Should().Be(1m);
    }
}
