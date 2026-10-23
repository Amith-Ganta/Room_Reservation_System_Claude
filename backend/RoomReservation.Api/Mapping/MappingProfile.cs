using AutoMapper;
using RoomReservation.Api.Domain;
using RoomReservation.Api.Dtos;

namespace RoomReservation.Api.Mapping;

public sealed class MappingProfile : Profile
{
    /// <summary>Key of the mapping context item that carries a user's active reservation count (it is not on <see cref="User"/>).</summary>
    public const string ActiveCountItem = "ActiveReservationCount";

    public MappingProfile()
    {
        CreateMap<User, UserDto>()
            .ForCtorParam(nameof(UserDto.ActiveReservationCount),
                options => options.MapFrom((_, context) =>
                    context.TryGetItems(out var items) && items.TryGetValue(ActiveCountItem, out var count) ? (int)count : 0));

        CreateMap<Room, RoomDto>()
            .ForCtorParam(nameof(RoomDto.Equipment), options => options.MapFrom(room => EquipmentFlags.Expand(room.Equipment)));

        CreateMap<RoomListQuery, RoomFilter>()
            .ForMember(filter => filter.Equipment, options => options.MapFrom(query => ParseEquipment(query.Equipment)));

        CreateMap<Reservation, ReservationDto>()
            .ForCtorParam(nameof(ReservationDto.OrganizerName),
                options => options.MapFrom(reservation => reservation.User.FirstName + " " + reservation.User.LastName));

        CreateMap<ReservationListQuery, ReservationFilter>()
            .ForMember(filter => filter.Status, options => options.MapFrom(query => ParseStatus(query.Status)));

        CreateMap<UsageReportQuery, UsageReportFilter>();
    }

    // Blank text means "no status filter"; the query was already validated, so a bad value never gets here.
    private static ReservationStatus? ParseStatus(string? text) =>
        ReservationStatusNameAttribute.TryParse(text, out var status) ? status : null;

    // Blank text means "no equipment filter"; the query was already validated, so a bad value never gets here.
    private static Equipment? ParseEquipment(string? text) =>
        EquipmentFlags.TryParseList(text, out var equipment) && equipment != Equipment.None ? equipment : null;
}
