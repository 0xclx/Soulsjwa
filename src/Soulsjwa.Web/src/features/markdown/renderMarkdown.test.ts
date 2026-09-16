import { describe, expect, it } from 'vitest'
import { renderMarkdown } from './renderMarkdown'

describe('renderMarkdown', () => {
  // html: false means raw HTML in the source is escaped to inert text, not
  // parsed — so a payload's letters can still appear on the page (e.g. the
  // word "onerror"), but never as a real tag or attribute a browser would
  // act on. These assertions check for the actual DOM-meaningful markup
  // (an unescaped opening tag, a working href) rather than raw substrings,
  // which the escaped-text case would trivially still contain.
  it('never emits a real <script> tag for a raw script payload', () => {
    const html = renderMarkdown('<script>alert(1)</script>')
    expect(html).not.toContain('<script>')
    expect(html).not.toContain('<script ')
  })

  it('never emits a real <img> tag for a raw onerror payload', () => {
    const html = renderMarkdown('<img src=x onerror=alert(1)>')
    expect(html).not.toContain('<img')
  })

  it('never emits a link with a javascript: href', () => {
    const html = renderMarkdown('[x](javascript:alert(1))')
    expect(html).not.toContain('<a ')
    expect(html).not.toMatch(/href=["']javascript:/i)
  })

  it('never emits a link with a data: href', () => {
    const html = renderMarkdown('[x](data:text/html,alert(1))')
    expect(html).not.toContain('<a ')
    expect(html).not.toMatch(/href=["']data:/i)
  })

  it('adds rel and target="_blank" to absolute links', () => {
    const html = renderMarkdown('[docs](https://example.com/page)')
    expect(html).toContain('rel="noopener noreferrer nofollow"')
    expect(html).toContain('target="_blank"')
    expect(html).toContain('href="https://example.com/page"')
  })

  it('adds rel but not target to relative links', () => {
    const html = renderMarkdown('[home](/events)')
    expect(html).toContain('rel="noopener noreferrer nofollow"')
    expect(html).not.toContain('target="_blank"')
  })

  it('autolinks bare URLs via linkify', () => {
    const html = renderMarkdown('See https://example.com for details.')
    expect(html).toContain('href="https://example.com"')
    expect(html).toContain('target="_blank"')
  })

  it('renders ordinary Markdown', () => {
    const html = renderMarkdown('# Title\n\nSome **bold** text.')
    expect(html).toContain('<h1>Title</h1>')
    expect(html).toContain('<strong>bold</strong>')
  })
})
