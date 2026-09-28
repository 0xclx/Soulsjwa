import { useId, useState, type FormEvent } from 'react'
import Button from '@mui/material/Button'
import Stack from '@mui/material/Stack'
import TextField from '@mui/material/TextField'

/** Mirrors the server's `User.DisplayNameOverrideMaxLength`. */
export const DISPLAY_NAME_MAX_LENGTH = 50

const DISPLAY_NAME_TEXT = {
  label: 'Display name',
  save: 'Save',
  resetLabel: (twitchDisplayName: string) => `Use Twitch name (${twitchDisplayName})`,
} as const

interface DisplayNameFieldProps {
  /** The name shown today; the field starts from it. */
  currentName: string
  twitchDisplayName: string
  /** Whether a custom name is set, so there is a Twitch name to go back to. */
  hasOverride: boolean
  pending: boolean
  /** The server's validation message, shown on the field. */
  error: string | null
  /** Called with the trimmed name. */
  onSave: (displayName: string) => void
  onReset: () => void
}

/**
 * Edits a display name override: a labelled field capped at 50 characters
 * with a counter, Save, and a way back to the Twitch name. Shared by the
 * Profile page and the admin Users tab. Remount it (via `key`) to restart
 * from a new `currentName`.
 */
export const DisplayNameField = ({
  currentName,
  twitchDisplayName,
  hasOverride,
  pending,
  error,
  onSave,
  onReset,
}: DisplayNameFieldProps) => {
  const [value, setValue] = useState(currentName)
  const helperId = useId()
  const trimmed = value.trim()
  const canSave = !pending && trimmed !== '' && trimmed !== currentName

  const submit = (e: FormEvent) => {
    e.preventDefault()
    if (canSave) onSave(trimmed)
  }

  return (
    <Stack component="form" spacing={1.5} onSubmit={submit} noValidate>
      <TextField
        label={DISPLAY_NAME_TEXT.label}
        value={value}
        onChange={(e) => setValue(e.target.value)}
        disabled={pending}
        error={error !== null}
        helperText={error ?? `${value.length}/${DISPLAY_NAME_MAX_LENGTH}`}
        fullWidth
        slotProps={{
          htmlInput: { maxLength: DISPLAY_NAME_MAX_LENGTH, 'aria-describedby': helperId },
          formHelperText: { id: helperId },
        }}
      />
      <Stack direction={{ xs: 'column', sm: 'row' }} spacing={1}>
        <Button type="submit" variant="contained" disabled={!canSave}>
          {DISPLAY_NAME_TEXT.save}
        </Button>
        {hasOverride && (
          <Button onClick={onReset} disabled={pending}>
            {DISPLAY_NAME_TEXT.resetLabel(twitchDisplayName)}
          </Button>
        )}
      </Stack>
    </Stack>
  )
}
