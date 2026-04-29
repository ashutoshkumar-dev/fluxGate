import { create } from 'zustand'
import { persist } from 'zustand/middleware'
import authApi from '../services/authService'

// Decode roles from a JWT payload without verifying the signature.
// The gateway validates the token; the UI only needs the claims for display.
function decodeRoles(token) {
  if (!token) return []
  try {
    const payload = JSON.parse(atob(token.split('.')[1]))
    // ClaimTypes.Role maps to the long URN key in .NET JWTs
    const roleKey = 'http://schemas.microsoft.com/ws/2008/06/identity/claims/role'
    const raw = payload[roleKey] ?? payload['role'] ?? []
    return Array.isArray(raw) ? raw : [raw]
  } catch {
    return []
  }
}

// useAuthStore – persisted to localStorage under key 'fluxgate-auth'.
const useAuthStore = create(
  persist(
    (set, get) => ({
      token: null,
      user:  null,
      roles: [],

      /** POST /auth/login → stores token + user + decoded roles */
      login: async (username, password) => {
        const resp = await authApi.post('/auth/login', { username, password })
        const { token, username: name } = resp.data
        const roles = decodeRoles(token)
        set({ token, user: { username: name }, roles })
        return resp.data
      },

      logout: () => set({ token: null, user: null, roles: [] }),

      isAuthenticated: () => !!get().token,

      /** Returns true when the current user has the admin role */
      isAdmin: () => get().roles.includes('admin'),
    }),
    {
      name: 'fluxgate-auth',
      partialize: (state) => ({ token: state.token, user: state.user, roles: state.roles }),
    },
  ),
)

export default useAuthStore
