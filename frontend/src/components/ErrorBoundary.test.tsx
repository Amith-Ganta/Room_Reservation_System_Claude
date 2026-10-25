import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it, vi } from 'vitest'
import { ErrorBoundary } from './ErrorBoundary'

function Broken(): never {
  throw new Error('render failed')
}

describe('ErrorBoundary', () => {
  it('renders its children when nothing fails', () => {
    render(
      <ErrorBoundary>
        <p>All good</p>
      </ErrorBoundary>,
    )

    expect(screen.getByText('All good')).toBeInTheDocument()
    expect(screen.queryByRole('alert')).not.toBeInTheDocument()
  })

  it('shows the message and a Reload button when a child crashes', () => {
    // React and the boundary both log the crash; keep the test output clean.
    const consoleError = vi.spyOn(console, 'error').mockImplementation(() => undefined)

    render(
      <ErrorBoundary>
        <Broken />
      </ErrorBoundary>,
    )

    expect(screen.getByRole('alert')).toHaveTextContent('This page could not be shown. Reload to try again.')
    expect(screen.getByRole('button', { name: 'Reload' })).toBeInTheDocument()
    expect(consoleError).toHaveBeenCalled()
  })

  it('reloads the page when Reload is clicked', async () => {
    vi.spyOn(console, 'error').mockImplementation(() => undefined)
    const reload = vi.fn()
    // Only reload is read, so a plain object copy of jsdom's location is enough.
    vi.stubGlobal('location', { ...window.location, reload })

    render(
      <ErrorBoundary>
        <Broken />
      </ErrorBoundary>,
    )
    await userEvent.setup().click(screen.getByRole('button', { name: 'Reload' }))

    expect(reload).toHaveBeenCalledTimes(1)
  })
})
