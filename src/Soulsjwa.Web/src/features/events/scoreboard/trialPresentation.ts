import type { GameBreakdown } from '../../../types'

export const TRIAL_BADGE_TEXT = 'Trial'

/** Uppercase variant for the OBS overlay, which shouts its labels. */
export const TRIAL_BADGE_TEXT_OVERLAY = 'TRIAL'

/**
 * Amber, matching MUI's `warning.main`. Spelled out as a hex literal because
 * the overlay is inline-styled and cannot resolve theme tokens.
 */
export const TRIAL_ACCENT_HEX = '#f59e0b'

export const TRIAL_TOOLTIP =
  'Trial/training run — this progress never counts toward the official score or ranking.'

/**
 * Referenced by the copy below as well as by the tab itself, so renaming the
 * tab cannot leave instructions pointing at one that no longer exists.
 */
export const TRIAL_TAB_LABEL = 'Trial runs'

/**
 * Why an official control is read-only while trial mode is on. The two cases
 * are worded differently because the fix differs: a *recording* run takes the
 * tick (tick it on the Trial tab instead), while a *dormant* one takes nothing
 * at all — the server refuses the write outright, so the user must start the
 * run or turn trial mode off. Two lengths each (standing alert, one-line
 * caption), kept together so they can't drift into contradicting each other.
 */
export const TRIAL_RECORDING_BLOCKS_OFFICIAL =
  'A trial run is recording for this game. Objectives ticked here would be recorded against ' +
  `that run instead, so they are managed from the ${TRIAL_TAB_LABEL} tab on My Events.`

export const TRIAL_RECORDING_BLOCKS_OFFICIAL_SHORT = `A trial run is recording for this game — use the ${TRIAL_TAB_LABEL} tab.`

export const TRIAL_DORMANT_BLOCKS_OFFICIAL =
  'Trial mode is enabled for this game but the run is not recording, so nothing can be ' +
  `ticked here or against the official score. Start the run from the ${TRIAL_TAB_LABEL} tab ` +
  'to practise, or disable trial mode to play for real again.'

export const TRIAL_DORMANT_BLOCKS_OFFICIAL_SHORT =
  'Trial mode is enabled but not recording — start the run or disable trial mode.'

/**
 * One helper so every surface offering an official tick refuses on exactly
 * the condition the server does: the slot existing, not the run recording.
 */
export const trialBlockReason = (
  game: { hasTrialRun: boolean; isTrialActive: boolean },
  short = false,
): string | undefined => {
  if (!game.hasTrialRun) return undefined
  if (game.isTrialActive)
    return short ? TRIAL_RECORDING_BLOCKS_OFFICIAL_SHORT : TRIAL_RECORDING_BLOCKS_OFFICIAL
  return short ? TRIAL_DORMANT_BLOCKS_OFFICIAL_SHORT : TRIAL_DORMANT_BLOCKS_OFFICIAL
}

/**
 * Marks an interactive objective list as writing to a trial run. Needed on the
 * list itself, not just the surrounding panel: the panel's heading and alert
 * scroll away, leaving a long list of checkboxes that look exactly like the
 * official ones.
 */
export const TRIAL_RUN_LIST_LABEL = 'Trial run'

/** Suffix on each checkbox's accessible name so the distinction is not visual-only. */
export const TRIAL_RUN_CONTROL_SUFFIX = 'in trial run'

/** Screen-reader names for the trial figures shown beside official ones. */
export const TRIAL_FIGURE_LABELS = {
  score: 'Trial score',
  completed: 'Completed in trial',
  failed: 'Failed in trial',
  lastCompleted: 'Last completed in trial',
} as const

/**
 * Heads a list of objectives whose marks are the trial's rather than the
 * official record's. Needed inside the list itself: the ticks and crosses are
 * drawn identically to official ones, so without this an objective the
 * competitor *has* officially completed shows unticked beside one they have
 * only practised, with nothing on screen to say why.
 */
export const TRIAL_MARKS_NOTICE =
  'Showing this trial run’s marks — official completions for this game are not ticked below.'

/**
 * "Trial", or "Trial · <game>" when the trial is on a game other than the one
 * the surrounding figures report on. Without the game name a viewer has no way
 * to tell what an amber score on a row about a different game refers to, which
 * is possible because a trial is not restricted to the event's active game.
 */
export const trialBadgeLabel = (
  trialing: readonly GameBreakdown[],
  displayedGameId: string | undefined,
): string => {
  const [only] = trialing
  return trialing.length === 1 && only && only.eventGameId !== displayedGameId
    ? `${TRIAL_BADGE_TEXT} · ${only.gameName}`
    : TRIAL_BADGE_TEXT
}
