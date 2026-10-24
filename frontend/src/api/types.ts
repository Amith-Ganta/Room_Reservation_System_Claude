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
