namespace RoomReservation.Api.Domain;

public class Room
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int Capacity { get; set; }
    public int Floor { get; set; }
    public Equipment Equipment { get; set; }
    public bool IsActive { get; set; } = true;
}
