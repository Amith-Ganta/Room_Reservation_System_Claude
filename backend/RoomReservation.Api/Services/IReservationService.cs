using RoomReservation.Api.Common;
using RoomReservation.Api.Dtos;

namespace RoomReservation.Api.Services;

public interface IReservationService
{
    Task<Result<PagedResult<ReservationDto>>> ListAsync(ReservationListQuery query, CancellationToken ct = default);
    Task<Result<ReservationDto>> GetAsync(Guid id, CancellationToken ct = default);
    Task<Result<ReservationDto>> CreateAsync(CreateReservationRequest request, CancellationToken ct = default);
    Task<Result<ReservationDto>> UpdateAsync(Guid id, CreateReservationRequest request, CancellationToken ct = default);
    Task<Result<ReservationDto>> CancelAsync(Guid id, CancellationToken ct = default);
    Task<Result<ReservationDto>> EndEarlyAsync(Guid id, CancellationToken ct = default);
}
