using Microsoft.EntityFrameworkCore;
using RoomReservation.Api.Domain;

namespace RoomReservation.Api.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Room> Rooms => Set<Room>();
    public DbSet<User> Users => Set<User>();
    public DbSet<Reservation> Reservations => Set<Reservation>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Room>(room =>
        {
            room.HasKey(r => r.Id);
            room.Property(r => r.Name).IsRequired().HasMaxLength(100);
        });

        modelBuilder.Entity<User>(user =>
        {
            user.HasKey(u => u.Id);
            user.Property(u => u.FirstName).IsRequired().HasMaxLength(100);
            user.Property(u => u.LastName).IsRequired().HasMaxLength(100);
        });

        modelBuilder.Entity<Reservation>(reservation =>
        {
            reservation.HasKey(r => r.Id);
            reservation.Property(r => r.Title).IsRequired().HasMaxLength(200);
            reservation.HasOne(r => r.Room).WithMany().HasForeignKey(r => r.RoomId).OnDelete(DeleteBehavior.Restrict);
            reservation.HasOne(r => r.User).WithMany().HasForeignKey(r => r.UserId).OnDelete(DeleteBehavior.Restrict);
        });
    }
}
