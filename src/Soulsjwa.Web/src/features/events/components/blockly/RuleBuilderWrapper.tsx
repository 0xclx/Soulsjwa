import { lazy, Suspense } from 'react'
import Alert from '@mui/material/Alert'
import { ErrorBoundary } from '../../../../components/ErrorBoundary'
import { LoadingState } from '../../../../components/ui'
import type { RuleBuilderProps } from './RuleBuilder'

const LazyRuleBuilder = lazy(() =>
  import('./RuleBuilder').then(({ RuleBuilder }) => ({ default: RuleBuilder })),
)

export const RuleBuilderWrapper = (props: RuleBuilderProps) => (
  <ErrorBoundary
    fallback={<Alert severity="error">The objective rule builder could not be loaded.</Alert>}
  >
    <Suspense fallback={<LoadingState label="Loading objective rule builder…" />}>
      <LazyRuleBuilder {...props} />
    </Suspense>
  </ErrorBoundary>
)
