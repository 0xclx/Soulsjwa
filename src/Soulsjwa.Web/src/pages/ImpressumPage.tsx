import Stack from '@mui/material/Stack'
import { ErrorMessage, LoadingState, PageHeader } from '../components/ui'
import { MarkdownView } from '../features/markdown/MarkdownView'
import { useLegalDocument } from '../features/legal/hooks/useLegalDocument'

/**
 * Public Impressum page. An empty document renders the app's 404 page,
 * not a blank one — thrown in the shape React Router's
 * `isRouteErrorResponse` duck-types (status/statusText/internal/data), so
 * the nearest errorElement (RouteErrorElement) handles it exactly like a
 * genuinely unmatched route.
 */
export const ImpressumPage = () => {
  const { data, isLoading, isError } = useLegalDocument('Impressum')

  if (isLoading) return <LoadingState label="Loading…" />
  if (isError || !data) return <ErrorMessage message="Failed to load this document." />
  if (!data.content?.trim()) {
    throw { status: 404, statusText: 'Not Found', internal: false, data: 'Not Found' }
  }

  return (
    <Stack spacing={3}>
      <PageHeader eyebrow="Legal" title="Impressum" />
      <MarkdownView source={data.content} />
    </Stack>
  )
}
