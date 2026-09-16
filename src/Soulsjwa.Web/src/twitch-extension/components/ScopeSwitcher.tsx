import type { TwitchExtensionGame } from '../../types/twitchExtension'
import { ACTIVE_SCOPE, ALL_SCOPE, sameScope, type BoardScope } from '../scoreboard/scope'

interface ScopeSwitcherProps {
  scope: BoardScope
  games: TwitchExtensionGame[]
  activeGame: TwitchExtensionGame | null
  onChange: (scope: BoardScope) => void
  compact?: boolean
}

export const ALL_GAMES_LABEL = 'All games'
export const ACTIVE_GAME_PREFIX = 'Now:'
export const PICK_GAME_LABEL = 'Pick game'
export const NO_ACTIVE_GAME_LABEL = 'No active game'

/**
 * All games, the active game, or one game from a chip row. The chip row is
 * only shown once the viewer asks for it, since the panel is 318px wide and
 * most viewers only ever toggle between the first two.
 */
export function ScopeSwitcher({
  scope,
  games,
  activeGame,
  onChange,
  compact = false,
}: ScopeSwitcherProps) {
  const picking = scope.kind === 'game'
  const activeLabel = activeGame ? `${ACTIVE_GAME_PREFIX} ${activeGame.name}` : NO_ACTIVE_GAME_LABEL

  const pick = () => {
    if (picking) return
    const first = games.find((g) => g.eventGameId !== activeGame?.eventGameId) ?? games[0]
    if (first) onChange({ kind: 'game', eventGameId: first.eventGameId })
  }

  return (
    <div className={`sx-scope${compact ? ' sx-scope--compact' : ''}`}>
      <div className="sx-scope__tabs" role="tablist" aria-label="Scope">
        <button
          type="button"
          role="tab"
          className="sx-scope__tab"
          aria-selected={scope.kind === 'all'}
          onClick={() => onChange(ALL_SCOPE)}
        >
          {ALL_GAMES_LABEL}
        </button>
        <button
          type="button"
          role="tab"
          className="sx-scope__tab"
          aria-selected={scope.kind === 'active'}
          disabled={!activeGame}
          title={activeGame?.name}
          onClick={() => onChange(ACTIVE_SCOPE)}
        >
          {activeLabel}
        </button>
        {games.length > 0 && (
          <button
            type="button"
            role="tab"
            className="sx-scope__tab"
            aria-selected={picking}
            onClick={pick}
          >
            {PICK_GAME_LABEL}
          </button>
        )}
      </div>
      {picking && (
        <div className="sx-scope__games" role="group" aria-label="Game">
          {games.map((game) => {
            const selected = sameScope(scope, { kind: 'game', eventGameId: game.eventGameId })
            return (
              <button
                key={game.eventGameId}
                type="button"
                className="sx-scope__chip"
                aria-pressed={selected}
                onClick={() => onChange({ kind: 'game', eventGameId: game.eventGameId })}
              >
                {game.isEnabled && <span className="sx-scope__live-dot" aria-label="active" />}
                {game.name}
              </button>
            )
          })}
        </div>
      )}
    </div>
  )
}
