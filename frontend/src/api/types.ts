/** Equipment names exactly as the API sends and accepts them. */
export type EquipmentName = 'Projector' | 'TvScreen' | 'VideoConferencing' | 'Whiteboard'

export interface RoomDto {
  id: string
  name: string
  capacity: number
  floor: number
  equipment: EquipmentName[]
  isActive: boolean
}

export interface ApiErrorItem {
  code: string
  message: string
}

/** The single error body shape used by every failing endpoint. */
export interface ApiErrorBody {
  errors: ApiErrorItem[]
}

export interface UserDto {
  id: string
  firstName: string
  lastName: string
  /** Number of Booked reservations the user organizes; calculated by the API. */
  activeReservationCount: number
}

/** Status names exactly as the API sends and accepts them. */
export type ReservationStatus = 'Booked' | 'Completed' | 'Cancelled'

export interface ReservationDto {
  id: string
  roomId: string
  roomName: string
  userId: string
  organizerName: string
  title: string
  /** ISO 8601 timestamp with an offset. */
  start: string
  end: string
  /** Set only when the reservation was ended early; the planned `end` is never replaced. */
  endedAt: string | null
  attendeeCount: number
  status: ReservationStatus
}

/** Body of create and change. The title may be empty: the API decides whether that is allowed. */
export interface CreateReservationRequest {
  roomId: string
  userId: string
  title: string
  start: string
  end: string
  attendeeCount: number
}

/** One row of the usage report; every figure and the flag are calculated by the API. */
export interface RoomUsageDto {
  roomId: string
  roomName: string
  floor: number
  totalBookings: number
  completedBookings: number
  cancelledBookings: number
  upcomingBookings: number
  plannedHours: number
  usedHours: number
  /** Null when the room has no planned hours in the period. */
  utilizationPercent: number | null
  isOftenBookedRarelyUsed: boolean
}

export interface UsageReportDto {
  /** ISO 8601 timestamp with an offset. */
  generatedAt: string
  /** Echo of the query; null when the bound was not sent. */
  from: string | null
  to: string | null
  minBookingsToFlag: number
  underusedThresholdPercent: number
  /** Already in display order: flagged first, then most bookings, then name. */
  rooms: RoomUsageDto[]
}

/** The envelope every list endpoint answers with; `page` starts at 1. */
export interface PagedResult<T> {
  items: T[]
  page: number
  pageSize: number
  totalCount: number
  totalPages: number
}

/** Optional paging parameters; the API applies its own defaults and maximum when they are left out. */
export interface PageParams {
  page?: number
  pageSize?: number
}
