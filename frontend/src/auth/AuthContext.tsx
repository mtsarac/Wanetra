import { createContext, useContext } from 'react'
import type { AuthStatus } from '../lib/api'

export type AuthContextValue = {
  status: AuthStatus
  signOut: () => void
}

export const AuthContext = createContext<AuthContextValue | null>(null)

export function useAuth() {
  return useContext(AuthContext)
}
