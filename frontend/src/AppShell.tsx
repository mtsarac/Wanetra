import { useEffect, useRef } from 'react'
import { Link, NavLink, Outlet, useLocation } from 'react-router'

const navigation = [
  { to: '/', label: 'Dashboard', end: true },
  { to: '/history', label: 'History' },
  { to: '/alerts', label: 'Alerts' },
  { to: '/notifications', label: 'Notifications' },
  { to: '/settings', label: 'Settings' },
]

const pageTitles: Record<string, string> = {
  '/': 'Dashboard · Wanetra',
  '/history': 'History · Wanetra',
  '/alerts': 'Alerts · Wanetra',
  '/notifications': 'Notifications · Wanetra',
  '/settings': 'Settings · Wanetra',
}

export default function AppShell() {
  const location = useLocation()
  const previousPathname = useRef<string | null>(null)

  // Meaningful <title> per route + screen-reader focus shift on navigation
  useEffect(() => {
    const nextTitle = pageTitles[location.pathname] ?? 'Wanetra · WAN health monitor'
    document.title = nextTitle

    if (previousPathname.current !== null && previousPathname.current !== location.pathname) {
      const target = document.querySelector<HTMLElement>('#main-content h1') ?? document.querySelector<HTMLElement>('#main-content')
      if (target) {
        if (!target.hasAttribute('tabindex')) {
          target.setAttribute('tabindex', '-1')
        }
        target.focus({ preventScroll: true })
      }
    }
    previousPathname.current = location.pathname
  }, [location.pathname])

  // Reveal animation: assign indices; works without IntersectionObserver and under reduced-motion
  useEffect(() => {
    const elements = document.querySelectorAll<HTMLElement>('#main-content > header, #main-content > section')
    elements.forEach((element, index) => {
      element.classList.add('reveal')
      element.style.setProperty('--reveal-index', String(Math.min(index, 5)))
    })
  }, [location.pathname])

  return (
    <div className="shell">
      <a className="skip-link" href="#main-content">Skip to content</a>
      <header className="app-header">
        <Link className="brand" to="/">
          <span className="brand-mark" aria-hidden="true">
            <svg viewBox="0 0 32 32" fill="none">
              <path d="M3 18h6l4-9 5 15 4-8h7" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round" />
            </svg>
          </span>
          <span><strong>wanetra</strong><small>WAN health monitor</small></span>
        </Link>
        <nav aria-label="Main navigation">
          {navigation.map(({ to, label, end }) => <NavLink key={to} to={to} end={end}>{label}</NavLink>)}
        </nav>
      </header>
      <Outlet />
    </div>
  )
}
