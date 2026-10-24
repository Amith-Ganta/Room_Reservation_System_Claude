import { Box, CircularProgress } from '@mui/material'

interface LoadingIndicatorProps {
  label: string
}

/** Spinner with a live-region label, so screen readers announce that data is loading. */
export function LoadingIndicator({ label }: LoadingIndicatorProps) {
  return (
    <Box role="status" aria-live="polite" sx={{ display: 'inline-flex', alignItems: 'center', gap: 1 }}>
      <CircularProgress size={28} aria-hidden />
      <span>{label}…</span>
    </Box>
  )
}
