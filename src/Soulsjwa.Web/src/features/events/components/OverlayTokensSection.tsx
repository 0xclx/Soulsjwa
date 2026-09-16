import { useState } from 'react'
import Box from '@mui/material/Box'
import Button from '@mui/material/Button'
import Chip from '@mui/material/Chip'
import IconButton from '@mui/material/IconButton'
import Paper from '@mui/material/Paper'
import Stack from '@mui/material/Stack'
import Tooltip from '@mui/material/Tooltip'
import Typography from '@mui/material/Typography'
import AddIcon from '@mui/icons-material/Add'
import DeleteIcon from '@mui/icons-material/Delete'
import TuneIcon from '@mui/icons-material/Tune'
import { ConfirmDialog, EmptyState } from '../../../components/ui'
import type { OverlayToken } from '../../../types'
import { useOverlayTokens } from '../hooks/useOverlayTokens'
import { useRevokeOverlayToken } from '../hooks/useRevokeOverlayToken'
import { OVERLAY_THEME_LABELS, OVERLAY_VIEW_LABELS } from '../overlay/overlayConfig'
import { CreateOverlayTokenDialog } from './CreateOverlayTokenDialog'
import { EditOverlayTokenDialog } from './EditOverlayTokenDialog'
import type { OverlayDesignerGame } from './OverlayDesigner'
import type { OverlaySettingsCompetitor } from './OverlaySettingsForm'

interface OverlayTokensSectionProps {
  eventId: string
  eventUrlIdentifier: string
  eventName: string
  /**
   * True when the viewer is the event owner or an admin. Used to widen the UI
   * (e.g. surface tokens minted by other competitors and allow editing or
   * revoking them).
   */
  canManageAll: boolean
  /** Logged-in user id; used to scope the per-row edit/revoke affordances for competitors. */
  currentUserId: string | undefined
  /** Competitors used to populate the overlay designer's "pin to player" picker. */
  competitors?: OverlaySettingsCompetitor[]
  /** Games for the overlay designer's "restrict to game" picker and its sample data. */
  games: OverlayDesignerGame[]
}

export const EDIT_LOOK_LABEL = 'Edit look'
export const URL_DRIVEN_CHIP_LABEL = 'Look set by URL'

/** One line naming a token's saved look, for the list. */
function describeLook(token: OverlayToken): string {
  const settings = token.settings
  if (!settings) return URL_DRIVEN_CHIP_LABEL
  return `${OVERLAY_VIEW_LABELS[settings.view]} · ${OVERLAY_THEME_LABELS[settings.theme]}`
}

/**
 * Per-event OBS overlay-token tab. Tokens gate the public overlay endpoint.
 * Creation happens in a dialog that designs the look against a live preview
 * and reveals the raw secret exactly once (the API never returns it again);
 * afterwards this list shows the token's name and saved look, with the look
 * editable in place — an OBS source already using the token follows the
 * change. The event owner/admin sees every token; competitors see, edit and
 * revoke only their own.
 */
