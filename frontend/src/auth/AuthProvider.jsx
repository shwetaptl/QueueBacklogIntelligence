import { useState } from 'react'
import { MsalProvider, AuthenticatedTemplate, UnauthenticatedTemplate, useMsal } from '@azure/msal-react'
import { msalInstance, loginRequest } from './msalConfig'

const AUTH_ENABLED = import.meta.env.VITE_AUTH_ENABLED === 'true'

function LoginGate({ children }) {
  const { instance, inProgress } = useMsal()
  const [error, setError] = useState(null)
  const [loading, setLoading] = useState(false)

  async function handleLogin() {
    try {
      setError(null)
      setLoading(true)
      await instance.loginRedirect(loginRequest)
    } catch (e) {
      setError(e.message ?? 'Login failed')
      setLoading(false)
    }
  }

  return (
    <>
      <AuthenticatedTemplate>
        {children}
      </AuthenticatedTemplate>

      <UnauthenticatedTemplate>
        {/* inProgress !== 'none' means a redirect is in flight (login or logout).
            Suppress the login page during that window to prevent a visible flash. */}
        {inProgress === 'none' && <div className="min-h-screen bg-gray-900 flex items-center justify-center">
          <div className="bg-white rounded-2xl shadow-2xl p-10 w-full max-w-sm text-center">
            <div className="w-14 h-14 bg-blue-500 rounded-xl flex items-center justify-center mx-auto mb-5">
              <svg viewBox="0 0 20 20" fill="white" className="w-7 h-7">
                <rect x="2" y="2" width="6" height="6" rx="1"/>
                <rect x="12" y="2" width="6" height="6" rx="1"/>
                <rect x="2" y="12" width="6" height="6" rx="1"/>
                <rect x="12" y="12" width="6" height="3" rx="1"/>
                <rect x="15" y="17" width="3" height="1" rx="0.5"/>
              </svg>
            </div>
            <h1 className="text-xl font-bold text-gray-900 mb-1">QBIS</h1>
            <p className="text-sm text-gray-500 mb-8">Queue Backlog Intelligence System</p>
            <button
              onClick={handleLogin}
              disabled={loading}
              className="w-full flex items-center justify-center gap-3 px-4 py-3 bg-blue-600
                text-white text-sm font-semibold rounded-xl hover:bg-blue-700
                disabled:opacity-60 disabled:cursor-not-allowed transition-colors"
            >
              <svg viewBox="0 0 21 21" className="w-5 h-5 shrink-0">
                <rect x="1"  y="1"  width="9" height="9" fill="#f25022"/>
                <rect x="11" y="1"  width="9" height="9" fill="#7fba00"/>
                <rect x="1"  y="11" width="9" height="9" fill="#00a4ef"/>
                <rect x="11" y="11" width="9" height="9" fill="#ffb900"/>
              </svg>
              {loading ? 'Signing in…' : 'Sign in with Microsoft'}
            </button>

            {error && (
              <div className="mt-4 p-3 bg-red-50 border border-red-200 rounded-lg text-left">
                <p className="text-xs font-semibold text-red-700 mb-1">Login failed</p>
                <p className="text-xs text-red-600 break-all">{error}</p>
              </div>
            )}

            <p className="text-xs text-gray-400 mt-4">
              Sign in with your organisation account to continue.
            </p>
          </div>
        </div>}
      </UnauthenticatedTemplate>
    </>
  )
}

export default function AuthProvider({ children }) {
  // Local dev bypass — skip MSAL entirely when auth is disabled
  if (!AUTH_ENABLED) return children

  return (
    <MsalProvider instance={msalInstance}>
      <LoginGate>{children}</LoginGate>
    </MsalProvider>
  )
}
