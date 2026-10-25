using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using RoomReservation.Api.Common;
using RoomReservation.Api.Data;
using RoomReservation.Api.Domain;
using RoomReservation.Tests.Repositories;

namespace RoomReservation.Tests.Controllers;

public class RoomsEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public RoomsEndpointTests(WebApplicationFactory<Program> factory) => _factory = factory;

    // A fresh named database per test, filled with a small known set of rooms (the host runs as Development, so the seeder is switched off).
    private async Task<HttpClient> ClientAsync()
    {
        var factory = _factory.WithWebHostBuilder(builder => builder
            .UseSetting("Database:Name", TestDb.NewName())
            .UseSetting("Seed:Enabled", "false"));
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Rooms.AddRange(
                TestDb.Room("Small", capacity: 4, floor: 1, Equipment.Whiteboard),
                TestDb.Room("Medium", capacity: 10, floor: 2, Equipment.TvScreen | Equipment.VideoConferencing),
                TestDb.Room("Large", capacity: 30, floor: 2, Equipment.Projector | Equipment.TvScreen),
                TestDb.Room("Closed", capacity: 8, floor: 0, Equipment.None, isActive: false),
                TestDb.Room("Cellar", capacity: 6, floor: -1, Equipment.None));
            await db.SaveChangesAsync();
        }

        return factory.CreateClient();
    }

    private static async Task<List<string>> NamesAsync(HttpResponseMessage response)
    {
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return json.RootElement.GetProperty("items").EnumerateArray().Select(room => room.GetProperty("name").GetString()!).ToList();
    }

    [Fact]
    public async Task Lists_all_rooms_ordered_by_floor_then_name_with_the_documented_shape()
    {
        var client = await ClientAsync();

        var response = await client.GetAsync("/api/rooms");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var rooms = json.RootElement.GetProperty("items").EnumerateArray().ToList();
        rooms.Select(r => r.GetProperty("name").GetString()).Should().Equal("Cellar", "Closed", "Small", "Large", "Medium");
        var large = rooms.Single(r => r.GetProperty("name").GetString() == "Large");
        large.EnumerateObject().Select(p => p.Name).Should().Equal("id", "name", "capacity", "floor", "equipment", "isActive");
        large.GetProperty("equipment").EnumerateArray().Select(e => e.GetString()).Should().Equal("Projector", "TvScreen");
        large.GetProperty("capacity").GetInt32().Should().Be(30);
        large.GetProperty("isActive").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task Inactive_rooms_are_returned()
    {
        var client = await ClientAsync();

        (await NamesAsync(await client.GetAsync("/api/rooms"))).Should().Contain("Closed");
    }

    [Fact]
    public async Task Min_capacity_filter_is_inclusive()
    {
        var client = await ClientAsync();

        (await NamesAsync(await client.GetAsync("/api/rooms?minCapacity=10"))).Should().Equal("Large", "Medium");
    }

    [Fact]
    public async Task Floor_filter_works_for_zero_and_negative_floors()
    {
        var client = await ClientAsync();

        (await NamesAsync(await client.GetAsync("/api/rooms?floor=0"))).Should().Equal("Closed");
        (await NamesAsync(await client.GetAsync("/api/rooms?floor=-1"))).Should().Equal("Cellar");
        (await NamesAsync(await client.GetAsync("/api/rooms?floor=2"))).Should().Equal("Large", "Medium");
    }

    [Fact]
    public async Task Equipment_filter_requires_every_listed_item_and_ignores_case_and_spaces()
    {
        var client = await ClientAsync();

        (await NamesAsync(await client.GetAsync("/api/rooms?equipment=tvscreen"))).Should().Equal("Large", "Medium");
        (await NamesAsync(await client.GetAsync("/api/rooms?equipment=TvScreen,%20projector"))).Should().Equal("Large");
        (await NamesAsync(await client.GetAsync("/api/rooms?equipment=Projector,Projector"))).Should().Equal("Large");
    }

    [Fact]
    public async Task Blank_equipment_does_not_filter()
    {
        var client = await ClientAsync();

        (await NamesAsync(await client.GetAsync("/api/rooms?equipment="))).Should().HaveCount(5);
    }

    [Fact]
    public async Task Filters_are_combined()
    {
        var client = await ClientAsync();

        (await NamesAsync(await client.GetAsync("/api/rooms?minCapacity=5&floor=2&equipment=VideoConferencing")))
            .Should().Equal("Medium");
        (await NamesAsync(await client.GetAsync("/api/rooms?minCapacity=20&floor=1"))).Should().BeEmpty();
    }

    [Theory]
    [InlineData("equipment=Laser")]
    [InlineData("equipment=1")]
    [InlineData("equipment=None")]
    [InlineData("equipment=Projector,Laser")]
    [InlineData("minCapacity=-1")]
    [InlineData("minCapacity=abc")]
    [InlineData("minCapacity=99999999999")]
    [InlineData("floor=abc")]
    [InlineData("minCapacity=10001")]
    [InlineData("floor=201")]
    [InlineData("floor=-11")]
    public async Task Invalid_query_values_return_400_invalid_request(string query)
    {
        var client = await ClientAsync();

        var response = await client.GetAsync($"/api/rooms?{query}");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadFromJsonAsync<ApiErrorResponse>();
        body!.Errors.Should().NotBeEmpty().And.OnlyContain(e => e.Code == ErrorCodes.InvalidRequest && e.Message.Length > 0);
    }

    [Fact]
    public async Task Swagger_lists_the_rooms_endpoint_with_its_query_parameters()
    {
        var client = _factory.WithWebHostBuilder(b => b.UseEnvironment("Development")).CreateClient();

        var response = await client.GetAsync("/swagger/v1/swagger.json");

        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var parameters = json.RootElement.GetProperty("paths").GetProperty("/api/Rooms").GetProperty("get").GetProperty("parameters");
        parameters.EnumerateArray().Select(p => p.GetProperty("name").GetString())
            .Should().BeEquivalentTo("MinCapacity", "Equipment", "Floor", "Page", "PageSize");
    }
}
