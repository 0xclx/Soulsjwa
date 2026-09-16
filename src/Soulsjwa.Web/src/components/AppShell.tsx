import { memo, useEffect, useMemo, useState } from 'react'
import { Link as RouterLink, Outlet, useLocation, useNavigate } from 'react-router-dom'
import { useQueryClient } from '@tanstack/react-query'
import AppBar from '@mui/material/AppBar'
import Toolbar from '@mui/material/Toolbar'
import Typography from '@mui/material/Typography'
import Button from '@mui/material/Button'
import Box from '@mui/material/Box'
import Container from '@mui/material/Container'
import Drawer from '@mui/material/Drawer'
import IconButton from '@mui/material/IconButton'
import List from '@mui/material/List'
import ListItemButton from '@mui/material/ListItemButton'
import ListItemText from '@mui/material/ListItemText'
import Stack from '@mui/material/Stack'
import Tooltip from '@mui/material/Tooltip'
import MenuIcon from '@mui/icons-material/Menu'
import DownloadIcon from '@mui/icons-material/Download'
import { useIsAuthenticated, onAuthRefreshFailed } from '../lib/axios'
import { SCOREBOARD_CACHE_NAME } from '../lib/serviceWorkerCacheNames'
import { AppFooter } from './AppFooter'
import { ThemeToggle } from './ThemeToggle'
import { TwitchConsentInfo } from '../features/legal/components/TwitchConsentInfo'
import { useCurrentUser } from '../features/users/hooks/useCurrentUser'
import { useFeaturedEvent } from '../features/events/hooks/useFeaturedEvent'
import { useEventRules } from '../features/events/hooks/useEventRules'

/** Static download path served by ASP.NET's static-file middleware from
 *  wwwroot/downloads/, populated by the Dockerfile's `connector-build` stage
 *  (self-contained, single-file win-x64 publish of Soulsjwa.Connector). This
 *  is a real copy of the versioned exe (Soulsjwa.Connector-<Version>-win-x64.exe)
 *  that ships in the same image, so users always get the connector build
 *  paired with the running API. */
const CONNECTOR_DOWNLOAD_URL = '/downloads/Soulsjwa.Connector-latest-win-x64.exe'

interface NavItem {
  to: string
  label: string
  authOnly?: boolean
  adminOnly?: boolean
}

// Rules is inserted right after Home at render time (see `items` below, once
// a featured event's rules exist) — its slot here is deliberately skipped so
// the base order alone already reads Home, [Rules], Calendar, Events, …
const NAV_ITEMS: readonly NavItem[] = [
  { to: '/', label: 'Home' },
  { to: '/calendar', label: 'Calendar' },
  { to: '/events', label: 'Events' },
  { to: '/my-events', label: 'My Events', authOnly: true },
  { to: '/profile', label: 'Profile', authOnly: true },
  { to: '/admin', label: 'Admin', authOnly: true, adminOnly: true },
]

const isActiveRoute = (pathname: string, target: string) =>
  pathname === target || (target !== '/' && pathname.startsWith(`${target}/`))

interface NavLinkProps {
  to: string
  label: string
  active: boolean
}

const NavLink = memo(function NavLink({ to, label, active }: NavLinkProps) {
  return (
    <Button
      component={RouterLink}
      to={to}
      color="primary"
      variant={active ? 'contained' : 'text'}
      size="small"
      aria-current={active ? 'page' : undefined}
    >
      {label}
    </Button>
  )
})

