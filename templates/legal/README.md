# Legal document templates

Baseline **Impressum** and **Datenschutzerklärung / Privacy Policy** templates
for operators who host a public Soulsjwa instance.

| File | Language | Paste into |
| --- | --- | --- |
| [`impressum.de.md`](./impressum.de.md) | 🇩🇪 German | Admin → Legal → **Impressum** |
| [`impressum.en.md`](./impressum.en.md) | 🇬🇧 English | Admin → Legal → **Impressum** |
| [`datenschutzerklaerung.de.md`](./datenschutzerklaerung.de.md) | 🇩🇪 German | Admin → Legal → **Datenschutz** |
| [`privacy-policy.en.md`](./privacy-policy.en.md) | 🇬🇧 English | Admin → Legal → **Datenschutz** |

Both documents are stored as Markdown and rendered through the same pipeline as
event rules, so headings, tables, lists, and links all work. Each legal document
is capped at 64 KiB.

## How to use them

1. Open `/admin/legal` on your instance and pick the tab you want to fill.
2. Copy the matching template file into the editor.
3. Replace **every** `<placeholder>` with your real data. Angle-bracket
   placeholders are rendered literally by the Markdown pipeline, so anything you
   forget shows up on the public page as `<…>` — search the rendered page for
   `<` before you call it done.
4. Delete the ⚠️ operator-notice block at the top of each template and every
   `> **Operator note:**` / `> **Hinweis an den Betreiber:**` block, as well as
   any *optional* section that does not apply to your setup.
5. Save. The footer links (`/impressum`, `/datenschutz`) appear automatically as
   soon as a document has content; both public routes 404 while they are empty.

**Serving both languages:** the app stores exactly one document per kind. If you
want German and English on one page, paste the German version, add a horizontal
rule (`---`), and paste the English version underneath with a leading
`## English version` heading (and vice versa).

## What the templates already cover

They are written against what this codebase actually does, not against a generic
website:

- Twitch OAuth sign-in (`user:read:email`) and the admin-managed allowlist
- the account record: Twitch user ID, login, display name, email, avatar URL, role
- the two strictly necessary cookies (`refresh_token`, `oauth_state`) and the
  `soulsjwa.themeMode` entry in `localStorage`
- server access logs and IP-based rate limiting
- publicly visible competition data: scoreboards, completion times, competitor
  info links, calendar entries, OBS overlay tokens
- the Windows desktop connector and its API keys
- image uploads (content-addressed, metadata stripped)
- the append-only audit log
- outbound links to Twitch and YouTube (no embeds, nothing loaded from
  third-party CDNs)
- OpenTelemetry, described as an *optional* section — it is off by default in the
  stock `docker-compose.yml`

## What you must add yourself

The templates cannot know your infrastructure. **You** have to fill in or add:

- **Your hosting provider** — name and address, plus a data processing agreement
  (Art. 28 GDPR / AVV) with them.
- **Cloudflare or any other CDN, reverse proxy, DDoS protection, or WAF.** If
  your instance sits behind Cloudflare, Fastly, Bunny, a load balancer at your
  cloud provider, or similar, that provider terminates TLS and sees every
  visitor IP — it is a separate processing activity and a separate recipient
  (often with a third-country transfer) that **must** be declared. The privacy
  templates carry a section marked *— optional* for exactly this; fill it in or
  delete it.
- **Any other service you bolt on**: web analytics, error tracking (Sentry &
  co.), uptime monitors that log visitor data, an OTLP telemetry backend, a
  mail provider for contact forms, embedded Twitch players, a Discord widget.
- **Your retention periods.** The templates use `<placeholder>` values (e.g.
  `<7>` days for access logs) — put in what your server and your provider are
  actually configured to do.
- **Jurisdiction specifics.** The German texts are written for Germany
  (§ 5 DDG, § 18 Abs. 2 MStV, § 25 TDDDG). Austria (§ 5 ECG), Switzerland
  (revDSG/DSG), or a non-DACH jurisdiction need their own wording.

## ⚠️ No legal advice

These are starting points written by the maintainers of an open-source project,
not by lawyers, and they carry no warranty of completeness or correctness. The
operator of an instance is solely responsible for the accuracy of these pages
and for compliance with the law that applies to them. If you process personal
data of EU residents at any scale, have a lawyer review the result.
