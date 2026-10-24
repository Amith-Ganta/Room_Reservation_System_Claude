import { TablePagination } from '@mui/material'

/** What the last answer said about the list; the API is the source of truth for the page size it really used. */
export interface PageInfo {
  pageSize: number
  totalCount: number
}

interface ListPagerProps {
  /** Page number as the API counts it: the first page is 1. */
  page: number
  info: PageInfo | null
  label: string
  onChange: (page: number, pageSize?: number) => void
}

const PAGE_SIZE_CHOICES = [10, 20, 50, 100]

export function ListPager({ page, info, label, onChange }: ListPagerProps) {
  if (info === null || info.totalCount === 0) {
    return null
  }
  const choices = PAGE_SIZE_CHOICES.includes(info.pageSize)
    ? PAGE_SIZE_CHOICES
    : [...PAGE_SIZE_CHOICES, info.pageSize].sort((a, b) => a - b)
  return (
    <TablePagination
      component="div"
      aria-label={`${label} pages`}
      count={info.totalCount}
      page={page - 1}
      rowsPerPage={info.pageSize}
      rowsPerPageOptions={choices}
      onPageChange={(_event, zeroBased) => onChange(zeroBased + 1)}
      onRowsPerPageChange={(event) => onChange(1, Number(event.target.value))}
    />
  )
}
