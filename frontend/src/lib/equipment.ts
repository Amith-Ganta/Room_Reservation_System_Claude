import type { EquipmentName } from '../api/types'

/** Wire value -> label shown to the user. The order is the order of the filter list. */
export const EQUIPMENT_LABELS: Record<EquipmentName, string> = {
  Projector: 'Projector',
  TvScreen: 'TV screen',
  VideoConferencing: 'Video conferencing',
  Whiteboard: 'Whiteboard',
}

export const ALL_EQUIPMENT = Object.keys(EQUIPMENT_LABELS) as EquipmentName[]
