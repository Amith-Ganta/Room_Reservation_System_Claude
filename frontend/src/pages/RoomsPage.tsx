import {
  Box,
  Button,
  Checkbox,
  Chip,
  CircularProgress,
  FormControl,
  InputLabel,
  ListItemText,
  MenuItem,
  OutlinedInput,
  Paper,
  Select,
  Stack,
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableHead,
  TableRow,
  TextField,
  Typography,
} from '@mui/material'
import { useEffect, useState } from 'react'
import { ApiError } from '../api/client'
import { listRooms } from '../api/rooms'
import type { ApiErrorItem, EquipmentName, RoomDto } from '../api/types'
import { ErrorAlert } from '../components/ErrorAlert'
import { ALL_EQUIPMENT, EQUIPMENT_LABELS } from '../lib/equipment'

const TYPING_DELAY_MS = 300

interface TextFilters {
  minCapacity: string
  floor: string
}

const NO_TEXT_FILTERS: TextFilters = { minCapacity: '', floor: '' }

/** Turns the text of a number field into a whole number, or undefined when it is empty or not a whole number. */
function toWholeNumber(text: string): number | undefined {
  const trimmed = text.trim()
  return /^-?\d+$/.test(trimmed) ? Number(trimmed) : undefined
}

/** Minimum capacity must be a whole number >= 1. */
function toCapacity(text: string): number | undefined {
  const value = toWholeNumber(text)
  return value !== undefined && value >= 1 ? value : undefined
}

function isBadCapacity(text: string): boolean {
  return text.trim() !== '' && toCapacity(text) === undefined
}

function isBadFloor(text: string): boolean {
  return text.trim() !== '' && toWholeNumber(text) === undefined
}

