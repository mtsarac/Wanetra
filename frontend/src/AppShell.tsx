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
      <header className="app-header">
        <NavLink className="brand" to="/" end><span className="brand-mark">W</span><span><strong>WANETRA</strong><small>WAN HEALTH / SELF-HOSTED</small></span></NavLink>
        <nav aria-label="Main navigation">
          {navigation.map(({ to, label, end }) => <NavLink key={to} to={to} end={end}>{label}</NavLink>)}
        </nav>
      </header>
      <Outlet />
    </div>
  )
}
