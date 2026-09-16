# Streamer overlay

> For what *viewers* see on the Twitch channel page itself, see the
> [Twitch extension](twitch-extension.md); this page is about the
> streamer's own OBS scene.

Soulsjwa ships an OBS-friendly overlay route that streamers can drop into a
[browser source](https://obsproject.com/kb/browser-source) to surface live
event progress alongside their gameplay. The overlay is rendered without
the regular app chrome (no nav, no footer) and uses a transparent
background by default so it composites cleanly over a game capture.

By default the overlay renders a competitor's objectives as a live checklist
grouped **Game → Category → Objective** (for Elden Ring the category is the
in-game area). Completed objectives are crossed out and show the score they
award. Two other layouts are available via the `view` parameter: a paginated
`scores` scoreboard and a `games` game-by-game rotation.

Every score is shown against the points on offer — `100 / 208` — summed over
the same games the score itself was: the active game by default, the pinned
games with `games=`, and the one game of the page in the `games` view. The
maximum is drawn small and muted beside the score so it reads as context, not
a second number.

## Trial runs

A competitor practising in a trial run shows a `TRIAL` badge and
their trial score in amber next to their official score, so viewers can see what
the practice run has earned without mistaking it for the real thing. While a trial
is on screen the row's counts and progress bar track that run against its own
game's objectives, and the objectives view draws its completions with the ordinary
marks — the badge is what says which run they belong to, and the rows are discarded
when the run ends.

A trial is deliberately not restricted to the event's active game, so a trialing
game stays visible even when the overlay would otherwise show only the enabled one.
It never joins the official totals the overlay re-ranks by: the trial figures are
reported per game and summed only across the games actually on screen, so pinning
`games=` cannot make a competitor's official score or rank move.

## URL

```
/events/{eventId}/overlay?token={overlayToken}
```

The overlay route is `/events/:id/overlay` and it is backed by a token-gated
endpoint (`GET /api/v1/events/{eventId}/overlay-scoreboard`). The token can be
sent either as an `X-Overlay-Token` request header (preferred — it never ends
up in a URL, so it can't leak via logs, browser history, or a `Referer`
header) or as the `?token=…` query parameter shown above, which exists
because an OBS browser source is a bare URL and can't set headers. Whichever
form is used, the token is required; without it the overlay renders a clear
configuration error instead of data. The token is an opaque, per-event value
minted from the event detail page by the event owner or by any competitor in
the event — anyone with the URL can render the overlay, and nobody else can.
Tokens expire 90 days after being minted and can also be revoked at any
time; revocation propagates within a few seconds.

## Minting a token

1. Open the event's **Broadcast** tab. You must be the event owner **or** a
   competitor in the event.
2. In the **OBS overlay tokens** section click **Create token**.
3. Give the token a name (e.g. _"OBS – main scene"_) and design the overlay
   against the **live preview** beside the form: pick the view, restrict to
   a game, pin a player (defaults to yourself when you're a competitor
   here), and tune page size, cycle/refresh timings, highlight duration,
   title override, transitions, theme, panel opacity, and the visibility
   toggles described below. Every change shows in the preview as you make it.
4. Click **Create token**. The look you designed is saved with the token and
   the URL is shown once — it carries only the token, because the look lives
   on the token. Click **Copy URL** and paste it into an OBS browser source.
5. The token is now listed; from there you can see its saved look, when it
   was last used, when it expires, **edit its look** (the tune icon), and
   revoke it at any time. Competitors only see, edit and revoke their own
   tokens; the event owner/admin sees every token on the event and can edit
   or revoke any of them.

If you lose a URL, just mint a new token and revoke the old one — the raw
value is never stored in cleartext on the server and cannot be recovered.

## Changing the look of a source already in OBS

Editing a token's look saves it on the token, and an OBS browser source
using that token picks the change up on its next poll — no need to touch the
URL in OBS. The delay is the token's **refresh** interval plus up to the
server's 5-second cache window, so a change is normally on screen within a
few seconds; only the panel's `bg` debug colour stays URL-only.

**Precedence:** a saved look wins over the URL for every knob it carries.
Tokens minted before saved looks existed have none, so the parameters in
their URL keep driving them until you save a look; the edit dialog says so.
Once saved, the URL's parameters (other than `token` and `bg`) are ignored.

## Live preview

The preview beside the form is the real overlay route embedded at a chosen
**source size** (presets for a corner box, a sidebar, a wide bar and a full
1080p canvas, or a custom width and height), scaled down to fit, over a
switchable backdrop (checkerboard, dark or light scene) so transparency,
theme and panel opacity can be judged. Text sizes follow the source size, as
they do in OBS. The frame runs the exact code the OBS source runs, including
page cycling and change highlights, and is not reloaded as you edit.

It shows the event's real scoreboard, polled like the app's live tabs.
Before an event has anything to show, switch on **Use sample data** to see
fictional competitors over the event's own games (and its own objectives,
when it has any); every few seconds one of them completes an objective, so
progress bars move, rows flash and page jumps happen one at a time. A pinned
real player has no sample row, so the top-ranked sample competitor stands in.

**Simulate a completion** pretends a competitor just completed their next
open objective — exactly the change a real completion makes — so the
overlay reacts as it would live: in the `objectives` view the competitor on
screen gets a new tick animated in, in the other views a row flashes, and
the page jumps if needed. In the `scores`/`games` views repeated presses walk
through every competitor. Only the preview sees these pretend completions.

