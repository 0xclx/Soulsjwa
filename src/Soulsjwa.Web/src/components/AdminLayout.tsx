import { Outlet, useLocation } from 'react-router-dom'
import Stack from '@mui/material/Stack'
import { AdminSectionTabs } from '../features/admin/components/AdminSectionTabs'
import { getAdminSection } from '../features/admin/adminSections'
import { useCurrentUser } from '../features/users/hooks/useCurrentUser'
import { ErrorMessage, LoadingState, PageHeader } from './ui'

export interface AdminRouteContext {
  currentUserId: string
}

export const AdminLayout = () => {
  const { data: currentUser, isLoading } = useCurrentUser()
  const location = useLocation()

  if (isLoading) return <LoadingState label="Loading admin console…" />
  if (!currentUser || currentUser.role !== 'Admin') {
    return <ErrorMessage message="This page is for admins only." />
  }

  return (
    <Stack spacing={3}>
      <PageHeader
        eyebrow="Admin"
        title="Control center"
        description="Global administration is separated from per-event competitor and objective management."
      />
      <AdminSectionTabs activeSection={getAdminSection(location.pathname)} />
      <Outlet context={{ currentUserId: currentUser.id } satisfies AdminRouteContext} />
    </Stack>
  )
}
