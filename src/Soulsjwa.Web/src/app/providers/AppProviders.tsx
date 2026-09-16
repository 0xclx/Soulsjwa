import { lazy, Suspense, useEffect, useState, type ReactNode } from 'react'
import { QueryClientProvider } from '@tanstack/react-query'
import Box from '@mui/material/Box'
import { queryClient } from '../../lib/react-query'
import { tokenManager } from '../../lib/axios'
import { Spinner } from '../../components/ui'
import { ThemeModeProvider } from '../../theme'

// Tree-shaken in production — the dynamic import is never reached when
// import.meta.env.DEV is false, so Vite strips the devtools chunk entirely.
const ReactQueryDevtools = import.meta.env.DEV
  ? lazy(() =>
      import('@tanstack/react-query-devtools').then((m) => ({
        default: m.ReactQueryDevtools,
      })),
    )
  : () => null

interface AppProvidersProps {
  children: ReactNode
}

export const AppProviders = ({ children }: AppProvidersProps) => {
  const [isReady, setIsReady] = useState(false)

  useEffect(() => {
    tokenManager.bootstrap().finally(() => setIsReady(true))
    return () => tokenManager.stop()
  }, [])

  return (
    <QueryClientProvider client={queryClient}>
      <ThemeModeProvider>
        {isReady ? (
          <>
            {children}
            <Suspense>
              <ReactQueryDevtools initialIsOpen={false} />
            </Suspense>
          </>
        ) : (
          <Box sx={{ display: 'flex', justifyContent: 'center', py: 8 }}>
            <Spinner />
          </Box>
        )}
      </ThemeModeProvider>
    </QueryClientProvider>
  )
}
