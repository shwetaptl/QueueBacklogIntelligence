import { Routes, Route, Link, useLocation } from 'react-router-dom'
import { ToastProvider } from './context/ToastContext'
import Dashboard         from './pages/Dashboard'
import Settings          from './pages/Settings'
import { useFetch }      from './hooks'

const API_BASE_URL = import.meta.env.VITE_API_URL || 'http://localhost:7071/api'

// ── NavBar — uses useLocation to highlight the active link ────────────────
function NavBar() {
  const location = useLocation()
  const healthR  = useFetch(`${API_BASE_URL}/health`, 60_000)
  const ok       = healthR.data?.status === 'Healthy'

  function navLink(to, label) {
    const active = location.pathname === to
    return (
      <Link
        to={to}
        className={`text-sm font-medium px-3 py-1.5 rounded-lg transition-colors ${
          active
            ? 'bg-gray-900 text-white'
            : 'text-gray-500 hover:text-gray-900 hover:bg-gray-100'
        }`}
      >
        {label}
      </Link>
    )
  }

  return (
    <header className="bg-white border-b border-gray-200 px-6 py-3 flex items-center justify-between">
      {/* Brand */}
      <span className="text-sm font-bold text-gray-800 tracking-tight">
        QBIS — Queue Backlog Intelligence
      </span>

      <div className="flex items-center gap-4">
        {/* Navigation links */}
        <nav className="flex gap-1">
          {navLink('/',         'Dashboard')}
          {navLink('/settings', 'Settings')}
        </nav>

        {/* Health indicator dot */}
        <div className="flex items-center gap-1.5 text-xs">
          <span className={`w-2 h-2 rounded-full ${
            healthR.error ? 'bg-gray-300'
            : ok          ? 'bg-green-500'
            :               'bg-red-500'
          }`} />
          <span className={
            healthR.error ? 'text-gray-400'
            : ok          ? 'text-green-700'
            :               'text-red-600'
          }>
            {!healthR.data && !healthR.error
              ? 'Checking…'
              : ok ? 'Healthy' : healthR.error ? 'Checking…' : 'Degraded'}
          </span>
        </div>
      </div>
    </header>
  )
}

// ── App — root component: wraps everything in ToastProvider + Router ───────
export default function App() {
  return (
    <ToastProvider>
      <div className="min-h-screen bg-gray-50 font-sans">
        <NavBar />
        <div className="max-w-5xl mx-auto px-4 py-6">
          <Routes>
            <Route path="/"         element={<Dashboard />} />
            <Route path="/settings" element={<Settings />} />
          </Routes>
        </div>
      </div>
    </ToastProvider>
  )
}
