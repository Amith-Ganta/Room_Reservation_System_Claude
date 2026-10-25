import { render, screen } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { ErrorAlert } from './ErrorAlert'

describe('ErrorAlert', () => {
  it('renders nothing for an empty list', () => {
    const { container } = render(<ErrorAlert errors={[]} />)

    expect(container).toBeEmptyDOMElement()
  })

  it('shows one error as CODE -- message under a singular title', () => {
    render(<ErrorAlert errors={[{ code: 'R4_PAST_START', message: 'The start is in the past.' }]} />)

    expect(screen.getByRole('alert')).toBeInTheDocument()
    expect(screen.getByText('Something went wrong')).toBeInTheDocument()
    expect(screen.getByText('R4_PAST_START -- The start is in the past.')).toBeInTheDocument()
  })

  it('shows every error under a plural title', () => {
    render(
      <ErrorAlert
        errors={[
          { code: 'R1_MAX_DURATION', message: 'Too long.' },
          { code: 'R5_CAPACITY_EXCEEDED', message: 'Too many people.' },
        ]}
      />,
    )

    expect(screen.getByText('Some things went wrong')).toBeInTheDocument()
    expect(screen.getAllByRole('listitem').map((item) => item.textContent)).toEqual([
      'R1_MAX_DURATION -- Too long.',
      'R5_CAPACITY_EXCEEDED -- Too many people.',
    ])
  })

  it('keeps two errors with the same code', () => {
    render(
      <ErrorAlert
        errors={[
          { code: 'INVALID_REQUEST', message: 'Name is required.' },
          { code: 'INVALID_REQUEST', message: 'Capacity must be positive.' },
        ]}
      />,
    )

    expect(screen.getAllByRole('listitem')).toHaveLength(2)
  })
})
