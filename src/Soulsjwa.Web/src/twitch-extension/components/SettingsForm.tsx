import { useEffect, useState } from 'react'
import {
  TWITCH_EXTENSION_SCOPES,
  TWITCH_EXTENSION_SCOPE_LABELS,
  type TwitchExtensionConfiguration,
  type TwitchExtensionScope,
  type UpdateTwitchExtensionConfigurationRequest,
} from '../../types/twitchExtension'
import {
  SITE_BASE_URL,
  twitchExtensionClient,
  TwitchExtensionApiError,
} from '../api/twitchExtensionClient'

interface SettingsFormProps {
  token: string
  /** The dashboard's live view is the same form with a shorter heading. */
  live?: boolean
}

export const CONFIG_HEADING = 'Scoreboard settings for your channel'
export const LIVE_CONFIG_HEADING = 'Scoreboard'
export const FOLLOW_FEATURED_LABEL = 'Follow the featured event'
export const SAVE_LABEL = 'Save'
export const SAVED_LABEL = 'Saved · viewers update within a few seconds'
export const EVENT_CHOICE_LOCKED_NOTICE =
  'An admin has set every channel to follow the featured event.'
export const SIGN_IN_NOTICE =
  'Sign in to Soulsjwa with this Twitch account once, then reload this page, to change what your channel shows.'
export const HIGHLIGHT_LABEL = 'Highlight my own row when I compete'
export const TRIAL_LABEL = 'Show trial-run progress in amber'
export const LOADING_LABEL = 'Loading…'
const FEATURED_TAG = 'featured'
const LIVE_TAG = 'live'
const COMPETING_TAG = 'you compete'
const CURRENTLY_PREFIX = 'Currently:'
const SWITCHES_NOTE = 'switches automatically when admins feature a new one'
const NO_FEATURED_NOTE = 'nothing is featured right now'
const EVENT_LEGEND = 'Event to show'
const SCOPE_LEGEND = 'Default view for viewers'
const PIN_LEGEND = 'Pinned game'
const OPTIONS_LEGEND = 'Options'
const PICK_GAME_PLACEHOLDER = 'Pick a game'
const PINNED_SCOPE: TwitchExtensionScope = 'PinnedGame'

type FormState =
  | { status: 'loading' }
  | { status: 'failed'; message: string }
  | { status: 'ready'; configuration: TwitchExtensionConfiguration }

/**
 * What the broadcaster sees in the extension's config and live-config views.
 * Saving needs a Soulsjwa account signed in with the channel's Twitch account,
 * so an unlinked broadcaster gets the explanation instead of a disabled button.
 */
