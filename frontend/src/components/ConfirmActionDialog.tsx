import { Button, Dialog, DialogActions, DialogContent, DialogContentText, DialogTitle } from '@mui/material'
import { useEffect, useRef, useState } from 'react'
import type { ApiErrorItem } from '../api/types'
import { isStaleError, toErrorItems } from '../lib/errors'
import { ErrorAlert } from './ErrorAlert'

interface ConfirmActionDialogProps {
  /** Short heading, for example "Cancel reservation". */
  title: string
  /** Sentence naming what is about to change. */
  description: string
  confirmLabel: string
  /** Sends the call. A rejected promise keeps the dialog open and shows the errors. */
  run: () => Promise<unknown>
  onClose: () => void
  onDone: () => void
  /** Called when the server says the row is out of date (404 or 409), so the page can reload the list. */
  onStale: () => void
}

/** Confirm step for a write. Render it only while it is needed: each opening starts with a clean state. */
export function ConfirmActionDialog({
  title,
  description,
  confirmLabel,
  run,
  onClose,
  onDone,
  onStale,
}: ConfirmActionDialogProps) {
  const [saving, setSaving] = useState(false)
  const [errors, setErrors] = useState<ApiErrorItem[]>([])
  // A ref is updated at once, so a fast double click cannot start the call twice.
  const busy = useRef(false)
  const mounted = useRef(true)

  useEffect(() => {
    mounted.current = true
    return () => {
      mounted.current = false
    }
  }, [])

  async function confirm() {
    if (busy.current) {
      return
    }
    busy.current = true
    setSaving(true)
    setErrors([])
    try {
      await run()
      if (mounted.current) {
        onDone()
      }
    } catch (error: unknown) {
      if (mounted.current) {
        setErrors(toErrorItems(error))
        if (isStaleError(error)) {
          onStale()
        }
      }
    } finally {
      busy.current = false
      if (mounted.current) {
        setSaving(false)
      }
    }
  }

  function close() {
    if (!busy.current) {
      onClose()
    }
  }

  return (
    <Dialog open onClose={close} aria-labelledby="confirm-action-title" fullWidth maxWidth="sm">
      <DialogTitle id="confirm-action-title">{title}</DialogTitle>
      <DialogContent>
        <DialogContentText sx={{ mb: errors.length > 0 ? 2 : 0 }}>{description}</DialogContentText>
        <ErrorAlert errors={errors} />
      </DialogContent>
      <DialogActions>
        <Button onClick={close} disabled={saving}>
          Back
        </Button>
        <Button variant="contained" onClick={() => void confirm()} disabled={saving}>
          {saving ? 'Saving…' : confirmLabel}
        </Button>
      </DialogActions>
    </Dialog>
  )
}
