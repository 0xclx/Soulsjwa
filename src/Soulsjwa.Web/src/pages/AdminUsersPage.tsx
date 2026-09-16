import { useOutletContext } from 'react-router-dom'
import type { AdminRouteContext } from '../components/AdminLayout'
import { UsersTab } from '../features/admin/components/UsersTab'

export const AdminUsersPage = () => {
  const { currentUserId } = useOutletContext<AdminRouteContext>()
  return <UsersTab currentUserId={currentUserId} />
}
