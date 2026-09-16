import { describe, it, expect } from 'vitest'
import { render, screen } from '@testing-library/react'
import { CardListSkeleton, TableSkeleton } from './LoadingSkeletons'

describe('loading skeletons', () => {
  it('renders an accessible busy card list with the requested label', () => {
    render(<CardListSkeleton count={3} label="Loading events…" />)
    const status = screen.getByRole('status', { name: 'Loading events…' })
    expect(status).toHaveAttribute('aria-busy', 'true')
  })

  it('renders an accessible busy table placeholder', () => {
    render(<TableSkeleton label="Loading table…" />)
    const status = screen.getByRole('status', { name: 'Loading table…' })
    expect(status).toHaveAttribute('aria-busy', 'true')
  })
})
