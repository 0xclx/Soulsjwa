import { act, renderHook } from '@testing-library/react'
import type { ReactNode } from 'react'
import { MemoryRouter, useLocation, useNavigationType } from 'react-router-dom'
import { describe, expect, it } from 'vitest'
import { useScoreboardView } from './useScoreboardView'

const renderAt = (url: string) =>
  renderHook(
    () => {
      const [view, setView] = useScoreboardView()
      return { view, setView, location: useLocation(), navigationType: useNavigationType() }
    },
    {
      wrapper: ({ children }: { children: ReactNode }) => (
        <MemoryRouter initialEntries={['/start', url]} initialIndex={1}>
          {children}
        </MemoryRouter>
      ),
    },
  )

describe('useScoreboardView', () => {
  it('defaults to the current-game view without a view param', () => {
    expect(renderAt('/scoreboard/e').result.current.view).toBe('game')
  })

  it('reads the whole-event view from ?view=event', () => {
    expect(renderAt('/scoreboard/e?view=event').result.current.view).toBe('event')
  })

  it('falls back to the default for an unknown view param', () => {
    expect(renderAt('/scoreboard/e?view=nope').result.current.view).toBe('game')
  })

  it('writes the view to the URL, keeping other params and replacing history', () => {
    const { result } = renderAt('/scoreboard/e?tab=scores')

    act(() => result.current.setView('event'))

    expect(result.current.view).toBe('event')
    const params = new URLSearchParams(result.current.location.search)
    expect(params.get('view')).toBe('event')
    expect(params.get('tab')).toBe('scores')
    expect(result.current.navigationType).toBe('REPLACE')
  })

  it('drops the param when switching back to the default view', () => {
    const { result } = renderAt('/scoreboard/e?view=event&tab=scores')

    act(() => result.current.setView('game'))

    expect(result.current.view).toBe('game')
    expect(result.current.location.search).toBe('?tab=scores')
  })
})