Under the hood the frame is the route in *preview mode*
(`/events/{eventId}/overlay?preview=1`): it polls nothing and renders only
what the embedding page posts to it, same-origin and from its parent window
only. Opened on its own, a preview URL shows a "waiting for the preview"
message and never any data — there is no token-less way to put the live
overlay on screen.

## Query string configuration

Every knob is also a URL query parameter, so a configuration can round-trip
through OBS without any server-side state; a look saved on the token takes
precedence over these (see above).

| Parameter | Type | Default | Description |
| --- | --- | --- | --- |
| `token` | opaque overlay token | _(required)_ | Authenticates the OBS overlay request. Missing tokens show a configuration error; invalid/revoked tokens show an invalid-token error. |
| `games` | csv of event-game ids, `all`, `*`, or empty | `all` | Restrict the overlay to specific games in the event. |
| `players` | csv of user ids, `all`, `*`, or empty | `all` | Restrict the overlay to specific competitor user ids. In the `objectives` view, setting exactly one user id is the designer's "pin to player" behavior; otherwise the top-ranked visible competitor is used. |
| `view` | `objectives` \| `scores` \| `games` | `objectives` | `objectives` shows one competitor's objectives grouped **Game → Category → Objective** (completed ones are crossed out and show their score); `scores` paginates competitors; `games` cycles one page per visible game, with every figure on a row narrowed to that game. |
| `pageSize` | integer 1–50 | `10` | Rows per page — competitors in `scores`, objectives in `objectives`. The `games` view ignores this because it uses one page per game. |
| `cycle` | integer 0–600 (seconds) | `30` | Auto-rotate pages every N seconds. `0` disables auto-cycle. |
| `refresh` | integer 2–120 (seconds) | `5` | How often the token-gated scoreboard is re-fetched. |
| `showTitle` | boolean | `true` | Show the title row. In `objectives`, the row also includes the selected competitor and score. |
| `showProgress` | boolean | `true` | Show objective completion progress bars: one per visible competitor in the `scores` and `games` views, and one under the title for the shown competitor in the `objectives` view. Reflects the trial run while one is on screen. |
| `showPagination` | boolean | `true` | Show the "Page X / N" indicator when more than one page exists. |
| `highlight` | boolean | `true` | Briefly flash a competitor row when their last completion timestamp changes — the trial's own timestamp while a trial is on screen, so practice progress still flashes. A flash on a page that is not on screen pulls the overlay to that page (with the page fade, when `animate` is on). On screen or not, a flash restarts the cycle timer and holds the page for at least `highlightSeconds`, so a short `cycle` cannot paginate away mid-flash; in the `games` view only the page of the game the completion belongs to flashes. In the `objectives` view a completion that just landed is drawn in rather than switched on: the overlay jumps to its page, the row's background fades in, the tick pops, and the strikethrough sweeps across the name over about 3.5 s (capped by `highlightSeconds`). |
| `highlightSeconds` | integer 1–60 | `6` | How long the highlight stays on. |
| `animate` | boolean | `true` | When pagination cycles to the next page, cross-fade: the old page fades out, then the new one fades in. Set to `false` to swap instantly. |
| `theme` | `dark` \| `light` | `dark` | Color scheme for the panel. |
| `bg` | validated CSS color | _(transparent)_ | Override the *page* background — useful during testing in a normal browser (an OBS browser source is already transparent without it). Accepts hex (`#rgb`/`#rgba`/`#rrggbb`/`#rrggbbaa`), `rgb()`/`rgba()`/`hsl()`/`hsla()`, `transparent`, and a conservative set of named colors; anything else (including a value containing `;`, `{`, `}`, `/*`, `url(`, or `\`) is rejected and falls back to transparent. The designer does not expose this debug-only knob. |
| `panelOpacity` | integer 0–100 | `80` | Opacity of the *panel* — the box behind the title/objectives/rows — independent of `bg`. Keeps the theme's own panel color, just fades it; `0` makes the panel invisible while its contents keep rendering on the bare page. |
| `title` | string | event name, then `Soulsjwa` fallback | Override the title row text. |
| `preview` | boolean | `false` | Preview mode for the app's designer: the route polls nothing and renders what its embedding window posts (see [Live preview](#live-preview)). Not for OBS. |

Booleans accept `1`/`0`, `true`/`false`, `yes`/`no`, `on`/`off`.

### Resizing

The overlay is fully responsive — drop it in OBS and drag the browser source
to any size you like; row text, ranks, and competitor progress bars scale via
`clamp()`-based font sizing.

## Examples

All examples assume `&token=ot_…` is appended to the URL — it is required
for the overlay to render data. Mint one from the event detail page (see
above).

A minimal overlay for an event:

```
/events/3f2b…/overlay?token=ot_…
```

A 7-row scoreboard that auto-cycles every 12 s and refreshes every 4 s:

```
/events/3f2b…/overlay?token=ot_…&view=scores&pageSize=7&cycle=12&refresh=4
```

An objective checklist pinned to one player, 12 objectives per page:

```
/events/3f2b…/overlay?token=ot_…&view=objectives&players=aaaa…&pageSize=12
```

Hide chrome, just the bar:

```
/events/3f2b…/overlay?token=ot_…&showTitle=0&showPagination=0
```

Filter to two specific games and a single player:

```
/events/3f2b…/overlay?token=ot_…&games=11111111-1111-...,22222222-2222-...&players=aaaa…
```

Game-by-game view with no highlight flashing:

```
/events/3f2b…/overlay?token=ot_…&view=games&highlight=0
```

Solid background for debugging (and a light theme):

```
/events/3f2b…/overlay?token=ot_…&bg=%23111&theme=light
```
