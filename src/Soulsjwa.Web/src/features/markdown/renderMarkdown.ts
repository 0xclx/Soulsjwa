import MarkdownIt from 'markdown-it'

const ABSOLUTE_URL = /^(?:[a-z][a-z\d+.-]*:|\/\/)/i

/**
 * The single configured markdown-it instance for the app. `html: false`
 * means raw HTML in the source is escaped rather than passed through — this
 * is what makes storing Markdown (instead of HTML) safe for admin-authored
 * content rendered to anonymous visitors: there is no HTML path to
 * sanitise. `javascript:`/`data:` link schemes are dropped by markdown-it's
 * default URL validator; that validator is intentionally left at its
 * defaults rather than overridden.
 */
const md = new MarkdownIt({ html: false, linkify: true, typographer: false })

const renderLinkOpen =
  md.renderer.rules.link_open ??
  ((tokens, idx, options, _env, self) => self.renderToken(tokens, idx, options))

md.renderer.rules.link_open = (tokens, idx, options, env, self) => {
  const token = tokens[idx]!
  const hrefIndex = token.attrIndex('href')
  const href = hrefIndex >= 0 ? String(token.attrs![hrefIndex]![1] ?? '') : ''

  token.attrSet('rel', 'noopener noreferrer nofollow')
  if (ABSOLUTE_URL.test(href)) {
    token.attrSet('target', '_blank')
  }

  return renderLinkOpen(tokens, idx, options, env, self)
}

/** Renders Markdown to sanitised HTML. The only place a markdown-it instance is constructed. */
export function renderMarkdown(source: string): string {
  return md.render(source)
}
