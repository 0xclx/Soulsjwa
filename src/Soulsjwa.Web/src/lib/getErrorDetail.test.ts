import { describe, it, expect } from 'vitest'
import { getErrorDetail } from './getErrorDetail'

const axiosError = (data: unknown) => ({ response: { data } })

describe('getErrorDetail', () => {
  it('prefers detail when present', () => {
    expect(getErrorDetail(axiosError({ detail: 'boom' }), 'fallback')).toBe('boom')
  })

  it('flattens the errors validation dictionary when detail is absent', () => {
    const err = axiosError({
      errors: { palette: ['Accent and Default fail contrast.'], font: ['font is invalid.'] },
    })
    expect(getErrorDetail(err, 'fallback')).toBe(
      'Accent and Default fail contrast. font is invalid.',
    )
  })

  it('falls back when neither detail nor errors are present', () => {
    expect(getErrorDetail(axiosError({}), 'fallback')).toBe('fallback')
  })

  it('falls back for a non-axios error', () => {
    expect(getErrorDetail(new Error('network down'), 'fallback')).toBe('fallback')
  })
})
