using Microsoft.EntityFrameworkCore;
using RoomReservation.Api.Domain;

namespace RoomReservation.Api.Data;

/// <summary>
/// Fills the in-memory database with demo data. Dates are relative to "now" so the demo always has
/// past meetings (for the usage report) and upcoming ones (for cancel / end-early).
/// </summary>
public static class DbSeeder
{
    public static async Task SeedAsync(AppDbContext db, TimeProvider clock, CancellationToken ct = default)
    {
        if (await db.Rooms.AnyAsync(ct))
        {
            return;
        }

        var now = clock.GetUtcNow();
        var today = new DateTimeOffset(now.UtcDateTime.Date, TimeSpan.Zero);

        var rooms = SeedRooms();
        var users = SeedUsers();
        var reservations = SeedReservations(now, today, rooms, users);

        db.Rooms.AddRange(rooms.Values);
        db.Users.AddRange(users.Values);
        db.Reservations.AddRange(reservations);
        await db.SaveChangesAsync(ct);
    }

    private static Dictionary<string, Room> SeedRooms()
    {
        Room Make(string name, int floor, int capacity, Equipment equipment, bool isActive = true) => new()
        {
            Id = Guid.NewGuid(), Name = name, Floor = floor, Capacity = capacity, Equipment = equipment, IsActive = isActive
        };

        var rooms = new[]
        {
            Make("Atlas", 1, 20, Equipment.Projector | Equipment.VideoConferencing | Equipment.Whiteboard),
            Make("Boardroom", 3, 14, Equipment.Projector | Equipment.TvScreen | Equipment.VideoConferencing | Equipment.Whiteboard),
            Make("Cedar", 1, 6, Equipment.Whiteboard),
            Make("Delta", 2, 10, Equipment.TvScreen | Equipment.VideoConferencing),
            Make("Echo", 2, 4, Equipment.TvScreen),
            Make("Focus Pod", 3, 2, Equipment.None),
            Make("Lecture Hall", 0, 40, Equipment.Projector | Equipment.Whiteboard),
            Make("Summit", 4, 12, Equipment.Projector | Equipment.VideoConferencing, isActive: false)
        };

        return rooms.ToDictionary(r => r.Name);
    }

    private static Dictionary<string, User> SeedUsers()
    {
        User Make(string first, string last) => new() { Id = Guid.NewGuid(), FirstName = first, LastName = last };

        var users = new[]
        {
            Make("Ava", "Johnson"),
            Make("Liam", "Smith"),
            Make("Mia", "Chen"),
            Make("Noah", "Patel"),
            Make("Emma", "Garcia"),
            Make("Lucas", "Mueller")
        };

        return users.ToDictionary(u => u.FirstName);
    }

    private static List<Reservation> SeedReservations(
        DateTimeOffset now, DateTimeOffset today, IReadOnlyDictionary<string, Room> rooms, IReadOnlyDictionary<string, User> users)
    {
        var list = new List<Reservation>();

        void AddAt(string room, string user, string title, DateTimeOffset start, int minutes, int attendees,
            ReservationStatus status, int? endedAfterMinutes = null)
        {
            list.Add(new Reservation
            {
                Id = Guid.NewGuid(),
                RoomId = rooms[room].Id,
                UserId = users[user].Id,
                Title = title,
                Start = start,
                End = start.AddMinutes(minutes),
                EndedAt = endedAfterMinutes is { } ended ? start.AddMinutes(ended) : null,
                AttendeeCount = attendees,
                Status = status
            });
        }

        void Add(string room, string user, string title, int dayOffset, int hour, int hours, int attendees,
            ReservationStatus status, int? endedAfterMinutes = null) =>
            AddAt(room, user, title, today.AddDays(dayOffset).AddHours(hour), hours * 60, attendees, status, endedAfterMinutes);

        const ReservationStatus done = ReservationStatus.Completed;
        const ReservationStatus cancelled = ReservationStatus.Cancelled;
        const ReservationStatus booked = ReservationStatus.Booked;

        // Boardroom: the "booked often, rarely used" example. Mostly cancelled or ended after a few minutes.
        Add("Boardroom", "Ava", "Quarterly planning", -12, 9, 2, 10, cancelled);
        Add("Boardroom", "Liam", "Vendor review", -10, 9, 2, 8, done, 10);
        Add("Boardroom", "Mia", "Budget sync", -8, 9, 2, 9, cancelled);
        Add("Boardroom", "Noah", "Roadmap workshop", -6, 9, 2, 12, cancelled);
        Add("Boardroom", "Emma", "Hiring panel", -4, 9, 2, 6, done, 15);
        Add("Boardroom", "Lucas", "Board prep", -3, 9, 2, 7, cancelled);

        // Atlas: well used.
        Add("Atlas", "Ava", "Sprint planning", -10, 11, 2, 15, done);
        Add("Atlas", "Liam", "Sprint review", -8, 11, 2, 14, done);
        Add("Atlas", "Mia", "Design critique", -6, 11, 2, 12, done);
        Add("Atlas", "Noah", "Architecture session", -4, 11, 2, 10, done);
        Add("Atlas", "Emma", "All hands", -2, 11, 2, 18, done);

        // Cedar and Delta: healthy utilization.
        Add("Cedar", "Lucas", "Pair session", -9, 13, 1, 4, done);
        Add("Cedar", "Ava", "Retro", -5, 13, 1, 5, done);
        Add("Cedar", "Mia", "Standup deep dive", -2, 13, 1, 3, done);
        Add("Delta", "Noah", "Client call", -9, 15, 2, 6, done);
        Add("Delta", "Emma", "Demo prep", -7, 15, 2, 8, done, 105);
        Add("Delta", "Liam", "Onboarding", -5, 15, 2, 5, done);
        Add("Delta", "Lucas", "Support review", -3, 15, 2, 7, done);

        // Echo: mixed.
        Add("Echo", "Ava", "1:1", -9, 17, 1, 2, done);
        Add("Echo", "Liam", "1:1", -7, 17, 1, 2, cancelled);
        Add("Echo", "Mia", "Interview", -5, 17, 1, 3, done);
        Add("Echo", "Noah", "Interview", -3, 17, 1, 3, done);
        Add("Echo", "Emma", "Coaching", -2, 17, 1, 2, cancelled);

        Add("Lecture Hall", "Lucas", "Internal training", -6, 8, 3, 35, done);

        // Upcoming (all valid under R1-R7). The first one starts an hour from now (rounded down to the minute, so
        // always in the future), giving the demo a reservation to cancel that is not a day away. Near midnight it
        // falls on tomorrow; either way its room and organizer have nothing else near that time.
        var laterToday = now.AddHours(1);
        laterToday = new DateTimeOffset(laterToday.Year, laterToday.Month, laterToday.Day, laterToday.Hour, laterToday.Minute, 0, TimeSpan.Zero);
        AddAt("Focus Pod", "Lucas", "Project check-in", laterToday, 60, 2, booked);

        Add("Echo", "Mia", "Customer interview", 1, 9, 1, 3, booked);
        Add("Atlas", "Ava", "Product kickoff", 2, 14, 1, 12, booked);
        Add("Boardroom", "Liam", "Strategy offsite prep", 3, 10, 2, 8, booked);
        Add("Cedar", "Lucas", "Code walkthrough", 4, 15, 1, 5, booked);
        Add("Delta", "Noah", "Partner sync", 5, 13, 1, 6, booked);
        Add("Lecture Hall", "Emma", "Knowledge share", 7, 9, 4, 35, booked);

        return list;
    }
}
