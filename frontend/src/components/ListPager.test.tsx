import { render, screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it, vi } from 'vitest'
import { ListPager } from './ListPager'

describe('ListPager', () => {
  it('renders nothing before the first answer', () => {
    const { container } = render(<ListPager page={1} info={null} label="Rooms" onChange={vi.fn()} />)

    expect(container).toBeEmptyDOMElement()
  })

  it('renders nothing for an empty list', () => {
    const { container } = render(
      <ListPager page={1} info={{ pageSize: 20, totalCount: 0 }} label="Rooms" onChange={vi.fn()} />,
    )

    expect(container).toBeEmptyDOMElement()
  })

  it('labels the pager and shows the range for the current page', () => {
    render(<ListPager page={2} info={{ pageSize: 10, totalCount: 35 }} label="Rooms" onChange={vi.fn()} />)

    expect(screen.getByLabelText('Rooms pages')).toBeInTheDocument()
    expect(screen.getByText('11–20 of 35')).toBeInTheDocument()
  })

  it('asks for the next page as a one-based number', async () => {
    const onChange = vi.fn()
    render(<ListPager page={1} info={{ pageSize: 10, totalCount: 35 }} label="Rooms" onChange={onChange} />)

    await userEvent.setup().click(screen.getByRole('button', { name: /next page/i }))

    expect(onChange).toHaveBeenCalledWith(2)
  })

  it('asks for the previous page', async () => {
    const onChange = vi.fn()
    render(<ListPager page={3} info={{ pageSize: 10, totalCount: 35 }} label="Rooms" onChange={onChange} />)

    await userEvent.setup().click(screen.getByRole('button', { name: /previous page/i }))

    expect(onChange).toHaveBeenCalledWith(2)
  })

  it('disables Previous on the first page and Next on the last page', () => {
    const { rerender } = render(
      <ListPager page={1} info={{ pageSize: 10, totalCount: 35 }} label="Rooms" onChange={vi.fn()} />,
    )
    expect(screen.getByRole('button', { name: /previous page/i })).toBeDisabled()

    rerender(<ListPager page={4} info={{ pageSize: 10, totalCount: 35 }} label="Rooms" onChange={vi.fn()} />)
    expect(screen.getByRole('button', { name: /next page/i })).toBeDisabled()
  })

  it('goes back to page 1 with the new size when the page size changes', async () => {
    const onChange = vi.fn()
    const user = userEvent.setup()
    render(<ListPager page={3} info={{ pageSize: 10, totalCount: 135 }} label="Rooms" onChange={onChange} />)

    await user.click(screen.getByRole('combobox'))
    await user.click(within(screen.getByRole('listbox')).getByRole('option', { name: '50' }))

    expect(onChange).toHaveBeenCalledWith(1, 50)
  })

  it('adds a page size the API used that is not one of the standard choices', async () => {
    const user = userEvent.setup()
    render(<ListPager page={1} info={{ pageSize: 25, totalCount: 100 }} label="Rooms" onChange={vi.fn()} />)

    await user.click(screen.getByRole('combobox'))

    expect(within(screen.getByRole('listbox')).getAllByRole('option').map((option) => option.textContent)).toEqual([
      '10',
      '20',
      '25',
      '50',
      '100',
    ])
  })
})
