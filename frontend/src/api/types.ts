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
