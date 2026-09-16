import { useState, useMemo } from 'react'
import Autocomplete from '@mui/material/Autocomplete'
import TextField from '@mui/material/TextField'
import Stack from '@mui/material/Stack'
import Chip from '@mui/material/Chip'
import Typography from '@mui/material/Typography'
import { useUserSearch } from '../hooks/useUserSearch'
import { useDebouncedValue } from '../../../lib/useDebouncedValue'
import type { UserSearchResult } from '../../../types'

export interface UserPickerOption {
  /** Existing user id, when one was selected from the search results. */
  userId?: string
  /** Raw Twitch handle entered freely, when no matching user was selected. */
  twitchLogin?: string
  label: string
}

interface Props {
  value: UserPickerOption | null
  onChange: (value: UserPickerOption | null) => void
  /** Allow entering a raw Twitch handle that doesn't yet match a known user. */
  allowFreeText?: boolean
  label?: string
  placeholder?: string
  disabled?: boolean
  size?: 'small' | 'medium'
  fullWidth?: boolean
  helperText?: string
}

const VALID_LOGIN = /^[a-zA-Z0-9_]{2,64}$/

/**
 * Combobox for picking an existing user or, when allowFreeText is true, a raw
 * Twitch handle that the backend will pre-allowlist + adopt on first login.
 */
export const UserPicker = ({
  value,
  onChange,
  allowFreeText = false,
  label = 'User',
  placeholder,
  disabled,
  size = 'small',
  fullWidth,
  helperText,
}: Props) => {
  const [input, setInput] = useState('')
  const debouncedInput = useDebouncedValue(input, 250)
  const { data, isFetching } = useUserSearch(debouncedInput)

  const options: UserSearchResult[] = useMemo(() => data ?? [], [data])

  return (
    <Autocomplete<UserSearchResult | string, false, false, boolean>
      // Only a real search-result selection feeds back into Autocomplete's
      // own `value` — a free-text commit is reflected purely through the
      // controlled `inputValue` below. Mirroring free text into `value` too
      // fights with Autocomplete's internal value/inputValue sync and
      // corrupts what's typed mid-keystroke.
      value={value?.userId ? value.label : null}
      onChange={(_, picked) => {
        if (!picked) {
          onChange(null)
          return
        }
        if (typeof picked === 'string') {
          const raw = picked.trim().toLowerCase()
          if (!VALID_LOGIN.test(raw)) {
            onChange(null)
            return
          }
          onChange({ twitchLogin: raw, label: raw })
        } else {
          onChange({ userId: picked.id, label: picked.displayName || picked.twitchLogin })
        }
      }}
      inputValue={input}
      onInputChange={(_, v, reason) => {
        setInput(v)
        // Commit (or clear) a free-text handle as the user types, so the
        // caller doesn't need an Enter keypress to know a valid handle is
        // selected. Only for genuine typing — option selection and reset
        // fire onInputChange too, but the Autocomplete onChange handler
        // above already covers those with the real picked value.
        if (!allowFreeText || reason !== 'input') return
        const raw = v.trim().toLowerCase()
        onChange(VALID_LOGIN.test(raw) ? { twitchLogin: raw, label: raw } : null)
      }}
      options={options}
      freeSolo={allowFreeText}
      autoHighlight
      loading={isFetching}
      disabled={disabled}
      fullWidth={fullWidth}
      size={size}
      filterOptions={(o) => o}
      getOptionLabel={(opt) =>
        typeof opt === 'string' ? opt : (opt.displayName ?? opt.twitchLogin)
      }
      isOptionEqualToValue={(opt, val) => {
        const o = typeof opt === 'string' ? opt : opt.id
        const v = typeof val === 'string' ? val : val.id
        return o === v
      }}
      renderOption={(props, opt) => {
        if (typeof opt === 'string') return null
        const { key, ...rest } = props as { key: string } & React.HTMLAttributes<HTMLLIElement>
        return (
          <li key={key} {...rest}>
            <Stack direction="row" spacing={1} sx={{ alignItems: 'center', width: '100%' }}>
              <Typography sx={{ flex: 1 }}>{opt.displayName}</Typography>
              <Typography variant="caption" color="text.secondary" sx={{ fontFamily: 'monospace' }}>
                {opt.twitchLogin}
              </Typography>
              {opt.isPending && <Chip label="pending" size="small" variant="outlined" />}
            </Stack>
          </li>
        )
      }}
      renderInput={(params) => (
        <TextField
          {...params}
          label={label}
          placeholder={placeholder}
          helperText={
            helperText ??
            (allowFreeText
              ? 'Pick a known user, or type a Twitch handle to pre-invite them.'
              : 'Search by Twitch login or display name.')
          }
        />
      )}
    />
  )
}
