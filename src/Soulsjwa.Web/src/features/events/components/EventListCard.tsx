import { memo } from 'react'
import { Link as RouterLink } from 'react-router-dom'
import Box from '@mui/material/Box'
import Button from '@mui/material/Button'
import Card from '@mui/material/Card'
import CardActionArea from '@mui/material/CardActionArea'
import CardContent from '@mui/material/CardContent'
import Chip from '@mui/material/Chip'
import Stack from '@mui/material/Stack'
import Typography from '@mui/material/Typography'
import ContentCopyIcon from '@mui/icons-material/ContentCopy'
import StarIcon from '@mui/icons-material/Star'
import UnarchiveIcon from '@mui/icons-material/Unarchive'
import type { EventListItem } from '../../../types'
import { getEventPath } from '../eventUrl'

interface EventListCardProps {
  event: EventListItem
  isAdmin: boolean
  onUnarchive: (id: string) => void
  unarchivePending: boolean
  onDuplicate: (id: string) => void
  duplicatePending: boolean
}

const DESCRIPTION_MAX = 100

/**
 * A single event summary card in the Events list: name, status chips, a
 * truncated description, and competitor/game counts. Admins get an inline
 * unarchive action on archived events. The whole card is a router link.
 */
export const EventListCard = memo(function EventListCard({
  event,
  isAdmin,
  onUnarchive,
  unarchivePending,
  onDuplicate,
  duplicatePending,
}: EventListCardProps) {
  const description = event.description ?? ''
  const truncated =
    description.length > DESCRIPTION_MAX ? description.slice(0, DESCRIPTION_MAX) + '…' : description

  return (
    <Card variant="outlined" sx={{ height: '100%' }}>
      <CardActionArea
        component={RouterLink}
        to={getEventPath(event.id, event.urlAlias)}
        sx={{ height: '100%' }}
      >
        <CardContent sx={{ p: { xs: 2, md: 2.5 } }}>
          <Stack
            direction={{ xs: 'column', sm: 'row' }}
            spacing={2}
            sx={{ justifyContent: 'space-between', alignItems: 'flex-start' }}
          >
            <Box sx={{ minWidth: 0 }}>
              <Stack direction="row" spacing={1} sx={{ alignItems: 'center', flexWrap: 'wrap' }}>
                <Typography variant="h6" component="h3" noWrap>
                  {event.name}
                </Typography>
                <Chip
                  label={event.isStarted ? 'Live' : 'Stopped'}
                  size="small"
                  color={event.isStarted ? 'success' : 'default'}
                  variant="outlined"
                />
                {event.isArchived && <Chip label="Archived" size="small" color="warning" />}
                {event.isFeatured && (
                  <Chip
                    icon={<StarIcon sx={{ fontSize: 16 }} />}
                    label="Featured"
                    size="small"
                    color="primary"
                  />
                )}
              </Stack>
              {truncated && (
                <Typography variant="body2" color="text.secondary" sx={{ mt: 0.5 }}>
                  {truncated}
                </Typography>
              )}
            </Box>
            <Stack
              sx={{
                alignItems: { xs: 'flex-start', sm: 'flex-end' },
                color: 'text.secondary',
                flexShrink: 0,
              }}
            >
              <Typography variant="caption">{event.competitorCount} competitors</Typography>
              <Typography variant="caption">{event.gameCount} games</Typography>
              {isAdmin && event.isArchived && (
                <Button
                  size="small"
                  startIcon={<UnarchiveIcon />}
                  onClick={(e) => {
                    e.preventDefault()
                    e.stopPropagation()
                    onUnarchive(event.id)
                  }}
                  disabled={unarchivePending}
                  sx={{ mt: 0.5 }}
                >
                  Unarchive
                </Button>
              )}
              {isAdmin && (
                <Button
                  size="small"
                  startIcon={<ContentCopyIcon />}
                  onClick={(e) => {
                    e.preventDefault()
                    e.stopPropagation()
                    onDuplicate(event.id)
                  }}
                  disabled={duplicatePending}
                  sx={{ mt: 0.5 }}
                >
                  Duplicate
                </Button>
              )}
            </Stack>
          </Stack>
        </CardContent>
      </CardActionArea>
    </Card>
  )
})
