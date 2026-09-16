import { Link as RouterLink } from 'react-router-dom'
import Box from '@mui/material/Box'
import Button from '@mui/material/Button'
import Chip from '@mui/material/Chip'
import Stack from '@mui/material/Stack'
import Tooltip from '@mui/material/Tooltip'
import Typography from '@mui/material/Typography'
import ArchiveIcon from '@mui/icons-material/Archive'
import ArrowBackIcon from '@mui/icons-material/ArrowBack'
import ContentCopyIcon from '@mui/icons-material/ContentCopy'
import EditIcon from '@mui/icons-material/Edit'
import PlayArrowIcon from '@mui/icons-material/PlayArrow'
import StarIcon from '@mui/icons-material/Star'
import StarBorderIcon from '@mui/icons-material/StarBorder'
import StopIcon from '@mui/icons-material/Stop'
import UnarchiveIcon from '@mui/icons-material/Unarchive'
import { PageHeader, StatCard } from '../../../components/ui'
import type { EventResponse } from '../../../types'
import { TIE_BREAK_MODE_LABELS } from '../../../types'

interface EventDetailHeaderProps {
  event: EventResponse
  /** True for the event creator or any admin — see `canManageEvent`. */
  canManage: boolean
  isAdmin: boolean
  /** Hides the Edit button while the edit dialog is open. */
  isEditing: boolean
  startStopPending: boolean
  archivePending: boolean
  unarchivePending: boolean
  featurePending: boolean
  duplicatePending: boolean
  onStartStop: () => void
  onEdit: () => void
  onArchive: () => void
  onUnarchive: () => void
  onFeatureToggle: () => void
  onDuplicate: () => void
}

/**
 * Event detail hero: back link, title/status metadata, owner/admin actions, and
 * the at-a-glance stat cards (competitors, games, objectives).
 */
export const EventDetailHeader = ({
  event,
  canManage,
  isAdmin,
  isEditing,
  startStopPending,
  archivePending,
  unarchivePending,
  featurePending,
  duplicatePending,
  onStartStop,
  onEdit,
  onArchive,
  onUnarchive,
  onFeatureToggle,
  onDuplicate,
}: EventDetailHeaderProps) => {
  const objectiveCount = event.games.reduce((sum, game) => sum + game.objectives.length, 0)
  const hasActiveGame = event.games.some((g) => g.isEnabled)

  return (
    <>
      <Button
        component={RouterLink}
        to="/events"
        startIcon={<ArrowBackIcon />}
        size="small"
        color="inherit"
        sx={{ alignSelf: 'flex-start', opacity: 0.75 }}
      >
        Back to events
      </Button>
      <PageHeader
        eyebrow={event.isArchived ? 'Archived event' : event.isStarted ? 'Live event' : 'Event'}
        title={event.name}
        description={event.description || 'No description provided yet.'}
        meta={
          <Stack
            direction="row"
            spacing={1}
            sx={{ alignItems: 'center', flexWrap: 'wrap', gap: 1 }}
          >
            <Chip
              label={event.isStarted ? 'Started' : 'Stopped'}
              color={event.isStarted ? 'success' : 'default'}
              size="small"
            />
            <Chip
              label={TIE_BREAK_MODE_LABELS[event.tieBreakMode]}
              size="small"
              variant="outlined"
            />
            {event.isFeatured && (
              <Chip
                icon={<StarIcon sx={{ fontSize: 16 }} />}
                label="Featured"
                color="warning"
                size="small"
              />
            )}
            <Typography variant="caption" color="text.disabled">
              Created {new Date(event.createdAt).toLocaleDateString()}
            </Typography>
          </Stack>
        }
        actions={
          <>
            {canManage && !event.isArchived && (
              <Tooltip
                title={
                  event.isStarted && hasActiveGame
                    ? 'Disable all games before stopping the event.'
                    : ''
                }
              >
                <span>
                  <Button
                    size="small"
                    variant="outlined"
                    color={event.isStarted ? 'warning' : 'success'}
                    startIcon={event.isStarted ? <StopIcon /> : <PlayArrowIcon />}
                    onClick={onStartStop}
                    disabled={startStopPending || (event.isStarted && hasActiveGame)}
                  >
                    {event.isStarted ? 'Stop Event' : 'Start Event'}
                  </Button>
                </span>
              </Tooltip>
            )}
            {canManage && !event.isArchived && !isEditing && (
              <Button size="small" variant="outlined" startIcon={<EditIcon />} onClick={onEdit}>
                Edit
              </Button>
            )}
            {canManage && !event.isArchived && (
              <Button
                size="small"
                variant="outlined"
                color="error"
                startIcon={<ArchiveIcon />}
                onClick={onArchive}
                disabled={archivePending}
              >
                Archive
              </Button>
            )}
            {canManage && event.isArchived && (
              <Button
                size="small"
                variant="outlined"
                startIcon={<UnarchiveIcon />}
                onClick={onUnarchive}
                disabled={unarchivePending}
              >
                Unarchive
              </Button>
            )}
            {isAdmin && (
              <Button
                size="small"
                variant="outlined"
                color={event.isFeatured ? 'warning' : 'inherit'}
                startIcon={event.isFeatured ? <StarIcon /> : <StarBorderIcon />}
                onClick={onFeatureToggle}
                disabled={featurePending}
              >
                {event.isFeatured ? 'Unfeature event' : 'Feature event'}
              </Button>
            )}
            {isAdmin && (
              <Button
                size="small"
                variant="outlined"
                startIcon={<ContentCopyIcon />}
                onClick={onDuplicate}
                disabled={duplicatePending}
              >
                Duplicate
              </Button>
            )}
          </>
        }
      />

      <Box
        sx={{
          display: 'grid',
          gridTemplateColumns: { xs: '1fr', sm: 'repeat(3, minmax(0, 1fr))' },
          gap: 2,
        }}
      >
        <StatCard
          label="Competitors"
          value={event.competitors.length}
          helper="Assigned to this event"
        />
        <StatCard label="Games" value={event.games.length} helper="Configured for this event" />
        <StatCard label="Objectives" value={objectiveCount} helper="Across event games" />
      </Box>
    </>
  )
}
