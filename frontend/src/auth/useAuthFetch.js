import { useCallback } from 'react'
import { useMsal } from '@azure/msal-react'
import { InteractionRequiredAuthError } from '@azure/msal-browser'
import { loginRequest } from './msalConfig'

const AUTH_ENABLED = import.meta.env.VITE_AUTH_ENABLED === 'true'

export function useAuthFetch() {
  const { instance, accounts } = useMsal()

  const getAuthHeaders = useCallback(async () => {
    if (!AUTH_ENABLED) return {}

    const account = accounts[0]
    if (!account) return {}

    try {
      const result = await instance.acquireTokenSilent({
        ...loginRequest,
        account,
      })
      return { Authorization: `Bearer ${result.accessToken}` }
    } catch (e) {
      if (e instanceof InteractionRequiredAuthError) {
        // Silent refresh failed (e.g. expired consent) — fall back to popup
        try {
          const result = await instance.acquireTokenPopup(loginRequest)
          return { Authorization: `Bearer ${result.accessToken}` }
        } catch {
          return {}
        }
      }
      return {}
    }
  }, [instance, accounts])

  return { getAuthHeaders }
}
