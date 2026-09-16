import { afterEach, describe, expect, it } from 'vitest'
import type { AxiosAdapter } from 'axios'
import { apiClient } from './apiClient'

/**
 * A GET's ETag response header should be remembered per-URL and sent back as
 * If-Match on a later PUT to that same URL, so the API can reject a stale
 * write with 409 instead of silently overwriting a concurrent change.
 * Exercised through the real interceptor chain via a fake axios adapter
 * (no real network, no extra mocking dependency) rather than through
 * etagCache directly, so a regression in the interceptor wiring itself would
 * fail this test.
 */
describe('apiClient ETag/If-Match interceptors', () => {
  afterEach(() => {
    apiClient.defaults.adapter = undefined
  })

  const installFakeAdapter = (
    handler: (config: Parameters<AxiosAdapter>[0]) => {
      status: number
      headers?: Record<string, string>
    },
  ) => {
    const adapter: AxiosAdapter = async (config) => {
      const result = handler(config)
      return {
        data: {},
        status: result.status,
        statusText: 'OK',
        headers: result.headers ?? {},
        config,
      }
    }
    apiClient.defaults.adapter = adapter
  }

  it('caches a GET response ETag and sends it as If-Match on a later PUT to the same URL', async () => {
    installFakeAdapter((config) => {
      if (config.method === 'get') return { status: 200, headers: { etag: '"42"' } }
      return { status: 200 }
    })

    await apiClient.get('/theme')

    let capturedIfMatch: unknown
    installFakeAdapter((config) => {
      capturedIfMatch = config.headers?.['If-Match']
      return { status: 200 }
    })

    await apiClient.put('/theme', {})

    expect(capturedIfMatch).toBe('"42"')
  })

  it('does not send If-Match for a URL with no prior GET', async () => {
    let capturedIfMatch: unknown = 'not-set'
    installFakeAdapter((config) => {
      capturedIfMatch = config.headers?.['If-Match']
      return { status: 200 }
    })

    await apiClient.put('/theme-never-fetched', {})

    expect(capturedIfMatch).toBeFalsy()
  })

  it('does not override an explicitly-set If-Match', async () => {
    installFakeAdapter((config) => {
      if (config.method === 'get') return { status: 200, headers: { etag: '"cached-value"' } }
      return { status: 200 }
    })
    await apiClient.get('/legal/impressum')

    let capturedIfMatch: unknown
    installFakeAdapter((config) => {
      capturedIfMatch = config.headers?.['If-Match']
      return { status: 200 }
    })

    await apiClient.put('/legal/impressum', {}, { headers: { 'If-Match': '"explicit-value"' } })

    expect(capturedIfMatch).toBe('"explicit-value"')
  })

  it('updates the cached ETag after a successful PUT response', async () => {
    installFakeAdapter((config) => {
      if (config.method === 'get') return { status: 200, headers: { etag: '"1"' } }
      return { status: 200, headers: { etag: '"2"' } }
    })

    await apiClient.get('/theme')
    await apiClient.put('/theme', {})

    let capturedIfMatch: unknown
    installFakeAdapter((config) => {
      capturedIfMatch = config.headers?.['If-Match']
      return { status: 200 }
    })

    await apiClient.put('/theme', {})

    expect(capturedIfMatch).toBe('"2"')
  })
})