export function RoomsPage() {
  const [typed, setTyped] = useState<TextFilters>(NO_TEXT_FILTERS)
  const [equipment, setEquipment] = useState<EquipmentName[]>([])

  // What was actually requested: the number fields are applied after a short pause in typing.
  const [applied, setApplied] = useState<TextFilters>(NO_TEXT_FILTERS)

  const [rooms, setRooms] = useState<RoomDto[]>([])
  const [loading, setLoading] = useState(true)
  const [errors, setErrors] = useState<ApiErrorItem[]>([])

  useEffect(() => {
    // Keep the previous object when nothing changed, so typing and deleting does not refetch.
    const timer = window.setTimeout(
      () =>
        setApplied((previous) =>
          previous.minCapacity === typed.minCapacity && previous.floor === typed.floor ? previous : typed,
        ),
      TYPING_DELAY_MS,
    )
    return () => window.clearTimeout(timer)
  }, [typed])

  useEffect(() => {
    // Aborting the previous request means an older response can never replace a newer one.
    const controller = new AbortController()
    setLoading(true)
    setErrors([])
    listRooms(
      {
        minCapacity: toCapacity(applied.minCapacity),
        floor: toWholeNumber(applied.floor),
        equipment,
      },
      controller.signal,
    )
      .then((result) => {
        if (controller.signal.aborted) {
          return
        }
        setRooms(result)
        setLoading(false)
      })
      .catch((error: unknown) => {
        if (controller.signal.aborted) {
          return
        }
        setRooms([])
        setErrors(
          error instanceof ApiError
            ? error.errors
            : [{ code: 'UNEXPECTED_ERROR', message: 'Something unexpected happened. Please try again.' }],
        )
        setLoading(false)
      })
    return () => controller.abort()
  }, [applied, equipment])

  function clearFilters() {
    setTyped(NO_TEXT_FILTERS)
    setApplied(NO_TEXT_FILTERS)
    setEquipment([])
  }

  return (
    <Stack spacing={2}>
      <Typography variant="h4" component="h1">
        Rooms
      </Typography>

      <Paper sx={{ p: 2 }}>
        <Stack direction={{ xs: 'column', md: 'row' }} spacing={2} sx={{ alignItems: { md: 'center' } }}>
          <TextField
            label="Minimum capacity"
            type="number"
            size="small"
            value={typed.minCapacity}
            onChange={(event) => setTyped({ ...typed, minCapacity: event.target.value })}
            error={isBadCapacity(typed.minCapacity)}
            helperText={isBadCapacity(typed.minCapacity) ? 'Enter a whole number, 1 or more' : undefined}
            slotProps={{ htmlInput: { min: 1, step: 1 } }}
            sx={{ width: { md: 180 } }}
          />
          <FormControl size="small" sx={{ minWidth: 240 }}>
            <InputLabel id="equipment-label">Equipment</InputLabel>
            <Select
              labelId="equipment-label"
              multiple
              value={equipment}
              onChange={(event) => {
                const value = event.target.value
                setEquipment(typeof value === 'string' ? (value.split(',') as EquipmentName[]) : value)
              }}
              input={<OutlinedInput label="Equipment" />}
              renderValue={(selected) => selected.map((name) => EQUIPMENT_LABELS[name]).join(', ')}
            >
              {ALL_EQUIPMENT.map((name) => (
                <MenuItem key={name} value={name}>
                  <Checkbox checked={equipment.includes(name)} />
                  <ListItemText primary={EQUIPMENT_LABELS[name]} />
                </MenuItem>
              ))}
            </Select>
          </FormControl>
          <TextField
            label="Floor"
            type="number"
            size="small"
            value={typed.floor}
            onChange={(event) => setTyped({ ...typed, floor: event.target.value })}
            error={isBadFloor(typed.floor)}
            helperText={isBadFloor(typed.floor) ? 'Enter a whole number' : undefined}
            slotProps={{ htmlInput: { step: 1 } }}
            sx={{ width: { md: 120 } }}
          />
          <Button onClick={clearFilters}>
            Clear filters
          </Button>
        </Stack>
      </Paper>

      <ErrorAlert errors={errors} />

      <TableContainer component={Paper}>
        <Table size="small" aria-label="Rooms">
          <TableHead>
            <TableRow>
              <TableCell>Name</TableCell>
              <TableCell>Id</TableCell>
              <TableCell align="right">Capacity</TableCell>
              <TableCell align="right">Floor</TableCell>
              <TableCell>Equipment</TableCell>
              <TableCell>Status</TableCell>
            </TableRow>
          </TableHead>
          <TableBody>
            {loading ? (
              <TableRow>
                <TableCell colSpan={6} align="center">
                  <CircularProgress size={28} aria-label="Loading rooms" />
                </TableCell>
              </TableRow>
            ) : rooms.length === 0 ? (
              <TableRow>
                <TableCell colSpan={6} align="center">
                  {errors.length > 0 ? 'No rooms to show.' : 'No rooms match these filters.'}
                </TableCell>
              </TableRow>
            ) : (
              rooms.map((room) => (
                <TableRow key={room.id} hover sx={room.isActive ? undefined : { opacity: 0.6 }}>
                  <TableCell>{room.name}</TableCell>
                  <TableCell>{room.id}</TableCell>
                  <TableCell align="right">{room.capacity}</TableCell>
                  <TableCell align="right">{room.floor}</TableCell>
                  <TableCell>
                    <Box sx={{ display: 'flex', flexWrap: 'wrap', gap: 0.5 }}>
                      {room.equipment.map((name) => (
                        <Chip key={name} size="small" variant="outlined" label={EQUIPMENT_LABELS[name]} />
                      ))}
                    </Box>
                  </TableCell>
                  <TableCell>
                    <Chip
                      size="small"
                      label={room.isActive ? 'Active' : 'Inactive'}
                      color={room.isActive ? 'success' : 'default'}
                    />
                  </TableCell>
                </TableRow>
              ))
            )}
          </TableBody>
        </Table>
      </TableContainer>
    </Stack>
  )
}
