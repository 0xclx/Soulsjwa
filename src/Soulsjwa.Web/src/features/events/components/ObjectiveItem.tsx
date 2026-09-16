import { memo } from 'react'
import Box from '@mui/material/Box'
import Checkbox from '@mui/material/Checkbox'
import Chip from '@mui/material/Chip'
import IconButton from '@mui/material/IconButton'
import Stack from '@mui/material/Stack'
import Tooltip from '@mui/material/Tooltip'
import Typography from '@mui/material/Typography'
import BoltIcon from '@mui/icons-material/Bolt'
import CancelIcon from '@mui/icons-material/Cancel'
import DeleteIcon from '@mui/icons-material/Delete'
import DragIndicatorIcon from '@mui/icons-material/DragIndicator'
import EditIcon from '@mui/icons-material/Edit'
import ReplayIcon from '@mui/icons-material/Replay'
import ScheduleIcon from '@mui/icons-material/Schedule'
import type { DragHandleProps, DragPosition, DragRowProps } from '../hooks/useDragReorder'
import type { Objective } from '../../../types'

export interface ObjectiveItemProps {
  objective: Objective
  /** Whether the current completion-target user has this objective completed. */
  isCompleted: boolean
  completedAt: string | null
  /** Whether the current completion-target user has this objective failed. */
  isFailed: boolean
  failedAt: string | null
  /** True if the current user is allowed to toggle completion/failure for the target. */
  canToggle: boolean
  canManage: boolean
  canEditTime: boolean
  /** Disabled while a mutation is in flight or the event is stopped. */
  disabled: boolean
  onToggle: () => void
  onToggleFailure: () => void
  onEdit: () => void
  onDelete: () => void
  onEditTime: () => void
  /** Present only when reordering is allowed (owner + objectives editable). */
  dragHandleProps?: DragHandleProps
  dragRowProps: DragRowProps
  dropIndicator: DragPosition | null
  isDragging: boolean
}

/**
 * A single objective row: pending/completed/failed status, auto-rule badge,
 * the manual complete/fail/reset controls, and the owner/admin management
 * affordances (edit, delete, edit completion time).
 */
export const ObjectiveItem = memo(function ObjectiveItem({
  objective,
  isCompleted,
  completedAt,
  isFailed,
  failedAt,
  canToggle,
  canManage,
  canEditTime,
  disabled,
  onToggle,
  onToggleFailure,
  onEdit,
  onDelete,
  onEditTime,
  dragHandleProps,
  dragRowProps,
  dropIndicator,
  isDragging,
}: ObjectiveItemProps) {
  return (
    <>
      {dropIndicator === 'before' && (
        <Box
          component="li"
          sx={{ height: 2, bgcolor: 'primary.main', borderRadius: 1, listStyle: 'none' }}
        />
      )}
      <Stack
        component="li"
        direction={{ xs: 'column', sm: 'row' }}
        spacing={1}
        ref={dragRowProps.ref}
        onDragOver={dragRowProps.onDragOver}
        onDrop={dragRowProps.onDrop}
        sx={{
          justifyContent: 'space-between',
          alignItems: { xs: 'stretch', sm: 'center' },
          p: 1.25,
          bgcolor: 'action.hover',
          opacity: isDragging ? 0.4 : 1,
        }}
      >
        <Stack direction="row" spacing={1} sx={{ alignItems: 'center', flex: 1, minWidth: 0 }}>
          {dragHandleProps && (
            <IconButton size="small" {...dragHandleProps} aria-label={`Reorder ${objective.name}`}>
              <DragIndicatorIcon fontSize="small" />
            </IconButton>
          )}
          {canToggle ? (
            <Checkbox
              size="small"
              checked={isCompleted}
              disabled={disabled || isFailed}
              onChange={onToggle}
              slotProps={{
                input: {
                  'aria-label': `${isCompleted ? 'Uncomplete' : 'Complete'} ${objective.name}`,
                },
              }}
              sx={{ p: 0.5 }}
            />
          ) : (
            <Checkbox size="small" checked={isCompleted} disabled sx={{ p: 0.5 }} tabIndex={-1} />
          )}
          <Typography
            variant="body2"
            sx={{
              textDecoration: isCompleted || isFailed ? 'line-through' : 'none',
              minWidth: 0,
            }}
          >
            {objective.name}
          </Typography>
          {objective.rule && (
            <Tooltip title="Has auto-completion rule">
              <Chip
                icon={<BoltIcon />}
                label="auto"
                size="small"
                color="primary"
                variant="outlined"
              />
            </Tooltip>
          )}
          {isFailed && (
            <Tooltip title={failedAt ? `Failed ${new Date(failedAt).toLocaleString()}` : 'Failed'}>
              <Chip label="Failed" size="small" color="error" variant="outlined" />
            </Tooltip>
          )}
        </Stack>
        <Stack
          direction="row"
          spacing={1}
          sx={{
            alignItems: 'center',
            justifyContent: { xs: 'space-between', sm: 'flex-end' },
            flexShrink: 0,
          }}
        >
          <Typography
            variant="caption"
            sx={{
              color: 'text.secondary',
              width: { sm: 150 },
              textAlign: 'right',
              display: { xs: 'none', sm: 'block' },
            }}
          >
            {completedAt ? new Date(completedAt).toLocaleString() : ''}
          </Typography>
          <Typography
            variant="body2"
            sx={{ fontWeight: 600, color: 'primary.main', width: 64, textAlign: 'right' }}
          >
            {objective.score} pts
          </Typography>
          {canToggle && (
            <Box sx={{ width: 40, display: 'flex', justifyContent: 'center', flexShrink: 0 }}>
              {!isCompleted && (
                <Tooltip title={isFailed ? 'Reset (mark pending again)' : 'Mark failed'}>
                  <span>
                    <IconButton
                      size="small"
                      onClick={onToggleFailure}
                      disabled={disabled}
                      sx={{ color: isFailed ? 'warning.main' : 'error.main' }}
                    >
                      {isFailed ? <ReplayIcon fontSize="small" /> : <CancelIcon fontSize="small" />}
                    </IconButton>
                  </span>
                </Tooltip>
              )}
            </Box>
          )}
          {(canEditTime || canManage) && (
            <Stack
              direction="row"
              spacing={0.5}
              sx={{ width: canManage ? 84 : 40, justifyContent: 'flex-end', flexShrink: 0 }}
            >
              {canEditTime && (
                <Tooltip title="Edit completion time">
                  <span>
                    <IconButton
                      size="small"
                      onClick={onEditTime}
                      disabled={!isCompleted}
                      sx={{ color: 'primary.main' }}
                    >
                      <ScheduleIcon fontSize="small" />
                    </IconButton>
                  </span>
                </Tooltip>
              )}
              {canManage && (
                <>
                  <Tooltip title="Edit objective">
                    <IconButton size="small" onClick={onEdit}>
                      <EditIcon fontSize="small" />
                    </IconButton>
                  </Tooltip>
                  <Tooltip title="Delete objective">
                    <IconButton size="small" color="error" onClick={onDelete}>
                      <DeleteIcon fontSize="small" />
                    </IconButton>
                  </Tooltip>
                </>
              )}
            </Stack>
          )}
        </Stack>
      </Stack>
      {dropIndicator === 'after' && (
        <Box
          component="li"
          sx={{ height: 2, bgcolor: 'primary.main', borderRadius: 1, listStyle: 'none' }}
        />
      )}
    </>
  )
})
