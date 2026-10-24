using FluentAssertions;
using RoomReservation.Api.Domain;
using RoomReservation.Api.Repositories;

namespace RoomReservation.Tests.Repositories;

public class UserRepositoryTests
{
    [Fact]
    public async Task List_orders_by_last_name_then_first_name()
    {
        var db = TestDb.Create();
        db.Users.AddRange(
            TestDb.User("Zoe", "Adams"),
            TestDb.User("Ann", "Brown"),
            TestDb.User("Abe", "Adams"));
        await db.SaveChangesAsync();

        var users = (await new UserRepository(db).ListPagedAsync(new PageRequest(1, 10))).Items;

        users.Select(u => $"{u.FirstName} {u.LastName}").Should().Equal("Abe Adams", "Zoe Adams", "Ann Brown");
    }
}
