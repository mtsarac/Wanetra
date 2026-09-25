import { NavLink, Outlet } from 'react-router'

const navigation = [
  { to: '/', label: 'Dashboard', end: true },
  { to: '/history', label: 'History' },
  { to: '/alerts', label: 'Alerts' },
  { to: '/notifications', label: 'Notifications' },
  { to: '/settings', label: 'Settings' },
]

export default function AppShell() {
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
