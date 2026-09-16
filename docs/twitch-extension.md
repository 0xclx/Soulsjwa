# Twitch extension

Soulsjwa can back a [Twitch Extension](https://dev.twitch.tv/docs/extensions/)
that shows an event's live scoreboard on a streamer's channel: a **panel**
under the video (the same panel on mobile) and a compact **video component**
over the player, plus the broadcaster's **config** and **live config** views.
The OBS overlay ([streamer-overlay.md](streamer-overlay.md)) stays the tool
for the streamer's own stream picture; the extension is what *viewers*
interact with on the channel page.

Twitch hosts the extension's front end on its own CDN, out of a zip this
repository builds, and runs it in a sandboxed iframe that may only talk to
hosts allow-listed in the Twitch developer console. The Soulsjwa API is that
host, acting as the extension's backend service (EBS). Nothing else in the
app changes shape: the whole feature is off until a client id and secret are
configured.

## What viewers see

- **Which event.** Every channel resolves to one event: the broadcaster's
  explicit pick while it exists and is not archived, otherwise the site's
  featured event, otherwise a quiet "no event right now" state. A channel
  that never saved settings follows the featured event with the site's
  default rules (see [Extension-wide rules](#extension-wide-rules)), so a
  freshly installed extension works with no setup.
- **Scope switcher.** *All games* is the event's official ranking exactly as
  the app computes it. *Now: `<active game>`* narrows every figure to the
  event's single enabled game and re-ranks within it, following the event's
  tie-break mode. *Pick game* reveals a chip per game. The broadcaster sets
  the default; viewers can switch unless an admin has turned the switcher
  off for every channel.
- **Rows.** Rank (gold for first), avatar with a live dot, name, completion
  bar, score. A finished competitor shows FINISHED. A trial run shows its
  score in amber under the official one and never affects rank, exactly as
  in the app. A row flashes briefly when its newest completion advances.
- **The streamer's own row** is marked YOU when the channel's Twitch account
  competes in the event (the broadcaster can turn this off).
- **Detail on tap.** A row expands to per-game figures and in-game time; a
  second tap loads that competitor's objective list, grouped by game and
  category with check and cross marks. Objectives are fetched only then, so
  the payload every viewer polls stays small.
- **Freshness.** The panel polls every 5 s while the event runs and every
  30 s otherwise, pauses while Twitch reports it hidden, revalidates by
  `ETag` (an unchanged board is a bodiless 304), and refetches at once when
  the server pushes a change (see [Push updates](#push-updates)).
- **Look.** A bordered, rounded card in Twitch's neutrals with the app's
  purple as the accent, drawn on Twitch's own page colour so it sits
  naturally among the channel's other panels. It follows the viewer's
  Twitch theme (dark or light) as the helper reports it.

## What the broadcaster controls

The extension's config view (shown on install and in Twitch's extension
manager) and live-config view (in the stream dashboard) edit the same
settings, as does the **Twitch extension** card on the event's Broadcast tab
in the web app:

| Setting | Meaning |
| --- | --- |
| Event | *Follow the featured event* (default) or one specific non-archived event; hidden when an admin has locked every channel to the featured event |
| Default view | `AllGames`, `ActiveGame`, or `PinnedGame` with a game of the shown event; the admin-set default applies until the broadcaster saves |
| Highlight my own row | Mark the broadcaster's row when they compete |
| Show trial-run progress | Amber trial figures beside the official ones |

**Saving requires a Soulsjwa account signed in with the channel's Twitch
account.** Every settings write is attributed to that user and audit-logged
(`twitch_extension.configuration_updated`). A broadcaster who has never
signed in sees the settings read-only with an explanation; their channel
still shows the featured event.

## Extension-wide rules

Admins set a few rules that apply to every channel from the **Twitch
extension** tab of the admin area (`/admin/twitch-extension`), stored in the
`TwitchExtensionSettings` singleton and audit-logged as
`twitch_extension.settings_updated`:

| Rule | Default | Effect |
| --- | --- | --- |
| Streamers may show an event of their choice | on | Off: every channel follows the featured event; existing picks are ignored (not deleted) and new ones are refused with a validation error |
| Viewers may switch the scope | on | Off: the panel shows the channel's default view only, without the *All games / Now / Pick game* switcher |
| Default view | `AllGames` | `AllGames` or `ActiveGame` for channels that never saved settings (a pinned game needs an event, so it cannot be a global default) |
| Highlight the streamer's row | on | Default for channels that never saved settings |
| Show trial-run progress | on | Default for channels that never saved settings |

A rule change evicts every cached extension response (the `twitch-extension`
cache tag), so panels pick it up on their next poll. Both viewer and
broadcaster payloads carry the rules as `policy`, so the extension's own
views hide what is locked instead of failing on save.

The same tab shows whether the server is configured and can push, the
extension origin that CORS allows, the Client ID and API host to enter in the
Twitch console, the setup steps below, and the **Download extension zip**
button (see [Getting the extension bundle](#getting-the-extension-bundle)).

## How it works

```mermaid
sequenceDiagram
    participant V as Viewer's browser (Twitch page)
    participant CDN as Twitch CDN
    participant API as Soulsjwa API (EBS)
    participant TW as Twitch Helix

    V->>CDN: load panel.html + bundle
    V->>V: Twitch.ext.onAuthorized → viewer JWT (channel_id, role, opaque_user_id)
    V->>API: GET /api/v1/twitch-extension/scoreboard<br/>Authorization: Bearer <JWT>, If-None-Match
    API->>API: verify HS256 with the extension secret; channel from the token
    API-->>V: 200 + ETag (cached 5 s per channel) or 304
    Note over API,TW: a completion evicts the event's scoreboard cache tag
    API->>TW: POST helix/extensions/pubsub (global, {type:"scoreboard", eventId})
    TW-->>V: Twitch.ext.listen("global") → refetch if it is the shown event
```

- **Authentication.** Twitch signs a JWT per viewer with the extension
  secret (HS256 over the base64-decoded secret). The API verifies it under a
  dedicated bearer scheme (`TwitchExtension`) that only the extension route
  group accepts; the channel id is read from the token, never from a
  parameter, so a viewer cannot read another channel's settings. Broadcaster
  routes additionally require `role = broadcaster`. Several secrets can be
  listed during a rotation (Twitch keeps the previous one valid for about an
  hour).
- **Caching.** Viewer reads are output-cached for 5 s per channel, keyed on
  the verified `channel_id` claim, and tagged with both the channel and the
  shown event's scoreboard tag, so every completion, live toggle or game
  switch evicts them like the in-app scoreboard and the overlay. The
  response also carries a strong `ETag`.
- **Rate limiting.** The `twitch-extension` policy is partitioned per
  viewer (the token's opaque id), 40 requests/min by default
  (`RateLimits:TwitchExtensionPerViewerPerMinute`), so a shared NAT full of
  viewers is not one bucket.
- **CORS.** A dedicated policy allows exactly the extension's CDN origin
  (`https://<clientId>.ext-twitch.tv`), bearer auth without credentials, and
  exposes `ETag`.
- **Payload.** Deliberately not the app's `ScoreboardResponse`, which embeds
  every objective for every competitor: the extension gets totals, per-game
  figures and trial summaries, roughly 10 KB for a 20-player event, and
  fetches one competitor's objectives on demand.

### Push updates

When `TwitchExtension:OwnerUserId` is set, the server also pushes tiny
"changed" pings through Twitch's Extension PubSub, so panels refetch about a
second after a completion lands instead of on their next poll:

- A scoreboard change goes to the **global** target (every channel the
  extension is active on) carrying the event id; each panel refetches only if
  that is the event it shows. Global rather than per channel because most
  channels have no settings row and cannot be enumerated.
- A settings change goes to that channel's **broadcast** target.

The hook is the output-cache store itself: evicting an event's scoreboard
tag, which every write path already does, is the signal. Pings are coalesced
over two seconds and signed with a short-lived `role: external` token as the
extension owner. Every failure is logged and dropped; viewers still poll, so
a lost ping costs seconds, never data. Twitch's 2025 shutdown of its legacy
PubSub did not affect Extension PubSub.

## Server configuration

| Setting | Required | Description |
| --- | --- | --- |
| `TwitchExtension__ClientId` | to enable | The extension's Client ID from the developer console |
| `TwitchExtension__Secret` | to enable | The extension secret exactly as shown in the console (base64) |
| `TwitchExtension__Secrets__0`, `__1`, … | alternative | Several secrets at once, for a rotation; newest first |
| `TwitchExtension__OwnerUserId` | for push | Numeric Twitch user id of the account that owns the extension |
| `TwitchExtension__LocalTestOrigin` | dev only | Extra CORS origin for Twitch's Local Test mode, e.g. `https://localhost:8080` |
| `TwitchExtension__BundlePath` | no | Directory holding the built extension pages; default `twitch-extension` next to the API (where the Docker image puts them) |
| `TwitchExtension__ApiUrl` | no | Origin written into the downloaded zip; default `Frontend__Url` |
| `RateLimits__TwitchExtensionPerViewerPerMinute` | no | Default 40 |

With neither client id nor secret the feature is off: the extension routes
are not mapped (they 404 like any unknown `/api/v1` path), `GET
/api/v1/twitch-extension/status` reports `configured: false`, and the web app
hides its card. Half a configuration fails startup. The stock
`docker-compose.yml` maps `TWITCH_EXTENSION_CLIENT_ID`,
`TWITCH_EXTENSION_SECRET` and `TWITCH_EXTENSION_OWNER_USER_ID` from `.env`.

Twitch requires the EBS to be reachable over **HTTPS with a CA-issued
certificate** for hosted test, review and release, so production needs the
TLS-terminating reverse proxy in front of port 8080 that
[deployment.md](deployment.md) already assumes.

## Getting the extension bundle

The Docker image builds the extension pages alongside the SPA and ships them
under `/app/twitch-extension`. An admin downloads the zip Twitch wants from
the **Twitch extension** tab (`GET /api/v1/admin/twitch-extension/bundle`);
the API assembles it on the fly and writes this deployment's origin into
`extension-config.js` at that moment, so one image serves any host and no
`npm` is needed on the server. The origin defaults to `Frontend__Url` and can
be overridden with `TwitchExtension__ApiUrl`.

`vite.twitch.config.ts` is a second build of the web package (see
[ADR 0008](adr/0008-twitch-extension-bundle.md)): five entry pages under
`twitch-extension/`, sources under `src/twitch-extension/`, relative asset
paths, no MUI, no router. The pages read the API origin at runtime from
`extension-config.js` (`window.SOULSJWA_TWITCH_EXTENSION.apiUrl`), which is
an empty placeholder in the build output and in development. CI runs
`npm run build:twitch` on every push as a compile check.

Without Docker, `cd src/Soulsjwa.Web && npm run build:twitch` writes the
pages to `dist-twitch/` (the development `appsettings` points
`TwitchExtension:BundlePath` there, so the admin download works against a
local backend too) and also packs them into
`soulsjwa-twitch-extension.zip` with the empty placeholder, for manual
editing.

`npm run dev:twitch` serves the pages on `https://localhost:8080/` with a
self-signed certificate (what Twitch's Local Test mode loads) and proxies
`/api` to the local backend on port 5000, so no API origin is needed in
development.

## Twitch developer console setup

1. Enable two-factor authentication on the Twitch account (required to
   create extensions), open the [Developer Console](https://dev.twitch.tv/console/extensions)
   and create an Extension.
2. **Extension views:** enable Panel, Video Component, Mobile, Config and
   Live Config, with viewer paths `panel.html`, `video_component.html`,
   `mobile.html`, `config.html` and `live_config.html`. Panel height 496 px.
3. **Capabilities:** add the API host (e.g. `events.example.com`) to
   *Allowlist for URL Fetching Domains* and `static-cdn.jtvnw.net` to
   *Allowlist for Image Domains* (competitor avatars).
4. Copy the **Client ID** and **Extension Secret** into
   `TwitchExtension__ClientId` and `TwitchExtension__Secret` on the server;
   for push updates also set `TwitchExtension__OwnerUserId` to the owning
   account's numeric Twitch id. The admin tab shows the Client ID and API
   host it is running with, so the values can be checked against the
   console.
5. **Local test:** set the Testing Base URI to `https://localhost:8080/`,
   run `npm run dev:twitch`, accept the self-signed certificate once in the
   browser, install the extension on your own channel and add tester
   accounts to the testing allowlist. Step by step under
   [Testing on localhost](#testing-on-localhost).
6. **Hosted test:** download the zip from the admin tab, upload it under
   *Files → Upload Version in Assets* and move the version to Hosted Test.
   It is usable on allow-listed channels without a review.
7. **Release** (optional): submit for review with a walkthrough. Not
   required for use on allow-listed channels.

Twitch's limits the design respects: panel 318 × 496 px, first load under
1 MB (the bundle is about 66 KB gzipped), PubSub messages under 5 KB, links
open in a new tab, viewers need no login, and every state (no event, backend
unreachable, unlinked broadcaster) renders something sensible.

## Testing on localhost

Nothing has to be deployed to try the extension: Twitch's **Local Test**
state loads an extension's front end from your own machine while everything
else (the channel page, the viewer tokens, the console) is the real thing.
What you do need is an extension registered in the developer console (free;
the account needs two-factor authentication), because Twitch signs the
viewer tokens with that extension's secret and the API verifies them with
it. There is no offline mode without Twitch.

### Local Test mode (everything on your machine)

1. Register the extension and configure its views and capabilities as in
   steps 1–3 above. Under **Extension Views**, set the *Testing Base URI* to
   `https://localhost:8080/` (Twitch insists on HTTPS, `localhost` is fine).
   Under **Status**, keep the version in *Local Test*.
2. Put the extension's Client ID and secret into the local backend:
   `TwitchExtension:ClientId` and `TwitchExtension:Secret` in
   `appsettings.Development.json` or `dotnet user-secrets`, then
   `dotnet run --project src/Soulsjwa.Api` (port 5000). Set
   `TwitchExtension:OwnerUserId` too if you want to see push updates; it is
   optional, polling works without it.
3. Start the extension pages: `cd src/Soulsjwa.Web && npm run dev:twitch`.
   The dev server listens on `https://localhost:8080/` with a self-signed
   certificate and proxies `/api` to the backend on port 5000, so the pages
   talk to the API on their own origin and neither CORS nor
   `TwitchExtension:LocalTestOrigin` is involved. Open
   `https://localhost:8080/panel.html` once directly and accept the
   certificate warning; until then Twitch's iframe loads a blank page. On its
   own the page only says it is waiting for Twitch, which is expected.
4. On the console's **Status** tab click *View on Twitch and Install*, then
   activate the panel (and the video component, if you want it) for your
   channel in the Creator Dashboard under *Extensions → My Extensions*. The
   config view opens on activation and is the same page as
   `config.html`.
5. Feature an event in Soulsjwa (or pick one in the config view after
   signing in to the web app with the same Twitch account) and open your
   channel page: the panel shows the board, and completions made in the app
   show up within a poll interval, or within about a second with push
   configured. Only the extension owner and the accounts under *Testing
   Accounts* in the console see a Local Test extension; the channel page
   looks unchanged to everyone else.

The board's *Full scoreboard* and sign-in links point at the dev server's
origin in this mode, because that is the only origin the pages know; use
the web app at `http://localhost:5173` for those.

**With the backend from Docker Compose instead:** it already holds port
8080, so run the pages on another port and proxy to it, and set the Testing
Base URI to match:

```bash
TWITCH_DEV_PORT=8443 TWITCH_DEV_API_URL=http://localhost:8080 npm run dev:twitch
# Testing Base URI: https://localhost:8443/
```

The extension credentials then go into `.env` as for production.

### Hosted Test through a tunnel (the real bundle, still no server)

To exercise the Docker image, the admin tab's zip download and Twitch's
CDN hosting before you own a domain, put the local Compose stack behind a
tunnel that gives it a public HTTPS hostname with a CA-issued certificate,
such as `cloudflared tunnel --url http://localhost:8080` or
`ngrok http 8080`:

1. Set `Frontend__Url` (or just `TwitchExtension__ApiUrl`) to the tunnel's
   `https://` origin and `ALLOWED_HOSTS` to its hostname, restart the API.
2. Add the tunnel hostname to *Allowlist for URL Fetching Domains* in the
   console.
3. Sign in as admin, download the zip from *Admin → Twitch extension*,
   upload it under *Files → Upload Version in Assets* and move the version
   to *Hosted Test*.

The pages now come from Twitch's CDN and call the tunnel, exactly as in
production. Every tunnel restart changes the hostname on the free tiers, and
with it the allowlist entry and the zip, so keep this for a final check
rather than day-to-day work; Local Test mode needs none of it.
