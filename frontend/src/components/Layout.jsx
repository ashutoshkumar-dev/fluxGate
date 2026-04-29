import { NavLink, Outlet, useNavigate } from 'react-router-dom'
import useAuthStore from '../stores/useAuthStore'
import { cn } from '../lib/utils'

const navItems = [
  { to: '/dashboard', label: 'Dashboard' },
  { to: '/routes',    label: 'Routes' },
  { to: '/logs',      label: 'Logs' },
  { to: '/metrics',   label: 'Metrics' },
]

// Layout wraps all protected pages with a Navbar (top) + Sidebar (left).
// React Router's <Outlet /> renders the matched child route (AC4).
export default function Layout() {
  const logout   = useAuthStore((s) => s.logout)
  const user     = useAuthStore((s) => s.user)
  const navigate = useNavigate()

  function handleLogout() {
    logout()
    navigate('/login')
  }

  return (
    <div className="flex h-screen bg-background">
      {/* ── Sidebar ──────────────────────────────────────────────────────── */}
      <aside className="w-56 border-r bg-card flex flex-col shrink-0">
        <div className="px-6 py-4 border-b">
          <span className="font-bold text-lg text-primary">Fluxgate</span>
        </div>

        <nav className="flex-1 px-3 py-4 space-y-1">
          {navItems.map(({ to, label }) => (
            <NavLink
              key={to}
              to={to}
              className={({ isActive }) =>
                cn(
                  'block px-3 py-2 rounded-md text-sm font-medium transition-colors',
                  isActive
                    ? 'bg-primary text-primary-foreground'
                    : 'text-muted-foreground hover:bg-accent hover:text-accent-foreground',
                )
              }
            >
              {label}
            </NavLink>
          ))}
        </nav>

        <div className="px-6 py-3 border-t text-xs text-muted-foreground truncate">
          {user?.username}
        </div>
      </aside>

      {/* ── Main area ────────────────────────────────────────────────────── */}
      <div className="flex-1 flex flex-col overflow-hidden">
        {/* Navbar */}
        <header className="h-14 border-b bg-card flex items-center justify-between px-6 shrink-0">
          <span className="font-semibold text-sm">Admin Dashboard</span>
          <button
            onClick={handleLogout}
            className="text-sm text-muted-foreground hover:text-foreground transition-colors"
          >
            Logout
          </button>
        </header>

        {/* Page content */}
        <main className="flex-1 overflow-auto p-6">
          <Outlet />
        </main>
      </div>
    </div>
  )
}