export function SettingsForm({ token, live = false }: SettingsFormProps) {
  const [state, setState] = useState<FormState>({ status: 'loading' })
  const [draft, setDraft] = useState<UpdateTwitchExtensionConfigurationRequest | null>(null)
  const [saving, setSaving] = useState(false)
  const [saved, setSaved] = useState(false)
  const [saveError, setSaveError] = useState<string | null>(null)

  useEffect(() => {
    let cancelled = false
    twitchExtensionClient
      .configuration(token)
      .then((result) => {
        if (cancelled || result.status !== 'ok') return
        setState({ status: 'ready', configuration: result.data })
        setDraft({ ...result.data.settings })
      })
      .catch((caught: unknown) => {
        if (cancelled) return
        setState({
          status: 'failed',
          message:
            caught instanceof TwitchExtensionApiError ? caught.message : 'Could not load settings.',
        })
      })
    return () => {
      cancelled = true
    }
  }, [token])

  if (state.status === 'loading') return <p className="sx-form__note">{LOADING_LABEL}</p>
  if (state.status === 'failed') return <p className="sx-form__error">{state.message}</p>
  if (!draft) return null

  const { configuration } = state
  const linked = configuration.linkedUser !== null
  const canPickEvent = configuration.policy.allowChannelEventChoice
  const featured = configuration.events.find((e) => e.isFeatured) ?? null
  const shownEvent = draft.eventId
    ? (configuration.events.find((e) => e.id === draft.eventId) ?? null)
    : featured
  const games = shownEvent?.games ?? []

  const update = (patch: Partial<UpdateTwitchExtensionConfigurationRequest>) => {
    setSaved(false)
    setSaveError(null)
    setDraft((current) => (current ? { ...current, ...patch } : current))
  }

  const chooseEvent = (eventId: string | null) => {
    // A pinned game belongs to one event; changing the event drops it.
    update({ eventId, pinnedEventGameId: null })
  }

  const submit = async (event: React.FormEvent) => {
    event.preventDefault()
    if (!draft) return
    setSaving(true)
    setSaveError(null)
    try {
      const body: UpdateTwitchExtensionConfigurationRequest = {
        ...draft,
        pinnedEventGameId: draft.defaultScope === PINNED_SCOPE ? draft.pinnedEventGameId : null,
      }
      const result = await twitchExtensionClient.saveConfiguration(token, body)
      if (result.status === 'ok') {
        setState({ status: 'ready', configuration: result.data })
        setDraft({ ...result.data.settings })
        setSaved(true)
      }
    } catch (caught) {
      setSaveError(caught instanceof TwitchExtensionApiError ? caught.message : 'Could not save.')
    } finally {
      setSaving(false)
    }
  }

  const needsPin = draft.defaultScope === PINNED_SCOPE
  const canSave = linked && !saving && (!needsPin || draft.pinnedEventGameId !== null)

  return (
    <form className="sx-form" onSubmit={(e) => void submit(e)}>
      <h1 className="sx-form__heading">{live ? LIVE_CONFIG_HEADING : CONFIG_HEADING}</h1>
      {!linked && (
        <p className="sx-form__notice">
          {SIGN_IN_NOTICE}{' '}
          <a href={SITE_BASE_URL || '/'} target="_blank" rel="noopener noreferrer">
            Open Soulsjwa ↗
          </a>
        </p>
      )}

      {!canPickEvent && (
        <p className="sx-form__note">
          {EVENT_CHOICE_LOCKED_NOTICE}
          {featured
            ? ` ${CURRENTLY_PREFIX} ${featured.name}.`
            : ` ${CURRENTLY_PREFIX} ${NO_FEATURED_NOTE}.`}
        </p>
      )}
      <fieldset className="sx-form__group" disabled={!linked} hidden={!canPickEvent}>
        <legend>{EVENT_LEGEND}</legend>
        <label className="sx-form__option" data-on={draft.eventId === null || undefined}>
          <input
            type="radio"
            name="event"
            id="event-featured"
            checked={draft.eventId === null}
            onChange={() => chooseEvent(null)}
          />
          <span>
            {FOLLOW_FEATURED_LABEL}
            <small>
              {featured
                ? `${CURRENTLY_PREFIX} ${featured.name} · ${SWITCHES_NOTE}`
                : `${CURRENTLY_PREFIX} ${NO_FEATURED_NOTE}`}
            </small>
          </span>
        </label>
        {configuration.events.map((option) => (
          <label
            key={option.id}
            className="sx-form__option"
            data-on={draft.eventId === option.id || undefined}
          >
            <input
              type="radio"
              name="event"
              id={`event-${option.id}`}
              checked={draft.eventId === option.id}
              onChange={() => chooseEvent(option.id)}
            />
            <span>
              {option.name}
              <small>
                {[
                  option.isFeatured ? FEATURED_TAG : null,
                  option.isStarted ? LIVE_TAG : null,
                  option.isCompetitor ? COMPETING_TAG : null,
                ]
                  .filter(Boolean)
                  .join(' · ')}
              </small>
            </span>
          </label>
        ))}
      </fieldset>

      <div className="sx-form__row">
        <fieldset className="sx-form__group" disabled={!linked}>
          <legend>{SCOPE_LEGEND}</legend>
          <select
            id="default-scope"
            value={draft.defaultScope}
            onChange={(e) => update({ defaultScope: e.target.value as TwitchExtensionScope })}
          >
            {TWITCH_EXTENSION_SCOPES.map((scope) => (
              <option key={scope} value={scope}>
                {TWITCH_EXTENSION_SCOPE_LABELS[scope]}
              </option>
            ))}
          </select>
        </fieldset>
        <fieldset className="sx-form__group" disabled={!linked || !needsPin}>
          <legend>{PIN_LEGEND}</legend>
          <select
            id="pinned-game"
            value={draft.pinnedEventGameId ?? ''}
            onChange={(e) => update({ pinnedEventGameId: e.target.value || null })}
          >
            <option value="">{PICK_GAME_PLACEHOLDER}</option>
            {games.map((game) => (
              <option key={game.eventGameId} value={game.eventGameId}>
                {game.name}
                {game.isEnabled ? ' (active)' : ''}
              </option>
            ))}
          </select>
        </fieldset>
      </div>

      <fieldset className="sx-form__group" disabled={!linked}>
        <legend>{OPTIONS_LEGEND}</legend>
        <label className="sx-form__option" data-on={draft.highlightChannelCompetitor || undefined}>
          <input
            type="checkbox"
            id="highlight-me"
            checked={draft.highlightChannelCompetitor}
            onChange={(e) => update({ highlightChannelCompetitor: e.target.checked })}
          />
          <span>{HIGHLIGHT_LABEL}</span>
        </label>
        <label className="sx-form__option" data-on={draft.showTrialProgress || undefined}>
          <input
            type="checkbox"
            id="show-trial"
            checked={draft.showTrialProgress}
            onChange={(e) => update({ showTrialProgress: e.target.checked })}
          />
          <span>{TRIAL_LABEL}</span>
        </label>
      </fieldset>

      <div className="sx-form__actions">
        <button type="submit" className="sx-form__save" disabled={!canSave}>
          {SAVE_LABEL}
        </button>
        {saved && <span className="sx-form__saved">{SAVED_LABEL}</span>}
        {saveError && <span className="sx-form__error">{saveError}</span>}
      </div>
    </form>
  )
}