export function OverlayTokensSection({
  eventId,
  eventUrlIdentifier,
  eventName,
  canManageAll,
  currentUserId,
  competitors,
  games,
}: OverlayTokensSectionProps) {
  const { data: tokens, isLoading } = useOverlayTokens(eventId)
  const revokeMutation = useRevokeOverlayToken(eventId)

  const [createOpen, setCreateOpen] = useState(false)
  const [tokenToEdit, setTokenToEdit] = useState<OverlayToken | null>(null)
  const [tokenToRevoke, setTokenToRevoke] = useState<{ id: string; name: string } | null>(null)

  const handleRevoke = async () => {
    if (!tokenToRevoke) return
    await revokeMutation.mutateAsync(tokenToRevoke.id)
    setTokenToRevoke(null)
  }

  return (
    <Box component="section" aria-labelledby="overlay-tokens-heading">
      <Stack
        direction={{ xs: 'column', sm: 'row' }}
        spacing={2}
        sx={{ justifyContent: 'space-between', alignItems: { xs: 'stretch', sm: 'center' }, mb: 2 }}
      >
        <Box>
          <Typography variant="h5" component="h2" id="overlay-tokens-heading">
            OBS overlay tokens
          </Typography>
          <Typography variant="body2" color="text.secondary">
            {canManageAll
              ? 'Mint a token to embed the scoreboard in OBS. As the event owner you can see and revoke every token on this event.'
              : 'Mint a token to embed the scoreboard in OBS. You can see and revoke only the tokens you create here.'}
          </Typography>
        </Box>
        <Button
          variant="contained"
          startIcon={<AddIcon />}
          onClick={() => setCreateOpen(true)}
          sx={{ alignSelf: { xs: 'flex-start', sm: 'center' }, flexShrink: 0 }}
        >
          Create token
        </Button>
      </Stack>

      {isLoading ? (
        <Typography variant="body2" color="text.secondary">
          Loading tokens…
        </Typography>
      ) : tokens && tokens.length > 0 ? (
        <Stack spacing={1.5}>
          {tokens.map((t) => {
            // Backend already enforces edit/revoke scoping, but mirror it in the
            // UI so competitors don't see a button they can't use against
            // someone else's token.
            const canManage = canManageAll || t.createdById === currentUserId
            const isExpired = !!t.expiresAt && new Date(t.expiresAt) <= new Date()
            return (
              <Paper
                key={t.id}
                variant="outlined"
                sx={{ p: 2, display: 'flex', alignItems: 'center', gap: 2 }}
              >
                <Box sx={{ minWidth: 0, flex: 1 }}>
                  <Typography variant="subtitle1" sx={{ fontWeight: 600 }} noWrap>
                    {t.name}
                  </Typography>
                  <Stack
                    direction="row"
                    spacing={1}
                    sx={{ alignItems: 'center', flexWrap: 'wrap', mt: 0.5 }}
                  >
                    <Chip
                      label={`ot_${t.tokenPrefix}…`}
                      size="small"
                      sx={{ fontFamily: 'monospace' }}
                    />
                    {isExpired && <Chip label="Expired" size="small" color="error" />}
                    <Chip
                      label={describeLook(t)}
                      size="small"
                      variant="outlined"
                      color={t.settings ? 'primary' : 'default'}
                    />
                    <Typography variant="caption" color="text.secondary">
                      Created {new Date(t.createdAt).toLocaleDateString()} · Last used{' '}
                      {t.lastUsedAt ? new Date(t.lastUsedAt).toLocaleString() : 'never'}
                      {t.expiresAt &&
                        ` · ${isExpired ? 'Expired' : 'Expires'} ${new Date(t.expiresAt).toLocaleDateString()}`}
                    </Typography>
                  </Stack>
                </Box>
                {canManage && (
                  <>
                    <Tooltip title={EDIT_LOOK_LABEL}>
                      <IconButton
                        onClick={() => setTokenToEdit(t)}
                        aria-label={`${EDIT_LOOK_LABEL} of ${t.name}`}
                      >
                        <TuneIcon fontSize="small" />
                      </IconButton>
                    </Tooltip>
                    <Tooltip title="Revoke">
                      <span>
                        <IconButton
                          color="error"
                          onClick={() => setTokenToRevoke({ id: t.id, name: t.name })}
                          disabled={revokeMutation.isPending}
                          aria-label={`Revoke ${t.name}`}
                        >
                          <DeleteIcon fontSize="small" />
                        </IconButton>
                      </span>
                    </Tooltip>
                  </>
                )}
              </Paper>
            )
          })}
        </Stack>
      ) : (
        <EmptyState
          title="No overlay tokens yet"
          description="Create a token to design an OBS browser-source overlay for this event."
        />
      )}

      <CreateOverlayTokenDialog
        open={createOpen}
        eventId={eventId}
        eventUrlIdentifier={eventUrlIdentifier}
        eventName={eventName}
        competitors={competitors}
        games={games}
        currentUserId={currentUserId}
        onClose={() => setCreateOpen(false)}
      />
      <EditOverlayTokenDialog
        token={tokenToEdit}
        eventId={eventId}
        eventName={eventName}
        competitors={competitors}
        games={games}
        onClose={() => setTokenToEdit(null)}
      />
      <ConfirmDialog
        open={!!tokenToRevoke}
        title={`Revoke ${tokenToRevoke?.name ?? 'overlay token'}?`}
        description="Any OBS source using this token will stop working immediately. This cannot be undone."
        confirmLabel="Revoke token"
        pending={revokeMutation.isPending}
        onCancel={() => setTokenToRevoke(null)}
        onConfirm={() => void handleRevoke()}
      />
    </Box>
  )
}
