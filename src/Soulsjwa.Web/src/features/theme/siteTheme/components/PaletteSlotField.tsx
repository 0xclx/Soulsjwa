import Stack from '@mui/material/Stack'
import TextField from '@mui/material/TextField'
import Typography from '@mui/material/Typography'
import Box from '@mui/material/Box'
import { contrastRatio, isValidHex } from '../contrast'

interface PaletteSlotFieldProps {
  label: string
  value: string
  onChange: (value: string) => void
  /** The mode's Default slot value, to show a live 4.5:1 contrast readout against. Omit for the Default slot itself. */
  contrastAgainst?: string
  disabled?: boolean
}

/**
 * One palette slot: a native colour swatch synced to a hex text field, plus
 * a live WCAG contrast readout against the mode's Default slot. The
 * readout is a UI hint only — the server (`SiteThemeValidator`) is the
 * authoritative 4.5:1 check and is what actually rejects an invalid save.
 */
export const PaletteSlotField = ({
  label,
  value,
  onChange,
  contrastAgainst,
  disabled,
}: PaletteSlotFieldProps) => {
  const valid = isValidHex(value)
  const ratio =
    valid && contrastAgainst && isValidHex(contrastAgainst)
      ? contrastRatio(value, contrastAgainst)
      : null
  const passesAA = ratio !== null && ratio >= 4.5

  return (
    <Stack direction="row" sx={{ alignItems: 'center', gap: 1 }}>
      <Box
        component="input"
        type="color"
        value={valid ? value : '#000000'}
        onChange={(e: React.ChangeEvent<HTMLInputElement>) => onChange(e.target.value)}
        disabled={disabled}
        aria-label={`${label} colour swatch`}
        sx={{ width: 36, height: 36, p: 0, border: 1, borderColor: 'divider', borderRadius: 1 }}
      />
      <TextField
        label={label}
        value={value}
        onChange={(e) => onChange(e.target.value)}
        disabled={disabled}
        size="small"
        error={!valid}
        helperText={!valid ? 'Must be #rrggbb.' : undefined}
        slotProps={{ htmlInput: { maxLength: 7 } }}
        sx={{ width: 160 }}
      />
      {ratio !== null && (
        <Typography variant="caption" color={passesAA ? 'success.main' : 'error.main'}>
          {ratio.toFixed(2)}:1 {passesAA ? '✓ AA' : '✗ below 4.5:1'}
        </Typography>
      )}
    </Stack>
  )
}
