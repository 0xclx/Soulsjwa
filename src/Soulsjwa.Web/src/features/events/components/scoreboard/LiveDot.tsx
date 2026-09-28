import Box from '@mui/material/Box'

const LIVE_LABELS = { live: 'Live', offline: 'Offline' } as const

interface LiveDotProps {
  isLive: boolean
}

/** Green/red stream-status dot, announced as "Live" or "Offline". */
export const LiveDot = ({ isLive }: LiveDotProps) => {
  const label = isLive ? LIVE_LABELS.live : LIVE_LABELS.offline
  return (
    <Box
      role="img"
      aria-label={label}
      title={label}
      sx={{
        width: 10,
        height: 10,
        borderRadius: '50%',
        bgcolor: isLive ? 'success.main' : 'error.main',
        flexShrink: 0,
      }}
    />
  )
}
