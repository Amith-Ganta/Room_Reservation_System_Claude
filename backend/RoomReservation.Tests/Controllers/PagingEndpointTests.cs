using System.Net;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using RoomReservation.Api.Common;
using RoomReservation.Api.Data;
using RoomReservation.Api.Domain;
using RoomReservation.Tests.Repositories;

namespace RoomReservation.Tests.Controllers;

/// <summary>The paging contract shared by <c>GET /api/rooms</c>, <c>/api/users</c> and <c>/api/reservations</c> (spec 14).</summary>
public class PagingEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private static readonly TimeSpan Hour = TimeSpan.FromHours(1);

    private readonly WebApplicationFactory<Program> _factory;

    public PagingEndpointTests(WebApplicationFactory<Program> factory) => _factory = factory;

    /// <summary>Starts the app on a fresh database with the given paging limits and seed rows.</summary>
    private HttpClient NewApp(int defaultSize, int maxSize, params object[] entities)
    {
        var clock = new FakeTimeProvider(TestDb.Now);
        var factory = _factory.WithWebHostBuilder(builder => builder
            .UseSetting("Database:Name", TestDb.NewName())
            .UseSetting("Seed:Enabled", "false")
            .UseSetting("Paging:DefaultPageSize", defaultSize.ToString())
            .UseSetting("Paging:MaxPageSize", maxSize.ToString())
            .ConfigureTestServices(services => services.AddSingleton<TimeProvider>(clock)));
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.AddRange(entities);
            db.SaveChanges();
        }

        return factory.CreateClient();
    }

    private static Room[] Rooms(int count) =>
        Enumerable.Range(1, count).Select(i => TestDb.Room($"Room {i:00}", floor: i % 3)).ToArray();

    /// <summary>Seed rows for one list endpoint: rooms, users, or reservations (with the room and user they need).</summary>
    private static object[] Seed(string url, int count)
    {
        if (url == "/api/users")
        {
            return Enumerable.Range(1, count).Select(i => (object)TestDb.User($"User{i:00}", "Lee")).ToArray();
        }

        if (url == "/api/reservations")
        {
            var room = TestDb.Room("Atlas");
            var ann = TestDb.User();
            var reservations = Enumerable.Range(1, count)
                .Select(i => (object)TestDb.Reservation(room, ann, TestDb.Now + Hour * (i * 3), Hour));
            return [room, ann, .. reservations];
        }

        return Rooms(count);
    }

    private static async Task<JsonElement> GetOkAsync(HttpClient client, string url)
    {
        var response = await client.GetAsync(url);
        response.StatusCode.Should().Be(HttpStatusCode.OK, url);
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.Clone();
    }

    private static string[] Ids(JsonElement page) =>
        page.GetProperty("items").EnumerateArray().Select(item => item.GetProperty("id").GetString()!).ToArray();

    // ---------- envelope and sizes ----------

    [Theory]
    [InlineData("/api/rooms")]
    [InlineData("/api/users")]
    [InlineData("/api/reservations")]
    public async Task A_list_is_wrapped_in_the_paging_envelope(string url)
    {
        var client = NewApp(2, 10, Seed(url, 5));

        var page = await GetOkAsync(client, url);

        page.GetProperty("items").GetArrayLength().Should().Be(2);
        page.GetProperty("page").GetInt32().Should().Be(1);
        page.GetProperty("pageSize").GetInt32().Should().Be(2);
        page.GetProperty("totalCount").GetInt32().Should().Be(5);
        page.GetProperty("totalPages").GetInt32().Should().Be(3);
    }

    [Theory]
    [InlineData("/api/rooms")]
    [InlineData("/api/users")]
    [InlineData("/api/reservations")]
    public async Task An_empty_list_has_zero_total_pages_on_every_endpoint(string url)
    {
        var client = NewApp(2, 10);

        var page = await GetOkAsync(client, url);

        page.GetProperty("items").GetArrayLength().Should().Be(0);
        page.GetProperty("totalCount").GetInt32().Should().Be(0);
        page.GetProperty("totalPages").GetInt32().Should().Be(0);
    }

    [Theory]
    [InlineData("/api/rooms")]
    [InlineData("/api/users")]
    [InlineData("/api/reservations")]
    public async Task The_default_page_size_comes_from_configuration(string url)
    {
        var client = NewApp(3, 10, Seed(url, 7));

        var page = await GetOkAsync(client, url);

        page.GetProperty("pageSize").GetInt32().Should().Be(3);
        page.GetProperty("items").GetArrayLength().Should().Be(3);
    }

    [Theory]
    [InlineData("/api/rooms")]
    [InlineData("/api/users")]
    [InlineData("/api/reservations")]
    public async Task A_page_size_above_the_maximum_is_clamped_not_rejected(string url)
    {
        var client = NewApp(2, 4, Seed(url, 9));

        var page = await GetOkAsync(client, $"{url}?pageSize=1000");

        page.GetProperty("pageSize").GetInt32().Should().Be(4);
        page.GetProperty("items").GetArrayLength().Should().Be(4);
        page.GetProperty("totalPages").GetInt32().Should().Be(3);
    }

    [Theory]
    [InlineData("/api/rooms")]
    [InlineData("/api/users")]
    [InlineData("/api/reservations")]
    public async Task A_smaller_page_size_than_the_default_is_honoured(string url)
    {
        var client = NewApp(5, 10, Seed(url, 9));

        var page = await GetOkAsync(client, $"{url}?pageSize=2&page=2");

        page.GetProperty("page").GetInt32().Should().Be(2);
        page.GetProperty("pageSize").GetInt32().Should().Be(2);
        page.GetProperty("items").GetArrayLength().Should().Be(2);
    }

    // ---------- invalid values ----------

    [Theory]
    [InlineData("/api/rooms", "page=0")]
    [InlineData("/api/rooms", "page=-1")]
    [InlineData("/api/rooms", "page=abc")]
    [InlineData("/api/rooms", "page=100001")]
    [InlineData("/api/rooms", "pageSize=0")]
    [InlineData("/api/rooms", "pageSize=-5")]
    [InlineData("/api/rooms", "pageSize=abc")]
    [InlineData("/api/rooms", "pageSize=99999999999")]
    [InlineData("/api/users", "page=0")]
    [InlineData("/api/users", "pageSize=0")]
    [InlineData("/api/users", "pageSize=abc")]
    [InlineData("/api/reservations", "page=0")]
    [InlineData("/api/reservations", "pageSize=-1")]
    [InlineData("/api/reservations", "pageSize=abc")]
    public async Task Invalid_paging_values_are_a_400_with_the_standard_error_body(string path, string query)
    {
        var client = NewApp(2, 10, Rooms(3));

        var response = await client.GetAsync($"{path}?{query}");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var errors = json.RootElement.GetProperty("errors").EnumerateArray().ToList();
        errors.Should().NotBeEmpty();
        errors.Should().OnlyContain(e => e.GetProperty("code").GetString() == ErrorCodes.InvalidRequest);
    }

    [Theory]
    [InlineData("/api/rooms")]
    [InlineData("/api/users")]
    [InlineData("/api/reservations")]
    public async Task A_page_past_the_end_is_a_200_with_no_items_and_the_real_totals(string url)
    {
        var client = NewApp(2, 10, Seed(url, 5));

        var page = await GetOkAsync(client, $"{url}?page=50");

        page.GetProperty("items").GetArrayLength().Should().Be(0);
        page.GetProperty("page").GetInt32().Should().Be(50);
        page.GetProperty("totalCount").GetInt32().Should().Be(5);
        page.GetProperty("totalPages").GetInt32().Should().Be(3);
    }

    // ---------- filters and order ----------

    [Fact]
    public async Task Filters_apply_before_paging_so_they_change_the_total()
    {
        var rooms = new[]
        {
            TestDb.Room("A", floor: 1), TestDb.Room("B", floor: 1), TestDb.Room("C", floor: 1),
            TestDb.Room("D", floor: 2), TestDb.Room("E", floor: 2)
        };
        var client = NewApp(2, 10, rooms);

        var all = await GetOkAsync(client, "/api/rooms");
        var floorOne = await GetOkAsync(client, "/api/rooms?floor=1");
        var floorTwoSecondPage = await GetOkAsync(client, "/api/rooms?floor=2&page=2");

        all.GetProperty("totalCount").GetInt32().Should().Be(5);
        floorOne.GetProperty("totalCount").GetInt32().Should().Be(3);
        floorOne.GetProperty("totalPages").GetInt32().Should().Be(2);
        floorTwoSecondPage.GetProperty("totalCount").GetInt32().Should().Be(2);
        floorTwoSecondPage.GetProperty("items").GetArrayLength().Should().Be(0);
    }

    [Fact]
    public async Task Room_pages_do_not_overlap_and_together_cover_every_room()
    {
        var rooms = Rooms(11);
        var client = NewApp(4, 10, rooms);

        var seen = new List<string>();
        for (var page = 1; page <= 3; page++)
        {
            seen.AddRange(Ids(await GetOkAsync(client, $"/api/rooms?page={page}")));
        }

        seen.Should().OnlyHaveUniqueItems().And.BeEquivalentTo(rooms.Select(r => r.Id.ToString()));
    }

    [Fact]
    public async Task Rooms_that_tie_on_floor_and_name_keep_a_stable_order_across_pages()
    {
        var rooms = Enumerable.Range(0, 6).Select(_ => TestDb.Room("Twin", floor: 1)).ToArray();
        var client = NewApp(2, 10, rooms);

        var firstPass = new List<string>();
        var secondPass = new List<string>();
        for (var page = 1; page <= 3; page++)
        {
            firstPass.AddRange(Ids(await GetOkAsync(client, $"/api/rooms?page={page}")));
        }

        for (var page = 1; page <= 3; page++)
        {
            secondPass.AddRange(Ids(await GetOkAsync(client, $"/api/rooms?page={page}")));
        }

        firstPass.Should().OnlyHaveUniqueItems().And.HaveCount(6);
        secondPass.Should().Equal(firstPass);
    }

    [Fact]
    public async Task Users_that_tie_on_both_names_keep_a_stable_order_across_pages()
    {
        var users = Enumerable.Range(0, 6).Select(_ => TestDb.User("Sam", "Twin")).ToArray();
        var client = NewApp(2, 10, users);

        var firstPass = new List<string>();
        var secondPass = new List<string>();
        for (var page = 1; page <= 3; page++)
        {
            firstPass.AddRange(Ids(await GetOkAsync(client, $"/api/users?page={page}")));
        }

        for (var page = 1; page <= 3; page++)
        {
            secondPass.AddRange(Ids(await GetOkAsync(client, $"/api/users?page={page}")));
        }

        firstPass.Should().OnlyHaveUniqueItems().And.BeEquivalentTo(users.Select(u => u.Id.ToString()));
        secondPass.Should().Equal(firstPass);
    }

    [Fact]
    public async Task Reservations_that_tie_on_start_keep_a_stable_order_across_pages()
    {
        var room = TestDb.Room("Atlas");
        var ann = TestDb.User();
        var reservations = Enumerable.Range(0, 6)
            .Select(_ => TestDb.Reservation(room, ann, TestDb.Now + Hour * 3, Hour))
            .ToArray();
        var client = NewApp(2, 10, [room, ann, .. reservations]);

        var firstPass = new List<string>();
        var secondPass = new List<string>();
        for (var page = 1; page <= 3; page++)
        {
            firstPass.AddRange(Ids(await GetOkAsync(client, $"/api/reservations?page={page}")));
        }

        for (var page = 1; page <= 3; page++)
        {
            secondPass.AddRange(Ids(await GetOkAsync(client, $"/api/reservations?page={page}")));
        }

        firstPass.Should().OnlyHaveUniqueItems().And.BeEquivalentTo(reservations.Select(r => r.Id.ToString()));
        secondPass.Should().Equal(firstPass);
    }

    [Fact]
    public async Task User_pages_follow_last_name_then_first_name_without_overlap()
    {
        var users = new[]
        {
            TestDb.User("Cara", "Young"), TestDb.User("Ann", "Lee"), TestDb.User("Bob", "Lee"),
            TestDb.User("Dan", "Adams"), TestDb.User("Eve", "Zane")
        };
        var client = NewApp(2, 10, users);

        var names = new List<string>();
        for (var page = 1; page <= 3; page++)
        {
            var body = await GetOkAsync(client, $"/api/users?page={page}");
            names.AddRange(body.GetProperty("items").EnumerateArray()
                .Select(u => $"{u.GetProperty("firstName").GetString()} {u.GetProperty("lastName").GetString()}"));
        }

        names.Should().Equal("Dan Adams", "Ann Lee", "Bob Lee", "Cara Young", "Eve Zane");
    }

    [Fact]
    public async Task Reservation_pages_are_newest_start_first_and_cover_every_booking()
    {
        var room = TestDb.Room("Atlas");
        var ann = TestDb.User();
        var reservations = Enumerable.Range(1, 5)
            .Select(i => TestDb.Reservation(room, ann, TestDb.Now + Hour * (i * 3), Hour))
            .ToArray();
        var client = NewApp(2, 10, [room, ann, .. reservations]);

        var seen = new List<string>();
        for (var page = 1; page <= 3; page++)
        {
            seen.AddRange(Ids(await GetOkAsync(client, $"/api/reservations?page={page}")));
        }

        seen.Should().Equal(reservations.Reverse().Select(r => r.Id.ToString()));
    }

    [Fact]
    public async Task Reservation_filters_apply_before_paging()
    {
        var room = TestDb.Room("Atlas");
        var ann = TestDb.User("Ann", "Lee");
        var bob = TestDb.User("Bob", "Kim");
        var client = NewApp(2, 10, room, ann, bob,
            TestDb.Reservation(room, ann, TestDb.Now + Hour * 2, Hour),
            TestDb.Reservation(room, ann, TestDb.Now + Hour * 4, Hour),
            TestDb.Reservation(room, ann, TestDb.Now + Hour * 6, Hour),
            TestDb.Reservation(room, bob, TestDb.Now + Hour * 8, Hour));

        var page = await GetOkAsync(client, $"/api/reservations?userId={ann.Id}");

        page.GetProperty("totalCount").GetInt32().Should().Be(3);
        page.GetProperty("items").GetArrayLength().Should().Be(2);
        page.GetProperty("totalPages").GetInt32().Should().Be(2);
    }

    // ---------- other readers of the lists ----------

    [Fact]
    public async Task The_usage_report_still_lists_every_room_when_rooms_outnumber_a_page()
    {
        var rooms = Rooms(7);
        var client = NewApp(2, 2, rooms);

        var report = await GetOkAsync(client, "/api/reports/usage");

        report.GetProperty("rooms").GetArrayLength().Should().Be(7);
    }

    [Fact]
    public async Task The_active_reservation_count_of_a_user_survives_paging()
    {
        var room = TestDb.Room("Atlas");
        var ann = TestDb.User("Ann", "Lee");
        var bob = TestDb.User("Bob", "Kim");
        var client = NewApp(1, 10, room, ann, bob,
            TestDb.Reservation(room, ann, TestDb.Now + Hour * 2, Hour),
            TestDb.Reservation(room, ann, TestDb.Now + Hour * 4, Hour));

        var first = await GetOkAsync(client, "/api/users?page=1");
        var second = await GetOkAsync(client, "/api/users?page=2");

        first.GetProperty("items")[0].GetProperty("activeReservationCount").GetInt32().Should().Be(0);
        second.GetProperty("items")[0].GetProperty("activeReservationCount").GetInt32().Should().Be(2);
    }
}
