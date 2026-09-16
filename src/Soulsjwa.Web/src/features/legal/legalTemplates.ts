import type { LegalDocumentKind } from '../../types'

/**
 * Baseline Impressum / Datenschutzerklärung templates that ship in the repo
 * under `templates/legal/`. They are linked (not inlined) from the admin
 * editor: an operator copies one in, replaces the `<placeholder>` values and
 * deletes what does not apply to their setup.
 *
 * Served by the app itself: the Dockerfile copies `templates/legal/` into
 * `wwwroot/legal-templates/`, and the Vite dev server maps the same path to
 * the repo folder. They used to link to the GitHub repository, which is
 * private — every operator following the link got a 404. Same-origin also
 * means the wording always matches the deployed build.
 */
const TEMPLATE_BASE_URL = '/legal-templates'

export const LEGAL_TEMPLATES_README_URL = `${TEMPLATE_BASE_URL}/README.md`

export interface LegalTemplateLink {
  /** Language as an operator picks it, not a locale code — this is a label. */
  language: string
  href: string
}

export const LEGAL_TEMPLATE_LINKS: Record<LegalDocumentKind, readonly LegalTemplateLink[]> = {
  Impressum: [
    { language: 'Deutsch', href: `${TEMPLATE_BASE_URL}/impressum.de.md` },
    { language: 'English', href: `${TEMPLATE_BASE_URL}/impressum.en.md` },
  ],
  Datenschutz: [
    { language: 'Deutsch', href: `${TEMPLATE_BASE_URL}/datenschutzerklaerung.de.md` },
    { language: 'English', href: `${TEMPLATE_BASE_URL}/privacy-policy.en.md` },
  ],
}

/**
 * Shown under the template links. The templates cover what the application
 * itself does; anything an operator puts in front of it (Cloudflare, another
 * CDN or reverse proxy, a WAF, an analytics or error-tracking service) sees
 * visitor IPs and is a separate recipient they have to declare themselves.
 */
export const LEGAL_TEMPLATE_HOSTING_NOTE =
  'The templates describe what this application does. Your infrastructure is yours to declare: ' +
  'if the instance runs behind Cloudflare or another CDN, reverse proxy, load balancer or WAF, ' +
  'that provider terminates TLS and sees every visitor IP — add it to the Datenschutz document ' +
  'yourself, together with your hosting provider, any analytics or error tracking, and an ' +
  'OpenTelemetry backend if you enabled one.'
