import { Link, useLocation } from 'react-router-dom'
import { useMsal } from '@azure/msal-react'
import { useFetch } from '../hooks'

const API_BASE_URL = import.meta.env.VITE_API_URL || 'http://localhost:7071/api'
const AUTH_ENABLED = import.meta.env.VITE_AUTH_ENABLED === 'true'

function SignOutButton() {
  const { instance } = useMsal()
  return (
    <button
      onClick={() => instance.logoutRedirect()}
      className="w-full text-left flex items-center gap-2.5 px-3 py-2 rounded-lg text-sm text-gray-400 hover:text-white hover:bg-white/5 transition-colors"
    >
      Sign out
    </button>
  )
}

const NAV = [
  { to: '/',         label: 'Overview',  exact: true },
  { to: '/settings', label: 'Settings',  exact: false },
]

export default function Sidebar() {
  const location = useLocation()
  const healthR  = useFetch(`${API_BASE_URL}/health`, 60_000)
  const ok       = healthR.data?.status === 'Healthy'

  return (
    <aside className="w-56 shrink-0 bg-gray-900 text-white flex flex-col h-screen sticky top-0 overflow-y-auto">
      {/* Brand */}
      <div className="px-5 pt-6 pb-5 border-b border-white/10">
        <div className="flex items-center gap-3">
          <div className="w-8 h-8 bg-blue-500 rounded-lg flex items-center justify-center shrink-0">
            <svg viewBox="0 0 20 20" fill="white" className="w-4 h-4">
              <rect x="2" y="2" width="6" height="6" rx="1"/>
              <rect x="12" y="2" width="6" height="6" rx="1"/>
              <rect x="2" y="12" width="6" height="6" rx="1"/>
              <rect x="12" y="12" width="6" height="3" rx="1"/>
              <rect x="15" y="17" width="3" height="1" rx="0.5"/>
            </svg>
          </div>
          <div>
            <p className="font-bold text-sm leading-tight">QBIS</p>
            <p className="text-[10px] text-gray-400 leading-tight mt-0.5">Queue Intelligence</p>
          </div>
        </div>
      </div>

      {/* Nav */}
      <nav className="flex-1 px-3 pt-4 space-y-0.5">
        {NAV.map(({ to, label, exact }) => {
          const active = exact
            ? location.pathname === to
            : location.pathname === to || location.pathname.startsWith(to + '/')
          return (
            <Link
              key={to}
              to={to}
              className={`flex items-center gap-2.5 px-3 py-2 rounded-lg text-sm transition-colors ${
                active
                  ? 'bg-white/10 text-white font-medium'
                  : 'text-gray-400 hover:text-white hover:bg-white/5'
              }`}
            >
              {label}
            </Link>
          )
        })}
      </nav>

      {/* Sign out */}
      {AUTH_ENABLED && (
        <div className="px-3 pb-2">
          <SignOutButton />
        </div>
      )}

      {/* Backend health */}
      <div className="px-5 py-4 border-t border-white/10">
        <div className="flex items-center gap-2 text-[11px]">
          <span className={`w-2 h-2 rounded-full shrink-0 ${
            !healthR.data && !healthR.error ? 'bg-gray-500'
            : ok           ? 'bg-green-400'
            : healthR.error ? 'bg-gray-500'
            : 'bg-red-400'
          }`} />
          <span className="text-gray-400">
            {!healthR.data && !healthR.error ? 'Checking…'
              : ok ? 'Backend Healthy'
              : healthR.error ? 'Checking…'
              : 'Backend Degraded'}
          </span>
        </div>
      </div>
    </aside>
  )
}
