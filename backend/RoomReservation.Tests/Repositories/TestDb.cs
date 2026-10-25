using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using RoomReservation.Api.Configuration;
using RoomReservation.Api.Data;
using RoomReservation.Api.Domain;

namespace RoomReservation.Tests.Repositories;

/// <summary>Builds an isolated InMemory database per test (fresh name each time) and small entity factories.</summary>
internal static class TestDb
{
    public static readonly DateTimeOffset Now = new(2026, 6, 1, 10, 0, 0, TimeSpan.Zero);

    /// <summary>Paging limits for service tests: small enough to cross a page boundary with a handful of rows.</summary>
    public static IOptions<PagingOptions> Paging(int defaultSize = 20, int maxSize = 100) =>
        Options.Create(new PagingOptions { DefaultPageSize = defaultSize, MaxPageSize = maxSize });

    public static string NewName() => $"tests-{Guid.NewGuid()}";

    /// <summary>Pass the same name to get a second, independent context over the same data (proves a save happened).</summary>
    public static AppDbContext Create(string? name = null) =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(name ?? NewName())
            .Options);

    public static Room Room(string name = "Room", int capacity = 10, int floor = 1,
        Equipment equipment = Equipment.None, bool isActive = true) =>
        new() { Id = Guid.NewGuid(), Name = name, Capacity = capacity, Floor = floor, Equipment = equipment, IsActive = isActive };

    public static User User(string first = "Ann", string last = "Lee") =>
        new() { Id = Guid.NewGuid(), FirstName = first, LastName = last };

    public static Reservation Reservation(Room room, User user, DateTimeOffset start, TimeSpan duration,
        ReservationStatus status = ReservationStatus.Booked, DateTimeOffset? endedAt = null) =>
        new()
        {
            Id = Guid.NewGuid(), RoomId = room.Id, UserId = user.Id, Title = "Meeting",
            Start = start, End = start + duration, EndedAt = endedAt, AttendeeCount = 2, Status = status
        };
}
