namespace RoomReservation.Api.Common;

/// <summary>Body of every failed API call: one or more coded, human-readable errors.</summary>
public sealed record ApiErrorResponse(IReadOnlyList<ApiError> Errors);
