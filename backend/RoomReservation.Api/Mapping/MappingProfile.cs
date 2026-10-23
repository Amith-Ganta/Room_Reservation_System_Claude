using AutoMapper;
using RoomReservation.Api.Domain;
using RoomReservation.Api.Dtos;

namespace RoomReservation.Api.Mapping;

public sealed class MappingProfile : Profile
{
    public MappingProfile()
    {
        CreateMap<Room, RoomDto>()
            .ForCtorParam(nameof(RoomDto.Equipment), options => options.MapFrom(room => EquipmentFlags.Expand(room.Equipment)));

        CreateMap<RoomListQuery, RoomFilter>()
            .ForMember(filter => filter.Equipment, options => options.MapFrom(query => ParseEquipment(query.Equipment)));
    }

    // Blank text means "no equipment filter"; the query was already validated, so a bad value never gets here.
    private static Equipment? ParseEquipment(string? text) =>
        EquipmentFlags.TryParseList(text, out var equipment) && equipment != Equipment.None ? equipment : null;
}
