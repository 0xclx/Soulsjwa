import { isValidElement, type ReactElement } from 'react'
import { describe, expect, it } from 'vitest'
import { ProtectedRoute } from '../components/ProtectedRoute'
import { MY_EVENTS_TAB_NAV } from '../features/myEvents/myEventsTabs'
import { routes } from './index'

const childComponent = (element: ReactElement) => {
  const suspense =
    element.type === ProtectedRoute
      ? (element as ReactElement<{ children: ReactElement }>).props.children
      : element
  const routeContent = suspense as ReactElement<{ children: ReactElement }>
  if (!isValidElement(routeContent.props.children)) {
    throw new Error('Expected route content')
  }
  return routeContent.props.children.type
}

describe('application routes', () => {
  const shellRoutes = routes.find((route) => !route.path)?.children ?? []
  const eventRoute = shellRoutes.find((route) => route.path === '/events/:id')
  const adminRoute = shellRoutes.find((route) => route.path === '/admin')

  it('uses a distinct page component for every event child route', () => {
    const pageRoutes =
      eventRoute?.children?.filter((route) =>
        [undefined, 'competitors', 'games', 'scoreboard', 'rules', 'tokens', 'activity'].includes(
          route.path,
        ),
      ) ?? []

    expect(pageRoutes).toHaveLength(7)
    expect(
      new Set(pageRoutes.map((route) => childComponent(route.element as ReactElement))).size,
    ).toBe(7)
  })

  it('protects token and activity pages at the router boundary', () => {
    for (const path of ['tokens', 'activity']) {
      const route = eventRoute?.children?.find((candidate) => candidate.path === path)
      expect((route?.element as ReactElement).type).toBe(ProtectedRoute)
    }
  })

  it('nests distinct admin pages under a protected layout', () => {
    expect((adminRoute?.element as ReactElement).type).toBe(ProtectedRoute)
    expect(adminRoute?.children?.map((route) => route.path)).toEqual([
      undefined,
      'users',
      'catalog',
      'audits',
      'feature-flags',
      'legal',
      'theme',
      'twitch-extension',
    ])

    const pageTypes = adminRoute?.children?.map((route) =>
      childComponent(route.element as ReactElement),
    )
    expect(new Set(pageTypes).size).toBe(8)
  })

  it('registers the public scoreboard deep link outside the app shell (chrome-free)', () => {
    const scoreboardRoute = routes.find((route) => route.path === '/scoreboard/:eventIdentifier')
    const overlayRoute = routes.find((route) => route.path === '/events/:id/overlay')

    expect(scoreboardRoute).toBeDefined()
    expect(scoreboardRoute).not.toBe(overlayRoute)
    expect(childComponent(scoreboardRoute!.element as ReactElement)).not.toBe(
      childComponent(overlayRoute!.element as ReactElement),
    )
  })

  it('registers every My Events tab path, protected', () => {
    // Driven off the nav model rather than a copied list, so adding a tab
    // without its route fails here instead of 404-ing at runtime.
    for (const item of MY_EVENTS_TAB_NAV) {
      const route = shellRoutes.find((candidate) => candidate.path === item.to)
      expect(route, item.to).toBeDefined()
      expect((route?.element as ReactElement).type, item.to).toBe(ProtectedRoute)
    }
  })

  it('registers public routes for the Impressum and Datenschutz pages', () => {
    const impressumRoute = shellRoutes.find((route) => route.path === '/impressum')
    const datenschutzRoute = shellRoutes.find((route) => route.path === '/datenschutz')

    expect(impressumRoute).toBeDefined()
    expect(datenschutzRoute).toBeDefined()
    expect(
      new Set([
        childComponent(impressumRoute!.element as ReactElement),
        childComponent(datenschutzRoute!.element as ReactElement),
      ]).size,
    ).toBe(2)
  })
})
