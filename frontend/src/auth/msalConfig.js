import { PublicClientApplication } from '@azure/msal-browser'

export const msalConfig = {
  auth: {
    clientId:    import.meta.env.VITE_AZURE_CLIENT_ID  ?? '',
    authority:   `https://login.microsoftonline.com/${import.meta.env.VITE_AZURE_TENANT_ID ?? 'common'}`,
    redirectUri: window.location.origin,
  },
  cache: {
    cacheLocation: 'sessionStorage',
    storeAuthStateInCookie: false,
  },
}

// Scopes to request when acquiring an access token for the backend API
export const loginRequest = {
  scopes: [import.meta.env.VITE_AZURE_API_SCOPE ?? ''],
}

export const msalInstance = new PublicClientApplication(msalConfig)
