import { describe, it, expect } from 'vitest'
import type { OverlayTokenSettings } from '../../../types/overlay'
import {
  OVERLAY_DEFAULT_SETTINGS,
  applyOverlaySettings,
  isOverlayPreviewRequest,
  parseOverlayConfig,
} from './overlayConfig'

describe('parseOverlayConfig', () => {
  it('returns sensible defaults for an empty query string', () => {
    const c = parseOverlayConfig('')
    expect(c.gameIds).toBeNull()
    expect(c.playerIds).toBeNull()
    expect(c.pageSize).toBe(10)
    expect(c.cycleSeconds).toBe(30)
    expect(c.showProgress).toBe(true)
    expect(c.showPagination).toBe(true)
    expect(c.showTitle).toBe(true)
    expect(c.highlight).toBe(true)
    expect(c.highlightSeconds).toBe(6)
    expect(c.animate).toBe(true)
    expect(c.theme).toBe('dark')
    expect(c.background).toBeNull()
    expect(c.panelOpacity).toBe(80)
    expect(c.refreshSeconds).toBe(5)
    expect(c.view).toBe('objectives')
    expect(c.title).toBeNull()
  })

  it('parses comma-separated game and player id filters', () => {
    const c = parseOverlayConfig('games=a,b,c&players=u1,u2')
    expect(c.gameIds).toEqual(['a', 'b', 'c'])
    expect(c.playerIds).toEqual(['u1', 'u2'])
  })

  it('treats "all" / "*" / empty as "no filter"', () => {
    expect(parseOverlayConfig('games=all').gameIds).toBeNull()
    expect(parseOverlayConfig('games=*').gameIds).toBeNull()
    expect(parseOverlayConfig('games=').gameIds).toBeNull()
  })

  it('accepts a variety of boolean representations', () => {
    expect(parseOverlayConfig('showProgress=0').showProgress).toBe(false)
    expect(parseOverlayConfig('showProgress=false').showProgress).toBe(false)
    expect(parseOverlayConfig('showProgress=off').showProgress).toBe(false)
    expect(parseOverlayConfig('animate=yes').animate).toBe(true)
    expect(parseOverlayConfig('animate=garbage').animate).toBe(true) // fallback to default
  })

  it('clamps numeric values into safe ranges', () => {
    expect(parseOverlayConfig('pageSize=99999').pageSize).toBe(50)
    expect(parseOverlayConfig('pageSize=-1').pageSize).toBe(1)
    expect(parseOverlayConfig('cycle=0').cycleSeconds).toBe(0)
    expect(parseOverlayConfig('refresh=1').refreshSeconds).toBe(2) // min 2
    expect(parseOverlayConfig('refresh=notanumber').refreshSeconds).toBe(5)
  })

  it('only accepts known theme and view values', () => {
    expect(parseOverlayConfig('theme=light').theme).toBe('light')
    expect(parseOverlayConfig('theme=neon').theme).toBe('dark')
    expect(parseOverlayConfig('view=games').view).toBe('games')
    expect(parseOverlayConfig('view=bogus').view).toBe('objectives')
  })

  it('passes through a valid background color and the title', () => {
    const c = parseOverlayConfig('bg=%23000000&title=Race%20Night')
    expect(c.background).toBe('#000000')
    expect(c.title).toBe('Race Night')
  })

  it('accepts every supported bg colour form', () => {
    expect(parseOverlayConfig('bg=%23ff0000').background).toBe('#ff0000')
    expect(parseOverlayConfig('bg=%23f00').background).toBe('#f00')
    expect(parseOverlayConfig('bg=%23ff0000aa').background).toBe('#ff0000aa')
    expect(parseOverlayConfig('bg=rgba(0,0,0,0.5)').background).toBe('rgba(0,0,0,0.5)')
    expect(parseOverlayConfig('bg=rgb(0 0 0 / 50%)').background).toBe('rgb(0 0 0 / 50%)')
    expect(parseOverlayConfig('bg=hsl(200, 50%25, 50%25)').background).toBe('hsl(200, 50%, 50%)')
    expect(parseOverlayConfig('bg=transparent').background).toBe('transparent')
    expect(parseOverlayConfig('bg=RED').background).toBe('red')
  })

  it('rejects a bg value that attempts to break out of the CSS declaration', () => {
    expect(parseOverlayConfig('bg=red%3B%7Dbody%7Bdisplay%3Anone%7D').background).toBeNull()
    expect(parseOverlayConfig('bg=url(https%3A%2F%2Fevil.example%2Fx)').background).toBeNull()
    expect(parseOverlayConfig('bg=%7Bbroken').background).toBeNull()
    expect(parseOverlayConfig('bg=%5C').background).toBeNull()
  })

  it('rejects a bg value that is not a recognisable colour at all', () => {
    expect(parseOverlayConfig('bg=not-a-color').background).toBeNull()
    expect(parseOverlayConfig('bg=javascript%3Aalert(1)').background).toBeNull()
  })

  it('does not let an invalid bg change any other overlay knob', () => {
    const c = parseOverlayConfig('bg=red%3B%7Dbody%7Bdisplay%3Anone%7D&theme=light&view=games')
    expect(c.background).toBeNull()
    expect(c.theme).toBe('light')
    expect(c.view).toBe('games')
  })
})

describe('applyOverlaySettings', () => {
  it('leaves the URL in charge when nothing is saved on the token', () => {
    const fromUrl = parseOverlayConfig('view=games&theme=light')
    expect(applyOverlaySettings(fromUrl, null)).toBe(fromUrl)
    expect(applyOverlaySettings(fromUrl, undefined)).toBe(fromUrl)
  })

  it('lets a saved look win over the URL for every knob it carries', () => {
    const fromUrl = parseOverlayConfig('view=games&theme=light&pageSize=3&title=From%20URL')
    const saved: OverlayTokenSettings = {
      ...OVERLAY_DEFAULT_SETTINGS,
      view: 'scores',
      theme: 'dark',
      pageSize: 7,
      title: null,
    }

    const applied = applyOverlaySettings(fromUrl, saved)

    expect(applied.view).toBe('scores')
    expect(applied.theme).toBe('dark')
    expect(applied.pageSize).toBe(7)
    // A saved "no title" is a decision too — the URL's title does not leak through.
    expect(applied.title).toBeNull()
  })

  it('keeps the page background from the URL, the one knob a saved look never carries', () => {
    const fromUrl = parseOverlayConfig('bg=%23000000')

    expect(applyOverlaySettings(fromUrl, OVERLAY_DEFAULT_SETTINGS).background).toBe('#000000')
  })
})

describe('isOverlayPreviewRequest', () => {
  it('is off unless the preview flag is a truthy spelling', () => {
    expect(isOverlayPreviewRequest(new URLSearchParams(''))).toBe(false)
    expect(isOverlayPreviewRequest(new URLSearchParams('preview=0'))).toBe(false)
    expect(isOverlayPreviewRequest(new URLSearchParams('preview=1'))).toBe(true)
    expect(isOverlayPreviewRequest(new URLSearchParams('preview=true'))).toBe(true)
  })
})
