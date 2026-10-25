import { act, render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import type { ComponentProps } from 'react'
import { describe, expect, it, vi } from 'vitest'
import { ApiError } from '../api/client'
import { ConfirmActionDialog } from './ConfirmActionDialog'

function setup(overrides: Partial<ComponentProps<typeof ConfirmActionDialog>> = {}) {
  const props = {
    title: 'Cancel reservation',
    description: 'This frees the room.',
    confirmLabel: 'Cancel it',
    run: vi.fn(() => Promise.resolve()),
    onClose: vi.fn(),
    onDone: vi.fn(),
    onStale: vi.fn(),
    ...overrides,
  }
  render(<ConfirmActionDialog {...props} />)
  return { props, user: userEvent.setup() }
}

describe('ConfirmActionDialog', () => {
  it('shows the title, the description and both buttons', () => {
    setup()

    expect(screen.getByRole('dialog', { name: 'Cancel reservation' })).toBeInTheDocument()
    expect(screen.getByText('This frees the room.')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Back' })).toBeEnabled()
    expect(screen.getByRole('button', { name: 'Cancel it' })).toBeEnabled()
  })

  it('runs the call and then reports done', async () => {
    const { props, user } = setup()

    await user.click(screen.getByRole('button', { name: 'Cancel it' }))

    await waitFor(() => expect(props.onDone).toHaveBeenCalledTimes(1))
    expect(props.run).toHaveBeenCalledTimes(1)
    expect(props.onStale).not.toHaveBeenCalled()
  })

  it('closes with Back without running anything', async () => {
    const { props, user } = setup()

    await user.click(screen.getByRole('button', { name: 'Back' }))

    expect(props.onClose).toHaveBeenCalledTimes(1)
    expect(props.run).not.toHaveBeenCalled()
  })

  it('shows Saving and disables both buttons while the call runs', async () => {
    let finish: () => void = () => undefined
    const run = vi.fn(() => new Promise<void>((resolve) => (finish = resolve)))
    const { props, user } = setup({ run })

    await user.click(screen.getByRole('button', { name: 'Cancel it' }))

    expect(await screen.findByRole('button', { name: 'Saving…' })).toBeDisabled()
    expect(screen.getByRole('button', { name: 'Back' })).toBeDisabled()

    finish()
    await waitFor(() => expect(props.onDone).toHaveBeenCalledTimes(1))
  })

  it('starts the call once even when the button is clicked twice quickly', async () => {
    let finish: () => void = () => undefined
    const run = vi.fn(() => new Promise<void>((resolve) => (finish = resolve)))
    const { props } = setup({ run })
    const button = screen.getByRole('button', { name: 'Cancel it' })

    // Both clicks run before React re-renders, so the disabled button cannot stop the second one: only the ref can.
    act(() => {
      button.click()
      button.click()
    })

    expect(run).toHaveBeenCalledTimes(1)
    finish()
    await waitFor(() => expect(props.onDone).toHaveBeenCalledTimes(1))
  })

  it('ignores Escape while the call is running', async () => {
    let finish: () => void = () => undefined
    const run = vi.fn(() => new Promise<void>((resolve) => (finish = resolve)))
    const { props, user } = setup({ run })

    await user.click(screen.getByRole('button', { name: 'Cancel it' }))
    await screen.findByRole('button', { name: 'Saving…' })
    await user.keyboard('{Escape}')

    expect(props.onClose).not.toHaveBeenCalled()
    finish()
    await waitFor(() => expect(props.onDone).toHaveBeenCalledTimes(1))
  })

  it('closes on Escape when nothing is running', async () => {
    const { props, user } = setup()

    await user.keyboard('{Escape}')

    expect(props.onClose).toHaveBeenCalledTimes(1)
  })

  it('reports nothing when the dialog is closed while the call is still running', async () => {
    let finish: () => void = () => undefined
    const run = vi.fn(() => new Promise<void>((resolve) => (finish = resolve)))
    const props = {
      title: 'Cancel reservation',
      description: 'This frees the room.',
      confirmLabel: 'Cancel it',
      run,
      onClose: vi.fn(),
      onDone: vi.fn(),
      onStale: vi.fn(),
    }
    const { unmount } = render(<ConfirmActionDialog {...props} />)

    await userEvent.setup().click(screen.getByRole('button', { name: 'Cancel it' }))
    await screen.findByRole('button', { name: 'Saving…' })
    unmount()
    await act(async () => {
      finish()
      await Promise.resolve()
    })

    expect(props.onDone).not.toHaveBeenCalled()
    expect(props.onStale).not.toHaveBeenCalled()
  })

  it('keeps the dialog open and shows the errors when the call fails', async () => {
    const run = vi.fn(() =>
      Promise.reject(new ApiError(422, [{ code: 'R4_PAST_START', message: 'The start is in the past.' }])),
    )
    const { props, user } = setup({ run })

    await user.click(screen.getByRole('button', { name: 'Cancel it' }))

    expect(await screen.findByText('R4_PAST_START -- The start is in the past.')).toBeInTheDocument()
    expect(props.onDone).not.toHaveBeenCalled()
    expect(props.onStale).not.toHaveBeenCalled()
    expect(screen.getByRole('button', { name: 'Cancel it' })).toBeEnabled()
  })

  it.each([404, 409])('tells the page the row is stale on status %i, and still shows the error', async (status) => {
    const run = vi.fn(() => Promise.reject(new ApiError(status, [{ code: 'NOT_FOUND', message: 'Gone.' }])))
    const { props, user } = setup({ run })

    await user.click(screen.getByRole('button', { name: 'Cancel it' }))

    await waitFor(() => expect(props.onStale).toHaveBeenCalledTimes(1))
    expect(screen.getByText('NOT_FOUND -- Gone.')).toBeInTheDocument()
    expect(props.onDone).not.toHaveBeenCalled()
  })

  it('shows a generic error for something that is not an ApiError', async () => {
    const run = vi.fn(() => Promise.reject(new Error('boom')))
    const { user } = setup({ run })

    await user.click(screen.getByRole('button', { name: 'Cancel it' }))

    expect(
      await screen.findByText('UNEXPECTED_ERROR -- Something unexpected happened. Please try again.'),
    ).toBeInTheDocument()
  })

  it('clears the old errors when the call is tried again', async () => {
    const run = vi
      .fn<() => Promise<void>>()
      .mockRejectedValueOnce(new ApiError(422, [{ code: 'R2_ROOM_OVERLAP', message: 'Taken.' }]))
      .mockResolvedValueOnce(undefined)
    const { props, user } = setup({ run })

    await user.click(screen.getByRole('button', { name: 'Cancel it' }))
    await screen.findByText('R2_ROOM_OVERLAP -- Taken.')
    await user.click(screen.getByRole('button', { name: 'Cancel it' }))

    await waitFor(() => expect(props.onDone).toHaveBeenCalledTimes(1))
    expect(screen.queryByText('R2_ROOM_OVERLAP -- Taken.')).not.toBeInTheDocument()
  })
})
