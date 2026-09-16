import { useContext } from 'react'
import { ThemeModeContext, type ThemeModeContextValue } from './ThemeModeContext'

export const useThemeMode = (): ThemeModeContextValue => {
  const ctx = useContext(ThemeModeContext)
  if (!ctx) {
    throw new Error('useThemeMode must be used within a ThemeModeProvider')
  }
  return ctx
}
