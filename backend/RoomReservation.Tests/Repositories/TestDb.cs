using Microsoft.EntityFrameworkCore;
using RoomReservation.Api.Data;
using RoomReservation.Api.Domain;

namespace RoomReservation.Tests.Repositories;

/// <summary>Builds an isolated InMemory database per test (fresh name each time) and small entity factories.</summary>
internal static class TestDb
{
    public static readonly DateTimeOffset Now = new(2026, 6, 1, 10, 0, 0, TimeSpan.Zero);

    public static AppDbContext Create() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"tests-{Guid.NewGuid()}")
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
