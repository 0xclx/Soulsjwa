import { describe, it, expect, beforeEach } from 'vitest'
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { ThemeModeProvider } from '../theme/ThemeModeProvider'
import { ThemeToggle } from './ThemeToggle'

const renderToggle = () => {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  return render(
    <QueryClientProvider client={client}>
      <ThemeModeProvider>
        <ThemeToggle />
      </ThemeModeProvider>
    </QueryClientProvider>,
  )
}

describe('<ThemeToggle />', () => {
  beforeEach(() => {
    window.localStorage.clear()
  })

  it('renders an accessible button with a descriptive aria-label', () => {
    renderToggle()
    expect(screen.getByRole('button', { name: /switch to system theme/i })).toBeInTheDocument()
  })

  it('cycles the aria-label as the user clicks through modes', async () => {
    const user = userEvent.setup()
    renderToggle()

    const getButton = () => screen.getByRole('button')

    // dark → system
    await user.click(getButton())
    expect(getButton()).toHaveAccessibleName(/switch to light theme/i)

    // system → light
    await user.click(getButton())
    expect(getButton()).toHaveAccessibleName(/switch to dark theme/i)

    // light → dark
    await user.click(getButton())
    expect(getButton()).toHaveAccessibleName(/switch to system theme/i)
  })
})
