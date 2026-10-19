import { useMutation, useQueryClient } from '@tanstack/react-query'
import { useState, type FormEvent } from 'react'
import { Button } from '../components/ui/button'
import { api } from '../lib/api'

export function LoginPage() {
  const queryClient = useQueryClient()
  const [username, setUsername] = useState('')
  const [password, setPassword] = useState('')

  const login = useMutation({
    mutationFn: () => api.login(username, password),
    onSuccess: (status) => {
      queryClient.setQueryData(['auth', 'status'], status)
    },
  })

  function submit(event: FormEvent) {
    event.preventDefault()
    login.mutate()
  }

  return (
    <main className="auth-screen">
      <section className="panel auth-panel" aria-labelledby="login-heading">
        <p className="kicker">Wanetra</p>
        <h1 id="login-heading">Sign in</h1>
        <form onSubmit={submit} className="auth-form" noValidate>
          <label>
            <span className="st-field-title">Username</span>
            <input className="st-input" value={username} onChange={(e) => setUsername(e.target.value)}
              autoComplete="username" autoFocus required />
          </label>
          <label>
            <span className="st-field-title">Password</span>
            <input className="st-input" type="password" value={password} onChange={(e) => setPassword(e.target.value)}
              autoComplete="current-password" required />
          </label>
          {login.isError && <p className="st-error-alert" role="alert">{login.error.message}</p>}
          <Button type="submit" disabled={login.isPending || !username || !password}>
            {login.isPending ? 'Signing in…' : 'Sign in'}
          </Button>
        </form>
      </section>
    </main>
  )
}
