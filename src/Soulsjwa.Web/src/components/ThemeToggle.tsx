import { memo } from 'react'
import IconButton from '@mui/material/IconButton'
import Tooltip from '@mui/material/Tooltip'
import LightModeIcon from '@mui/icons-material/LightMode'
import DarkModeIcon from '@mui/icons-material/DarkMode'
import SettingsBrightnessIcon from '@mui/icons-material/SettingsBrightness'
import { useThemeMode } from '../theme'
import type { ThemeMode } from '../theme'

const labels: Record<ThemeMode, string> = {
  light: 'Switch to dark theme',
  dark: 'Switch to system theme',
  system: 'Switch to light theme',
}

/**
 * Accessible theme switcher. Cycles light → dark → system → light.
 * Memoized so the AppBar does not rerender it on unrelated state changes.
 */
export const ThemeToggle = memo(function ThemeToggle() {
  const { mode, cycleMode } = useThemeMode()

  const icon =
    mode === 'light' ? (
      <LightModeIcon fontSize="small" />
    ) : mode === 'dark' ? (
      <DarkModeIcon fontSize="small" />
    ) : (
      <SettingsBrightnessIcon fontSize="small" />
    )

  return (
    <Tooltip title={labels[mode]}>
      <IconButton onClick={cycleMode} aria-label={labels[mode]} color="inherit" size="small">
        {icon}
      </IconButton>
    </Tooltip>
  )
})
