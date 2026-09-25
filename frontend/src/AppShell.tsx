import { useEffect } from 'react'
import { NavLink, Outlet, useLocation } from 'react-router'

const navigation = [
  { to: '/', label: 'Dashboard', end: true },
  { to: '/history', label: 'History' },
  { to: '/alerts', label: 'Alerts' },
  { to: '/notifications', label: 'Notifications' },
  { to: '/settings', label: 'Settings' },
]

export default function AppShell() {
  const location = useLocation()

  useEffect(() => {
    const elements = document.querySelectorAll<HTMLElement>('#main-content > header, #main-content > section')
    elements.forEach((element, index) => {
      element.classList.add('reveal')
      element.style.setProperty('--reveal-index', String(Math.min(index, 5)))
    })

    const observer = new IntersectionObserver((entries) => {
      entries.forEach((entry) => {
        if (!entry.isIntersecting) return
        entry.target.classList.add('is-visible')
        observer.unobserve(entry.target)
      })
    }, { threshold: 0.08 })

    elements.forEach((element) => observer.observe(element))
    return () => observer.disconnect()
  }, [location.pathname])

  return (
    <div className="shell">
      <a className="skip-link" href="#main-content">Skip to content</a>
      <header className="app-header">
        <NavLink className="brand" to="/" end><span className="brand-mark" aria-hidden="true"><svg viewBox="0 0 32 32" fill="none"><path d="M3 18h6l4-9 5 15 4-8h7" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round" /></svg></span><span><strong>wanetra</strong><small>WAN health monitor</small></span></NavLink>
        <nav aria-label="Main navigation">
          {navigation.map(({ to, label, end }) => <NavLink key={to} to={to} end={end}>{label}</NavLink>)}
        </nav>
      </header>
      <Outlet />
    </div>
  )
}