export const AppShell = () => {
  const location = useLocation()
  const navigate = useNavigate()
  const queryClient = useQueryClient()
  const isAuthenticated = useIsAuthenticated()
  const { data: me } = useCurrentUser()
  const isAdmin = me?.role === 'Admin'
  const [drawerOpen, setDrawerOpen] = useState(false)
  const { data: featuredEvent } = useFeaturedEvent()
  const { data: featuredRules } = useEventRules(featuredEvent?.id ?? '')

  const handleTwitchLogin = () => {
    window.location.href = '/api/v1/auth/twitch/login'
  }

  useEffect(() => {
    return onAuthRefreshFailed(() => {
      queryClient.clear()
      // An expired/revoked session is treated the same as an explicit
      // logout: cached authenticated-session responses must not persist.
      if ('caches' in window) void caches.delete(SCOREBOARD_CACHE_NAME)
      navigate('/', { replace: true })
    })
  }, [navigate, queryClient])

  const items = useMemo(() => {
    const base = NAV_ITEMS.filter((item) => {
      if (item.authOnly && !isAuthenticated) return false
      if (item.adminOnly && !isAdmin) return false
      return true
    })
    if (featuredEvent && featuredRules?.content?.trim()) {
      // Right after Home, not appended at the end — Rules is one of the
      // first things a visitor looks for alongside the event itself.
      const homeIndex = base.findIndex((item) => item.to === '/')
      const insertAt = homeIndex === -1 ? 0 : homeIndex + 1
      return [...base.slice(0, insertAt), { to: '/rules', label: 'Rules' }, ...base.slice(insertAt)]
    }
    return base
  }, [isAuthenticated, isAdmin, featuredEvent, featuredRules])

  const drawer = (
    <Box sx={{ width: 280, p: 2 }} role="presentation">
      <Typography variant="h6" sx={{ px: 1, py: 1, color: 'primary.main' }}>
        Soulsjwa
      </Typography>
      <List aria-label="Mobile primary navigation">
        {items.map((item) => (
          <ListItemButton
            key={item.to}
            component={RouterLink}
            to={item.to}
            onClick={() => setDrawerOpen(false)}
            selected={isActiveRoute(location.pathname, item.to)}
            aria-current={isActiveRoute(location.pathname, item.to) ? 'page' : undefined}
          >
            <ListItemText primary={item.label} />
          </ListItemButton>
        ))}
      </List>
      <Button
        component="a"
        href={CONNECTOR_DOWNLOAD_URL}
        download
        startIcon={<DownloadIcon />}
        variant="outlined"
        fullWidth
        sx={{ mt: 2 }}
      >
        Connector
      </Button>
    </Box>
  )

  return (
    <Box sx={{ minHeight: '100dvh', display: 'flex', flexDirection: 'column' }}>
      <Button
        component="a"
        href="#main-content"
        sx={{
          position: 'fixed',
          top: 0,
          left: 0,
          zIndex: 'tooltip',
          transform: 'translateY(-100%)',
          '&:focus': { transform: 'translateY(0)' },
        }}
      >
        Skip to main content
      </Button>
      <AppBar component="header" position="sticky" color="default" elevation={3}>
        <Toolbar sx={{ gap: 1 }}>
          <IconButton
            color="inherit"
            aria-label="Open navigation menu"
            onClick={() => setDrawerOpen(true)}
            sx={{ display: { xs: 'inline-flex', md: 'none' } }}
          >
            <MenuIcon />
          </IconButton>
          <Typography
            variant="h6"
            sx={{
              fontWeight: 700,
              color: 'primary.main',
              textDecoration: 'none',
            }}
          >
            <Box component={RouterLink} to="/" sx={{ color: 'inherit', textDecoration: 'none' }}>
              Soulsjwa
            </Box>
          </Typography>
          <Box
            component="nav"
            aria-label="Primary"
            sx={{ display: { xs: 'none', md: 'flex' }, gap: 0.75, ml: 3 }}
          >
            {items.map((item) => (
              <NavLink
                key={item.to}
                to={item.to}
                label={item.label}
                active={isActiveRoute(location.pathname, item.to)}
              />
            ))}
          </Box>
          <Box sx={{ flexGrow: 1 }} />
          <Stack direction="row" spacing={1} sx={{ alignItems: 'center' }}>
            <Tooltip title="Download the Windows connector for live game data">
              <Button
                component="a"
                href={CONNECTOR_DOWNLOAD_URL}
                download
                color="inherit"
                size="small"
                variant="outlined"
                startIcon={<DownloadIcon fontSize="small" />}
                sx={{ display: { xs: 'none', sm: 'inline-flex' } }}
              >
                Connector
              </Button>
            </Tooltip>
            <ThemeToggle />
            {!isAuthenticated && (
              <>
                <Button
                  onClick={handleTwitchLogin}
                  color="primary"
                  variant="contained"
                  size="small"
                >
                  Sign in with Twitch
                </Button>
                <TwitchConsentInfo />
              </>
            )}
          </Stack>
        </Toolbar>
      </AppBar>
      <Drawer open={drawerOpen} onClose={() => setDrawerOpen(false)}>
        {drawer}
      </Drawer>
      <Box id="main-content" component="main" tabIndex={-1} sx={{ flexGrow: 1 }}>
        <Container maxWidth="xl" sx={{ py: { xs: 3, md: 5 } }}>
          <Outlet />
        </Container>
      </Box>
      <AppFooter />
    </Box>
  )
}
