using System.Net;
using System.Net.Http.Json;
using System.Text;
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

public class ReservationsEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private static readonly TimeSpan Hour = TimeSpan.FromHours(1);
    private static readonly DateTimeOffset Now = TestDb.Now;

    private readonly WebApplicationFactory<Program> _factory;

    public ReservationsEndpointTests(WebApplicationFactory<Program> factory) => _factory = factory;

    /// <summary>One isolated app: a fixed clock, rooms "Main" (10 seats), "Small" (4), "Closed" (inactive), users Ann and Bob.</summary>
    private sealed class Env
    {
        public required HttpClient Client { get; init; }
        public required WebApplicationFactory<Program> Factory { get; init; }
        public required Room Main { get; init; }
        public required Room Small { get; init; }
        public required Room Closed { get; init; }
        public required User Ann { get; init; }
        public required User Bob { get; init; }

        public async Task SeedAsync(params Reservation[] reservations)
        {
            using var scope = Factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Reservations.AddRange(reservations);
            await db.SaveChangesAsync();
        }

        public object Body(Room room, User user, DateTimeOffset start, DateTimeOffset end, int attendees = 2, string title = "Planning") =>
            new { roomId = room.Id, userId = user.Id, title, start, end, attendeeCount = attendees };

        public Reservation Make(Room room, User user, DateTimeOffset start, TimeSpan duration,
            ReservationStatus status = ReservationStatus.Booked, DateTimeOffset? endedAt = null) =>
            TestDb.Reservation(room, user, start, duration, status, endedAt);

        public async Task<(HttpStatusCode Status, JsonElement Json)> SendAsync(HttpMethod method, string url, object? body = null)
        {
            using var request = new HttpRequestMessage(method, url);
            if (body is not null)
            {
                request.Content = JsonContent.Create(body);
            }

            var response = await Client.SendAsync(request);
            var text = await response.Content.ReadAsStringAsync();
            return (response.StatusCode, text.Length == 0 ? default : JsonDocument.Parse(text).RootElement.Clone());
        }

        public Task<(HttpStatusCode Status, JsonElement Json)> PostAsync(object body) =>
            SendAsync(HttpMethod.Post, "/api/reservations", body);
    }

    private async Task<Env> NewEnvAsync()
    {
        var clock = new FakeTimeProvider(Now);
        var factory = _factory.WithWebHostBuilder(builder => builder
            .UseSetting("Database:Name", TestDb.NewName())
            .UseSetting("Seed:Enabled", "false")
            .ConfigureTestServices(services => services.AddSingleton<TimeProvider>(clock)));
        var main = TestDb.Room("Main", capacity: 10);
        var small = TestDb.Room("Small", capacity: 4);
        var closed = TestDb.Room("Closed", capacity: 8, isActive: false);
        var ann = TestDb.User("Ann", "Lee");
        var bob = TestDb.User("Bob", "Kay");
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Rooms.AddRange(main, small, closed);
            db.Users.AddRange(ann, bob);
            await db.SaveChangesAsync();
        }

        return new Env { Client = factory.CreateClient(), Factory = factory, Main = main, Small = small, Closed = closed, Ann = ann, Bob = bob };
    }

    private static string[] Codes(JsonElement json) =>
        json.GetProperty("errors").EnumerateArray().Select(e => e.GetProperty("code").GetString()!).ToArray();

    private static void AssertReadableErrors(JsonElement json) =>
        json.GetProperty("errors").EnumerateArray()
            .Should().OnlyContain(e => !string.IsNullOrWhiteSpace(e.GetProperty("message").GetString()));

    private static string[] Ids(JsonElement list) =>
        list.GetProperty("items").EnumerateArray().Select(r => r.GetProperty("id").GetString()!).ToArray();

    // ---------- create ----------

    [Fact]
    public async Task Create_returns_201_with_location_and_the_documented_shape()
    {
        var env = await NewEnvAsync();
        var start = Now.AddDays(1);

        var response = await env.Client.PostAsJsonAsync("/api/reservations", env.Body(env.Main, env.Ann, start, start.AddHours(2), 3));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var body = json.RootElement;
        var id = body.GetProperty("id").GetGuid();
        response.Headers.Location!.AbsolutePath.Should().BeEquivalentTo($"/api/reservations/{id}");
        body.EnumerateObject().Select(p => p.Name).Should().Equal(
            "id", "roomId", "roomName", "userId", "organizerName", "title", "start", "end", "endedAt", "attendeeCount", "status");
        body.GetProperty("roomName").GetString().Should().Be("Main");
        body.GetProperty("organizerName").GetString().Should().Be("Ann Lee");
        body.GetProperty("status").GetString().Should().Be("Booked");
        body.GetProperty("endedAt").ValueKind.Should().Be(JsonValueKind.Null);
        body.GetProperty("attendeeCount").GetInt32().Should().Be(3);

        var fetched = await env.SendAsync(HttpMethod.Get, response.Headers.Location.AbsolutePath);
        fetched.Status.Should().Be(HttpStatusCode.OK);
        fetched.Json.GetProperty("id").GetGuid().Should().Be(id);
    }

    [Fact]
    public async Task Create_trims_the_title()
    {
        var env = await NewEnvAsync();
        var start = Now.AddDays(1);

        var created = await env.PostAsync(env.Body(env.Main, env.Ann, start, start.AddHours(1), title: "  Sprint review  "));

        created.Status.Should().Be(HttpStatusCode.Created);
        created.Json.GetProperty("title").GetString().Should().Be("Sprint review");
    }

    [Fact]
    public async Task R1_exactly_seven_days_is_allowed_and_one_minute_more_is_rejected()
    {
        var env = await NewEnvAsync();
        var start = Now.AddDays(1);

        var exact = await env.PostAsync(env.Body(env.Main, env.Ann, start, start.AddDays(7)));
        var tooLong = await env.PostAsync(env.Body(env.Small, env.Bob, start, start.AddDays(7).AddMinutes(1)));

        exact.Status.Should().Be(HttpStatusCode.Created);
        tooLong.Status.Should().Be(HttpStatusCode.UnprocessableEntity);
        Codes(tooLong.Json).Should().Equal(ErrorCodes.MaxDuration);
        AssertReadableErrors(tooLong.Json);
    }

    [Fact]
    public async Task R2_overlap_in_the_same_room_is_rejected_but_touching_edges_are_fine()
    {
        var env = await NewEnvAsync();
        var start = Now.AddDays(1);
        await env.SeedAsync(env.Make(env.Main, env.Ann, start, 2 * Hour));

        var overlap = await env.PostAsync(env.Body(env.Main, env.Bob, start.AddHours(1), start.AddHours(3)));
        var touching = await env.PostAsync(env.Body(env.Main, env.Bob, start.AddHours(2), start.AddHours(3)));

        overlap.Status.Should().Be(HttpStatusCode.UnprocessableEntity);
        Codes(overlap.Json).Should().Equal(ErrorCodes.RoomOverlap);
        AssertReadableErrors(overlap.Json);
        touching.Status.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task R3_organizer_cannot_be_in_two_rooms_at_once()
    {
        var env = await NewEnvAsync();
        var start = Now.AddDays(1);
        await env.SeedAsync(env.Make(env.Small, env.Ann, start, 2 * Hour));

        var response = await env.PostAsync(env.Body(env.Main, env.Ann, start.AddHours(1), start.AddHours(3)));

        response.Status.Should().Be(HttpStatusCode.UnprocessableEntity);
        Codes(response.Json).Should().Equal(ErrorCodes.UserOverlap);
        AssertReadableErrors(response.Json);
    }

    [Fact]
    public async Task R4_start_in_the_past_or_exactly_now_is_rejected()
    {
        var env = await NewEnvAsync();

        var past = await env.PostAsync(env.Body(env.Main, env.Ann, Now.AddHours(-1), Now.AddHours(1)));
        var now = await env.PostAsync(env.Body(env.Main, env.Ann, Now, Now.AddHours(1)));

        Codes(past.Json).Should().Equal(ErrorCodes.PastStart);
        Codes(now.Json).Should().Equal(ErrorCodes.PastStart);
        past.Status.Should().Be(HttpStatusCode.UnprocessableEntity);
        AssertReadableErrors(past.Json);
    }

    [Fact]
    public async Task R5_more_attendees_than_seats_is_rejected_but_equal_is_fine()
    {
        var env = await NewEnvAsync();
        var start = Now.AddDays(1);

        var over = await env.PostAsync(env.Body(env.Small, env.Ann, start, start.AddHours(1), attendees: 5));
        var equal = await env.PostAsync(env.Body(env.Small, env.Ann, start, start.AddHours(1), attendees: 4));

        over.Status.Should().Be(HttpStatusCode.UnprocessableEntity);
        Codes(over.Json).Should().Equal(ErrorCodes.CapacityExceeded);
        AssertReadableErrors(over.Json);
        equal.Status.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task R6_start_more_than_ninety_days_ahead_is_rejected_but_exactly_ninety_is_fine()
    {
        var env = await NewEnvAsync();
        var limit = Now.AddDays(90);

        var exact = await env.PostAsync(env.Body(env.Main, env.Ann, limit, limit.AddHours(1)));
        var beyond = await env.PostAsync(env.Body(env.Small, env.Bob, limit.AddMinutes(1), limit.AddHours(1), attendees: 2));

        exact.Status.Should().Be(HttpStatusCode.Created);
        beyond.Status.Should().Be(HttpStatusCode.UnprocessableEntity);
        Codes(beyond.Json).Should().Equal(ErrorCodes.BeyondHorizon);
        AssertReadableErrors(beyond.Json);
    }

    [Fact]
    public async Task R7_inactive_room_is_rejected()
    {
        var env = await NewEnvAsync();
        var start = Now.AddDays(1);

        var response = await env.PostAsync(env.Body(env.Closed, env.Ann, start, start.AddHours(1)));

        response.Status.Should().Be(HttpStatusCode.UnprocessableEntity);
        Codes(response.Json).Should().Equal(ErrorCodes.RoomInactive);
        AssertReadableErrors(response.Json);
    }

    [Fact]
    public async Task Several_broken_rules_are_all_reported_in_the_fixed_order()
    {
        var env = await NewEnvAsync();
        var start = Now.AddHours(-2);

        // Past start, 9 people in an 8-seat room, inactive room.
        var response = await env.PostAsync(env.Body(env.Closed, env.Ann, start, start.AddHours(1), attendees: 9));

        response.Status.Should().Be(HttpStatusCode.UnprocessableEntity);
        Codes(response.Json).Should().Equal(ErrorCodes.PastStart, ErrorCodes.CapacityExceeded, ErrorCodes.RoomInactive);
    }

    [Fact]
    public async Task Basic_input_errors_come_back_alone_without_rule_errors()
    {
        var env = await NewEnvAsync();
        var start = Now.AddHours(-5);

        var response = await env.PostAsync(env.Body(env.Closed, env.Ann, start, start.AddHours(-1), attendees: 0, title: "  "));

        response.Status.Should().Be(HttpStatusCode.UnprocessableEntity);
        Codes(response.Json).Should().BeEquivalentTo(
            ErrorCodes.InvalidTimeRange, ErrorCodes.TitleRequired, ErrorCodes.InvalidAttendees);
        AssertReadableErrors(response.Json);
    }

    [Fact]
    public async Task Missing_title_is_a_422_title_required()
    {
        var env = await NewEnvAsync();
        var start = Now.AddDays(1);

        var response = await env.PostAsync(new { roomId = env.Main.Id, userId = env.Ann.Id, start, end = start.AddHours(1), attendeeCount = 2 });

        response.Status.Should().Be(HttpStatusCode.UnprocessableEntity);
        Codes(response.Json).Should().Contain(ErrorCodes.TitleRequired);
    }

    [Fact]
    public async Task Unknown_room_and_unknown_organizer_are_404_and_nothing_is_stored()
    {
        var env = await NewEnvAsync();
        var start = Now.AddDays(1);

        var noRoom = await env.PostAsync(new { roomId = Guid.NewGuid(), userId = env.Ann.Id, title = "x", start, end = start.AddHours(1), attendeeCount = 1 });
        var noUser = await env.PostAsync(new { roomId = env.Main.Id, userId = Guid.NewGuid(), title = "x", start, end = start.AddHours(1), attendeeCount = 1 });
        var missingIds = await env.PostAsync(new { title = "x", start, end = start.AddHours(1), attendeeCount = 1 });

        noRoom.Status.Should().Be(HttpStatusCode.NotFound);
        Codes(noRoom.Json).Should().Equal(ErrorCodes.RoomNotFound);
        noUser.Status.Should().Be(HttpStatusCode.NotFound);
        Codes(noUser.Json).Should().Equal(ErrorCodes.UserNotFound);
        missingIds.Status.Should().Be(HttpStatusCode.NotFound);
        Codes(missingIds.Json).Should().Equal(ErrorCodes.RoomNotFound);
        (await env.SendAsync(HttpMethod.Get, "/api/reservations")).Json.GetProperty("items").GetArrayLength().Should().Be(0);
    }

    [Fact]
    public async Task Malformed_json_and_empty_body_are_400_in_the_standard_body()
    {
        var env = await NewEnvAsync();

        var malformed = await env.Client.PostAsync("/api/reservations", new StringContent("{ not json", Encoding.UTF8, "application/json"));
        var empty = await env.Client.PostAsync("/api/reservations", new StringContent("", Encoding.UTF8, "application/json"));

        foreach (var response in new[] { malformed, empty })
        {
            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            var text = await response.Content.ReadAsStringAsync();
            text.Should().Contain(ErrorCodes.InvalidRequest);
        }
    }

    [Fact]
    public async Task A_timestamp_without_an_offset_in_the_body_is_a_400_and_nothing_is_stored()
    {
        var env = await NewEnvAsync();
        var json = $$"""
            {"roomId":"{{env.Main.Id}}","userId":"{{env.Ann.Id}}","title":"x","start":"2026-06-02T09:00:00","end":"2026-06-02T10:00:00","attendeeCount":1}
            """;

        var response = await env.Client.PostAsync("/api/reservations", new StringContent(json, Encoding.UTF8, "application/json"));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).Should().Contain(ErrorCodes.InvalidRequest);
        (await env.SendAsync(HttpMethod.Get, "/api/reservations")).Json.GetProperty("items").GetArrayLength().Should().Be(0);
    }

    [Fact]
    public async Task A_timestamp_with_a_non_utc_offset_is_compared_as_an_instant()
    {
        var env = await NewEnvAsync();
        // 2026-06-02T11:00+02:00 is 09:00Z.
        var json = $$"""
            {"roomId":"{{env.Main.Id}}","userId":"{{env.Ann.Id}}","title":"x","start":"2026-06-02T11:00:00+02:00","end":"2026-06-02T12:00:00+02:00","attendeeCount":1}
            """;

        var response = await env.Client.PostAsync("/api/reservations", new StringContent(json, Encoding.UTF8, "application/json"));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        body.RootElement.GetProperty("start").GetDateTimeOffset().Should().Be(new DateTimeOffset(2026, 6, 2, 9, 0, 0, TimeSpan.Zero));
    }

    [Fact]
    public async Task Two_parallel_identical_bookings_give_one_201_and_one_R2()
    {
        var env = await NewEnvAsync();
        var start = Now.AddDays(1);

        var results = await Task.WhenAll(
            env.PostAsync(env.Body(env.Main, env.Ann, start, start.AddHours(1))),
            env.PostAsync(env.Body(env.Main, env.Bob, start, start.AddHours(1))));

        results.Count(r => r.Status == HttpStatusCode.Created).Should().Be(1);
        var rejected = results.Single(r => r.Status == HttpStatusCode.UnprocessableEntity);
        Codes(rejected.Json).Should().Equal(ErrorCodes.RoomOverlap);
        (await env.SendAsync(HttpMethod.Get, "/api/reservations")).Json.GetProperty("items").GetArrayLength().Should().Be(1);
    }

    [Fact]
    public async Task The_same_organizer_booking_two_rooms_in_parallel_gives_one_201_and_one_R3()
    {
        var env = await NewEnvAsync();
        var start = Now.AddDays(1);

        var results = await Task.WhenAll(
            env.PostAsync(env.Body(env.Main, env.Ann, start, start.AddHours(1))),
            env.PostAsync(env.Body(env.Small, env.Ann, start, start.AddHours(1))));

        results.Count(r => r.Status == HttpStatusCode.Created).Should().Be(1);
        var rejected = results.Single(r => r.Status == HttpStatusCode.UnprocessableEntity);
        Codes(rejected.Json).Should().Equal(ErrorCodes.UserOverlap);
    }

    // ---------- get ----------

    [Fact]
    public async Task Get_unknown_id_is_404_and_a_bad_guid_is_400()
    {
        var env = await NewEnvAsync();

        var missing = await env.SendAsync(HttpMethod.Get, $"/api/reservations/{Guid.NewGuid()}");
        var bad = await env.SendAsync(HttpMethod.Get, "/api/reservations/not-a-guid");

        missing.Status.Should().Be(HttpStatusCode.NotFound);
        Codes(missing.Json).Should().Equal(ErrorCodes.ReservationNotFound);
        bad.Status.Should().Be(HttpStatusCode.BadRequest);
        Codes(bad.Json).Should().Equal(ErrorCodes.InvalidRequest);
    }

    [Fact]
    public async Task Get_and_list_complete_an_elapsed_booking_on_the_fly()
    {
        var env = await NewEnvAsync();
        var elapsed = env.Make(env.Main, env.Ann, Now.AddHours(-3), Hour);
        await env.SeedAsync(elapsed);

        var listed = await env.SendAsync(HttpMethod.Get, "/api/reservations?status=Completed");
        var fetched = await env.SendAsync(HttpMethod.Get, $"/api/reservations/{elapsed.Id}");

        Ids(listed.Json).Should().Equal(elapsed.Id.ToString());
        fetched.Json.GetProperty("status").GetString().Should().Be("Completed");
    }

    // ---------- edit ----------

    [Fact]
    public async Task Edit_changes_title_time_room_organizer_and_attendees()
    {
        var env = await NewEnvAsync();
        var start = Now.AddDays(1);
        var original = env.Make(env.Main, env.Ann, start, Hour);
        await env.SeedAsync(original);
        var newStart = start.AddDays(1);

        var response = await env.SendAsync(HttpMethod.Put, $"/api/reservations/{original.Id}",
            env.Body(env.Small, env.Bob, newStart, newStart.AddHours(3), attendees: 4, title: "Moved"));

        response.Status.Should().Be(HttpStatusCode.OK);
        response.Json.GetProperty("roomName").GetString().Should().Be("Small");
        response.Json.GetProperty("organizerName").GetString().Should().Be("Bob Kay");
        response.Json.GetProperty("title").GetString().Should().Be("Moved");
        response.Json.GetProperty("attendeeCount").GetInt32().Should().Be(4);
        var stored = await env.SendAsync(HttpMethod.Get, $"/api/reservations/{original.Id}");
        stored.Json.GetProperty("start").GetDateTimeOffset().Should().Be(newStart);
        stored.Json.GetProperty("end").GetDateTimeOffset().Should().Be(newStart.AddHours(3));
        stored.Json.GetProperty("roomId").GetGuid().Should().Be(env.Small.Id);
        stored.Json.GetProperty("userId").GetGuid().Should().Be(env.Bob.Id);
    }

    [Fact]
    public async Task Edit_does_not_conflict_with_itself()
    {
        var env = await NewEnvAsync();
        var start = Now.AddDays(1);
        var original = env.Make(env.Main, env.Ann, start, 2 * Hour);
        await env.SeedAsync(original);

        // Same room, same organizer, overlapping its own old slot.
        var response = await env.SendAsync(HttpMethod.Put, $"/api/reservations/{original.Id}",
            env.Body(env.Main, env.Ann, start.AddHours(1), start.AddHours(3), title: "Shifted"));

        response.Status.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task A_rejected_edit_leaves_the_stored_reservation_unchanged()
    {
        var env = await NewEnvAsync();
        var start = Now.AddDays(1);
        var original = env.Make(env.Main, env.Ann, start, Hour);
        var blocker = env.Make(env.Small, env.Bob, start.AddDays(1), 2 * Hour);
        await env.SeedAsync(original, blocker);

        var response = await env.SendAsync(HttpMethod.Put, $"/api/reservations/{original.Id}",
            env.Body(env.Small, env.Ann, start.AddDays(1), start.AddDays(1).AddHours(1), title: "Clash"));

        response.Status.Should().Be(HttpStatusCode.UnprocessableEntity);
        Codes(response.Json).Should().Equal(ErrorCodes.RoomOverlap);
        var stored = await env.SendAsync(HttpMethod.Get, $"/api/reservations/{original.Id}");
        stored.Json.GetProperty("roomId").GetGuid().Should().Be(env.Main.Id);
        stored.Json.GetProperty("title").GetString().Should().Be("Meeting");
        stored.Json.GetProperty("start").GetDateTimeOffset().Should().Be(start);
    }

    [Fact]
    public async Task Edit_runs_the_same_rules_as_create()
    {
        var env = await NewEnvAsync();
        var start = Now.AddDays(1);
        var original = env.Make(env.Main, env.Ann, start, Hour);
        await env.SeedAsync(original);

        var response = await env.SendAsync(HttpMethod.Put, $"/api/reservations/{original.Id}",
            env.Body(env.Closed, env.Ann, start, start.AddHours(1)));

        response.Status.Should().Be(HttpStatusCode.UnprocessableEntity);
        Codes(response.Json).Should().Equal(ErrorCodes.RoomInactive);
    }

    [Fact]
    public async Task Edit_of_unknown_reservation_room_or_organizer_is_404()
    {
        var env = await NewEnvAsync();
        var start = Now.AddDays(1);
        var original = env.Make(env.Main, env.Ann, start, Hour);
        await env.SeedAsync(original);

        var noReservation = await env.SendAsync(HttpMethod.Put, $"/api/reservations/{Guid.NewGuid()}", env.Body(env.Main, env.Ann, start, start.AddHours(1)));
        var noRoom = await env.SendAsync(HttpMethod.Put, $"/api/reservations/{original.Id}",
            new { roomId = Guid.NewGuid(), userId = env.Ann.Id, title = "x", start, end = start.AddHours(1), attendeeCount = 1 });
        var noUser = await env.SendAsync(HttpMethod.Put, $"/api/reservations/{original.Id}",
            new { roomId = env.Main.Id, userId = Guid.NewGuid(), title = "x", start, end = start.AddHours(1), attendeeCount = 1 });

        Codes(noReservation.Json).Should().Equal(ErrorCodes.ReservationNotFound);
        Codes(noRoom.Json).Should().Equal(ErrorCodes.RoomNotFound);
        Codes(noUser.Json).Should().Equal(ErrorCodes.UserNotFound);
        noReservation.Status.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Edit_of_a_bad_guid_or_malformed_body_is_400()
    {
        var env = await NewEnvAsync();
        var start = Now.AddDays(1);

        var badId = await env.SendAsync(HttpMethod.Put, "/api/reservations/nope", env.Body(env.Main, env.Ann, start, start.AddHours(1)));
        var badBody = await env.Client.PutAsync($"/api/reservations/{Guid.NewGuid()}", new StringContent("{ nope", Encoding.UTF8, "application/json"));

        badId.Status.Should().Be(HttpStatusCode.BadRequest);
        badBody.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ---------- state table: edit / cancel / end ----------

    [Fact]
    public async Task A_started_booking_cannot_be_edited_or_cancelled()
    {
        var env = await NewEnvAsync();
        var running = env.Make(env.Main, env.Ann, Now.AddHours(-1), 3 * Hour);
        await env.SeedAsync(running);
        var start = Now.AddDays(1);

        var edit = await env.SendAsync(HttpMethod.Put, $"/api/reservations/{running.Id}", env.Body(env.Main, env.Ann, start, start.AddHours(1)));
        var cancel = await env.SendAsync(HttpMethod.Post, $"/api/reservations/{running.Id}/cancel");

        edit.Status.Should().Be(HttpStatusCode.Conflict);
        Codes(edit.Json).Should().Equal(ErrorCodes.AlreadyStarted);
        cancel.Status.Should().Be(HttpStatusCode.Conflict);
        Codes(cancel.Json).Should().Equal(ErrorCodes.AlreadyStarted);
        AssertReadableErrors(cancel.Json);
    }

    [Fact]
    public async Task A_booking_starting_exactly_now_counts_as_started()
    {
        var env = await NewEnvAsync();
        var startingNow = env.Make(env.Main, env.Ann, Now, Hour);
        await env.SeedAsync(startingNow);

        var cancel = await env.SendAsync(HttpMethod.Post, $"/api/reservations/{startingNow.Id}/cancel");

        cancel.Status.Should().Be(HttpStatusCode.Conflict);
        Codes(cancel.Json).Should().Equal(ErrorCodes.AlreadyStarted);
    }

    [Fact]
    public async Task Cancel_a_future_booking_frees_the_slot_and_a_second_cancel_is_409()
    {
        var env = await NewEnvAsync();
        var start = Now.AddDays(1);
        var booking = env.Make(env.Main, env.Ann, start, Hour);
        await env.SeedAsync(booking);

        var cancelled = await env.SendAsync(HttpMethod.Post, $"/api/reservations/{booking.Id}/cancel");
        var again = await env.SendAsync(HttpMethod.Post, $"/api/reservations/{booking.Id}/cancel");
        var rebooked = await env.PostAsync(env.Body(env.Main, env.Ann, start, start.AddHours(1)));

        cancelled.Status.Should().Be(HttpStatusCode.OK);
        cancelled.Json.GetProperty("status").GetString().Should().Be("Cancelled");
        again.Status.Should().Be(HttpStatusCode.Conflict);
        Codes(again.Json).Should().Equal(ErrorCodes.NotBooked);
        rebooked.Status.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task A_cancelled_or_completed_booking_cannot_be_edited_cancelled_or_ended()
    {
        var env = await NewEnvAsync();
        var start = Now.AddDays(1);
        var cancelled = env.Make(env.Main, env.Ann, start, Hour, ReservationStatus.Cancelled);
        var completed = env.Make(env.Small, env.Bob, Now.AddDays(-2), Hour, ReservationStatus.Completed);
        await env.SeedAsync(cancelled, completed);

        foreach (var id in new[] { cancelled.Id, completed.Id })
        {
            var edit = await env.SendAsync(HttpMethod.Put, $"/api/reservations/{id}", env.Body(env.Main, env.Ann, start.AddDays(2), start.AddDays(2).AddHours(1)));
            var cancel = await env.SendAsync(HttpMethod.Post, $"/api/reservations/{id}/cancel");
            var end = await env.SendAsync(HttpMethod.Post, $"/api/reservations/{id}/end");

            foreach (var result in new[] { edit, cancel, end })
            {
                result.Status.Should().Be(HttpStatusCode.Conflict);
                Codes(result.Json).Should().Equal(ErrorCodes.NotBooked);
            }
        }
    }

    [Fact]
    public async Task End_early_marks_it_completed_keeps_the_planned_end_and_frees_the_room_from_now()
    {
        var env = await NewEnvAsync();
        var running = env.Make(env.Main, env.Ann, Now.AddHours(-1), 3 * Hour);
        await env.SeedAsync(running);

        var ended = await env.SendAsync(HttpMethod.Post, $"/api/reservations/{running.Id}/end");
        var again = await env.SendAsync(HttpMethod.Post, $"/api/reservations/{running.Id}/end");
        // The planned end (12:00) is still in the future, but the room is free from the moment it ended (10:00).
        var next = await env.PostAsync(env.Body(env.Main, env.Bob, Now.AddMinutes(30), Now.AddHours(1)));

        ended.Status.Should().Be(HttpStatusCode.OK);
        ended.Json.GetProperty("status").GetString().Should().Be("Completed");
        ended.Json.GetProperty("endedAt").GetDateTimeOffset().Should().Be(Now);
        ended.Json.GetProperty("end").GetDateTimeOffset().Should().Be(running.End);
        again.Status.Should().Be(HttpStatusCode.Conflict);
        Codes(again.Json).Should().Equal(ErrorCodes.NotBooked);
        next.Status.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task End_early_on_a_future_booking_is_409_not_started()
    {
        var env = await NewEnvAsync();
        var future = env.Make(env.Main, env.Ann, Now.AddDays(1), Hour);
        await env.SeedAsync(future);

        var end = await env.SendAsync(HttpMethod.Post, $"/api/reservations/{future.Id}/end");

        end.Status.Should().Be(HttpStatusCode.Conflict);
        Codes(end.Json).Should().Equal(ErrorCodes.NotStarted);
        AssertReadableErrors(end.Json);
    }

    [Fact]
    public async Task Cancel_and_end_of_unknown_or_malformed_ids()
    {
        var env = await NewEnvAsync();

        foreach (var action in new[] { "cancel", "end" })
        {
            var missing = await env.SendAsync(HttpMethod.Post, $"/api/reservations/{Guid.NewGuid()}/{action}");
            var bad = await env.SendAsync(HttpMethod.Post, $"/api/reservations/nope/{action}");

            missing.Status.Should().Be(HttpStatusCode.NotFound);
            Codes(missing.Json).Should().Equal(ErrorCodes.ReservationNotFound);
            bad.Status.Should().Be(HttpStatusCode.BadRequest);
        }
    }

    // ---------- list ----------

    private async Task<(Env Env, Reservation Early, Reservation Middle, Reservation Late, Reservation Cancelled)> ListEnvAsync()
    {
        var env = await NewEnvAsync();
        var day = Now.AddDays(1);
        var early = env.Make(env.Main, env.Ann, day, Hour);                                   // day 00:00-01:00
        var middle = env.Make(env.Small, env.Bob, day.AddHours(2), Hour);                      // 02:00-03:00
        var late = env.Make(env.Main, env.Bob, day.AddHours(5), Hour);                         // 05:00-06:00
        var cancelled = env.Make(env.Main, env.Ann, day.AddHours(8), Hour, ReservationStatus.Cancelled);
        await env.SeedAsync(early, middle, late, cancelled);
        return (env, early, middle, late, cancelled);
    }

    private static string Q(DateTimeOffset value) => Uri.EscapeDataString(value.ToString("yyyy-MM-ddTHH:mm:ssK"));

    [Fact]
    public async Task List_is_newest_start_first_and_includes_every_status()
    {
        var (env, early, middle, late, cancelled) = await ListEnvAsync();

        var response = await env.SendAsync(HttpMethod.Get, "/api/reservations");

        response.Status.Should().Be(HttpStatusCode.OK);
        Ids(response.Json).Should().Equal(cancelled.Id.ToString(), late.Id.ToString(), middle.Id.ToString(), early.Id.ToString());
    }

    [Fact]
    public async Task List_filters_by_room_and_by_organizer()
    {
        var (env, early, middle, late, cancelled) = await ListEnvAsync();

        var byRoom = await env.SendAsync(HttpMethod.Get, $"/api/reservations?roomId={env.Small.Id}");
        var byUser = await env.SendAsync(HttpMethod.Get, $"/api/reservations?userId={env.Bob.Id}");
        var both = await env.SendAsync(HttpMethod.Get, $"/api/reservations?roomId={env.Main.Id}&userId={env.Ann.Id}");

        Ids(byRoom.Json).Should().Equal(middle.Id.ToString());
        Ids(byUser.Json).Should().Equal(late.Id.ToString(), middle.Id.ToString());
        Ids(both.Json).Should().Equal(cancelled.Id.ToString(), early.Id.ToString());
    }

    [Fact]
    public async Task List_unknown_room_or_user_gives_an_empty_list()
    {
        var (env, _, _, _, _) = await ListEnvAsync();

        var byRoom = await env.SendAsync(HttpMethod.Get, $"/api/reservations?roomId={Guid.NewGuid()}");
        var byUser = await env.SendAsync(HttpMethod.Get, $"/api/reservations?userId={Guid.NewGuid()}");

        byRoom.Status.Should().Be(HttpStatusCode.OK);
        byRoom.Json.GetProperty("items").GetArrayLength().Should().Be(0);
        byUser.Json.GetProperty("items").GetArrayLength().Should().Be(0);
    }

    [Theory]
    [InlineData("Cancelled")]
    [InlineData("cancelled")]
    [InlineData("CANCELLED")]
    public async Task List_status_filter_ignores_case(string status)
    {
        var (env, _, _, _, cancelled) = await ListEnvAsync();

        var response = await env.SendAsync(HttpMethod.Get, $"/api/reservations?status={status}");

        response.Status.Should().Be(HttpStatusCode.OK);
        Ids(response.Json).Should().Equal(cancelled.Id.ToString());
    }

    [Fact]
    public async Task List_status_booked_excludes_the_cancelled_one()
    {
        var (env, early, middle, late, _) = await ListEnvAsync();

        var response = await env.SendAsync(HttpMethod.Get, "/api/reservations?status=Booked");

        Ids(response.Json).Should().Equal(late.Id.ToString(), middle.Id.ToString(), early.Id.ToString());
    }

    [Fact]
    public async Task List_date_window_is_half_open()
    {
        var (env, early, middle, late, _) = await ListEnvAsync();
        var day = Now.AddDays(1);

        // [01:00, 05:00): the early one ends exactly at the window start (out), the late one starts exactly at the end (out).
        var window = await env.SendAsync(HttpMethod.Get, $"/api/reservations?from={Q(day.AddHours(1))}&to={Q(day.AddHours(5))}");
        // From inside the early meeting: it overlaps, so it is in.
        var overlapping = await env.SendAsync(HttpMethod.Get, $"/api/reservations?from={Q(day.AddMinutes(30))}&to={Q(day.AddHours(2))}");
        var onlyFrom = await env.SendAsync(HttpMethod.Get, $"/api/reservations?from={Q(day.AddHours(3))}&status=Booked");
        var onlyTo = await env.SendAsync(HttpMethod.Get, $"/api/reservations?to={Q(day.AddHours(2))}");

        Ids(window.Json).Should().Equal(middle.Id.ToString());
        Ids(overlapping.Json).Should().Equal(early.Id.ToString());
        Ids(onlyFrom.Json).Should().Equal(late.Id.ToString());
        Ids(onlyTo.Json).Should().Equal(early.Id.ToString());
    }

    [Fact]
    public async Task List_uses_the_ended_at_time_as_the_finish_of_an_ended_early_meeting()
    {
        var env = await NewEnvAsync();
        var ended = env.Make(env.Main, env.Ann, Now.AddHours(-2), 6 * Hour, ReservationStatus.Completed, endedAt: Now.AddHours(-1));
        await env.SeedAsync(ended);

        var afterEnd = await env.SendAsync(HttpMethod.Get, $"/api/reservations?from={Q(Now.AddMinutes(-30))}");
        var beforeEnd = await env.SendAsync(HttpMethod.Get, $"/api/reservations?from={Q(Now.AddMinutes(-90))}");

        afterEnd.Json.GetProperty("items").GetArrayLength().Should().Be(0);
        Ids(beforeEnd.Json).Should().Equal(ended.Id.ToString());
    }

    [Fact]
    public async Task List_accepts_from_equal_to_to_and_a_non_utc_offset()
    {
        var (env, early, _, _, _) = await ListEnvAsync();
        var day = Now.AddDays(1);

        var equal = await env.SendAsync(HttpMethod.Get, $"/api/reservations?from={Q(day.AddMinutes(10))}&to={Q(day.AddMinutes(10))}");
        // The same instants written with a +02:00 offset must select the same reservation.
        var plusTwo = await env.SendAsync(HttpMethod.Get,
            $"/api/reservations?from={Q(day.ToOffset(TimeSpan.FromHours(2)))}&to={Q(day.AddMinutes(30).ToOffset(TimeSpan.FromHours(2)))}");

        equal.Status.Should().Be(HttpStatusCode.OK);
        // An equal pair is an empty window, even though a meeting spans that instant.
        Ids(equal.Json).Should().BeEmpty();
        plusTwo.Status.Should().Be(HttpStatusCode.OK);
        Ids(plusTwo.Json).Should().Equal(early.Id.ToString());
    }

    [Theory]
    [InlineData("status=1")]
    [InlineData("status=abc")]
    [InlineData("roomId=not-a-guid")]
    [InlineData("userId=123")]
    [InlineData("from=2026-06-02T09:00:00")]
    [InlineData("to=2026-06-02")]
    [InlineData("from=yesterday")]
    [InlineData("from=2026-06-03T00:00:00Z&to=2026-06-02T00:00:00Z")]
    public async Task List_rejects_bad_filters_with_400(string query)
    {
        var (env, _, _, _, _) = await ListEnvAsync();

        var response = await env.SendAsync(HttpMethod.Get, $"/api/reservations?{query}");

        response.Status.Should().Be(HttpStatusCode.BadRequest);
        Codes(response.Json).Should().Equal(ErrorCodes.InvalidRequest);
        AssertReadableErrors(response.Json);
    }

    // ---------- swagger ----------

    [Fact]
    public async Task Swagger_lists_the_six_reservation_operations()
    {
        var env = await NewEnvAsync();

        var response = await env.Client.GetAsync("/swagger/v1/swagger.json");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var operations = doc.RootElement.GetProperty("paths").EnumerateObject()
            .Where(path => path.Name.StartsWith("/api/Reservations", StringComparison.OrdinalIgnoreCase))
            .SelectMany(path => path.Value.EnumerateObject().Select(op => $"{op.Name.ToUpperInvariant()} {path.Name.ToLowerInvariant()}"))
            .ToArray();
        operations.Should().BeEquivalentTo(
            "GET /api/reservations", "POST /api/reservations", "GET /api/reservations/{id}",
            "PUT /api/reservations/{id}", "POST /api/reservations/{id}/cancel", "POST /api/reservations/{id}/end");
    }
}
