import { Suspense, type ReactNode } from 'react'
import { createBrowserRouter, type RouteObject } from 'react-router-dom'
import { AppShell } from '../components/AppShell'
import { ProtectedRoute } from '../components/ProtectedRoute'
import { RouteErrorElement } from '../components/RouteErrorElement'
import { LoadingState } from '../components/ui'
import { MY_EVENTS_TAB_NAV } from '../features/myEvents/myEventsTabs'
import {
  AdminAuditsPage,
  AdminCatalogPage,
  AdminFeatureFlagsPage,
  AdminTwitchExtensionPage,
  AdminLayout,
  AdminLegalPage,
  AdminOverviewPage,
  AdminThemePage,
  AdminUsersPage,
  AuthCallbackPage,
  CalendarPage,
  DatenschutzPage,
  EventActivityPage,
  EventCalendarPage,
  EventCompetitorsPage,
  EventGamesPage,
  EventLayout,
  EventOverviewPage,
  EventRulesPage,
  EventTokensPage,
  EventsPage,
  HomePage,
  EventScoreboardPage,
  ImpressumPage,
  MyEventsPage,
  OverlayPage,
  ProfilePage,
  PublicScoreboardPage,
  RulesPage,
} from './lazyPages'

const routeContent = (children: ReactNode) => (
  <Suspense fallback={<LoadingState label="Loading page…" />}>{children}</Suspense>
)

const protectedContent = (children: ReactNode) => (
  <ProtectedRoute>{routeContent(children)}</ProtectedRoute>
)

export const routes: RouteObject[] = [
  {
    path: '/events/:id/overlay',
    element: routeContent(<OverlayPage />),
    errorElement: <RouteErrorElement />,
  },
  {
    path: '/scoreboard/:eventIdentifier',
    element: routeContent(<PublicScoreboardPage />),
    errorElement: <RouteErrorElement />,
  },
  {
    element: <AppShell />,
    errorElement: <RouteErrorElement />,
    children: [
      { path: '/', element: routeContent(<HomePage />) },
      { path: '/auth/callback', element: routeContent(<AuthCallbackPage />) },
      { path: '/profile', element: protectedContent(<ProfilePage />) },
      { path: '/impressum', element: routeContent(<ImpressumPage />) },
      { path: '/datenschutz', element: routeContent(<DatenschutzPage />) },
      { path: '/calendar', element: routeContent(<CalendarPage />) },
      { path: '/rules', element: routeContent(<RulesPage />) },
      { path: '/events', element: routeContent(<EventsPage />) },
      // One route per dashboard tab so the chosen tab survives a refresh and
      // can be linked to. Derived from the tab bar's own nav model, so a new
      // tab cannot ship with a link to a path that 404s.
      ...MY_EVENTS_TAB_NAV.map((tab) => ({
        path: tab.to,
        element: protectedContent(<MyEventsPage />),
      })),
      {
        path: '/events/:id',
        element: routeContent(<EventLayout />),
        errorElement: <RouteErrorElement />,
        children: [
          { index: true, element: routeContent(<EventOverviewPage />) },
          { path: 'competitors', element: routeContent(<EventCompetitorsPage />) },
          { path: 'games', element: routeContent(<EventGamesPage />) },
          { path: 'tokens', element: protectedContent(<EventTokensPage />) },
          { path: 'activity', element: protectedContent(<EventActivityPage />) },
          { path: 'scoreboard', element: routeContent(<EventScoreboardPage />) },
          { path: 'rules', element: routeContent(<EventRulesPage />) },
          { path: 'calendar', element: routeContent(<EventCalendarPage />) },
        ],
      },
      {
        path: '/admin',
        element: protectedContent(<AdminLayout />),
        children: [
          { index: true, element: routeContent(<AdminOverviewPage />) },
          { path: 'users', element: routeContent(<AdminUsersPage />) },
          { path: 'catalog', element: routeContent(<AdminCatalogPage />) },
          { path: 'audits', element: routeContent(<AdminAuditsPage />) },
          { path: 'feature-flags', element: routeContent(<AdminFeatureFlagsPage />) },
          { path: 'legal', element: routeContent(<AdminLegalPage />) },
          { path: 'theme', element: routeContent(<AdminThemePage />) },
          { path: 'twitch-extension', element: routeContent(<AdminTwitchExtensionPage />) },
        ],
      },
    ],
  },
]

export const router = createBrowserRouter(routes)
