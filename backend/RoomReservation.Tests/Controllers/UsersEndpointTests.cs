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

public class UsersEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private static readonly TimeSpan Hour = TimeSpan.FromHours(1);

    private readonly WebApplicationFactory<Program> _factory;

    public UsersEndpointTests(WebApplicationFactory<Program> factory) => _factory = factory;

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

    private static async Task<List<JsonElement>> UsersAsync(HttpClient client)
    {
        var response = await client.GetAsync("/api/users");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return json.RootElement.EnumerateArray().Select(user => user.Clone()).ToList();
    }

    [Fact]
    public async Task Lists_users_with_the_documented_shape_ordered_by_last_name()
    {
        var room = TestDb.Room("Atlas");
        var ann = TestDb.User("Ann", "Lee");
        var bob = TestDb.User("Bob", "Kim");
        var (client, _) = NewApp(room, ann, bob,
            TestDb.Reservation(room, ann, TestDb.Now + Hour, Hour),
            TestDb.Reservation(room, ann, TestDb.Now + Hour * 3, Hour),
            TestDb.Reservation(room, ann, TestDb.Now + Hour * 5, Hour, ReservationStatus.Cancelled));

        var users = await UsersAsync(client);

        users.Select(u => u.GetProperty("lastName").GetString()).Should().Equal("Kim", "Lee");
        var annJson = users.Single(u => u.GetProperty("id").GetGuid() == ann.Id);
        annJson.GetProperty("firstName").GetString().Should().Be("Ann");
        annJson.GetProperty("activeReservationCount").GetInt32().Should().Be(2);
        users.Single(u => u.GetProperty("id").GetGuid() == bob.Id).GetProperty("activeReservationCount").GetInt32().Should().Be(0);
    }

    [Fact]
    public async Task An_elapsed_booking_stops_counting_after_the_clock_advances()
    {
        var room = TestDb.Room("Atlas");
        var ann = TestDb.User();
        var (client, clock) = NewApp(room, ann, TestDb.Reservation(room, ann, TestDb.Now + Hour, Hour));

        (await UsersAsync(client)).Single().GetProperty("activeReservationCount").GetInt32().Should().Be(1);

        clock.Advance(Hour * 3);

        (await UsersAsync(client)).Single().GetProperty("activeReservationCount").GetInt32().Should().Be(0);
    }

    [Fact]
    public async Task No_users_gives_an_empty_array()
    {
        var (client, _) = NewApp();

        (await UsersAsync(client)).Should().BeEmpty();
    }
}
