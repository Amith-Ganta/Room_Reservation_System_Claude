/** Equipment names exactly as the API sends and accepts them. */
export type EquipmentName = 'Projector' | 'TvScreen' | 'VideoConferencing' | 'Whiteboard'

export interface RoomDto {
  id: number
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
