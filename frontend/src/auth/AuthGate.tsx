import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useEffect, type ReactNode } from 'react'
import { api, UNAUTHORIZED_EVENT } from '../lib/api'
import { AuthContext } from './AuthContext'
import { LoginPage } from './LoginPage'
import { SetupPage } from './SetupPage'
import './auth.css'

export const AUTH_STATUS_KEY = ['auth', 'status']

export default function AuthGate({ children }: { children: ReactNode }) {
  const queryClient = useQueryClient()
  const statusQuery = useQuery({
    queryKey: AUTH_STATUS_KEY,
    queryFn: () => api.getAuthStatus(),
    staleTime: 60_000,
    retry: 1,
  })

  useEffect(() => {
    const refresh = () => { void queryClient.invalidateQueries({ queryKey: AUTH_STATUS_KEY }) }
    window.addEventListener(UNAUTHORIZED_EVENT, refresh)
    return () => window.removeEventListener(UNAUTHORIZED_EVENT, refresh)
  }, [queryClient])

  const logout = useMutation({
    mutationFn: () => api.logout(),
    onSettled: () => {
      queryClient.clear()
      void queryClient.invalidateQueries({ queryKey: AUTH_STATUS_KEY })
    },
  })

  if (statusQuery.isLoading) {
    return (
      <main className="auth-screen" aria-busy="true">
        <p className="muted" role="status">Loading…</p>
      </main>
    )
  }

  if (statusQuery.isError || !statusQuery.data) {
    return (
      <main className="auth-screen">
        <section className="panel auth-panel">
          <h1>Wanetra</h1>
          <p className="error" role="alert">Could not reach the server: {statusQuery.error?.message}</p>
          <button type="button" className="auth-link" onClick={() => void statusQuery.refetch()}>Try again</button>
        </section>
      </main>
    )
  }

  const status = statusQuery.data
  if (status.setupRequired) return <SetupPage />
  if (!status.authenticated) return <LoginPage />

  return (
    <AuthContext.Provider value={{ status, signOut: () => logout.mutate() }}>
      {children}
    </AuthContext.Provider>
  )
}
