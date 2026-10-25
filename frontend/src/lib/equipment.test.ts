import { describe, expect, it } from 'vitest'
import { ALL_EQUIPMENT, EQUIPMENT_LABELS } from './equipment'

describe('equipment', () => {
  it('lists the four items in filter order', () => {
    expect(ALL_EQUIPMENT).toEqual(['Projector', 'TvScreen', 'VideoConferencing', 'Whiteboard'])
  })

  it('gives every item a readable label', () => {
    expect(EQUIPMENT_LABELS).toEqual({
      Projector: 'Projector',
      TvScreen: 'TV screen',
      VideoConferencing: 'Video conferencing',
      Whiteboard: 'Whiteboard',
    })
  })
})
