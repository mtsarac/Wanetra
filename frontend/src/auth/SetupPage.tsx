import { useMutation, useQueryClient } from '@tanstack/react-query'
import { useState, type FormEvent } from 'react'
import { Button } from '../components/ui/button'
import { api } from '../lib/api'

export function SetupPage() {
  const queryClient = useQueryClient()
  const [setupCode, setSetupCode] = useState('')
  const [username, setUsername] = useState('admin')
  const [password, setPassword] = useState('')
  const [confirm, setConfirm] = useState('')
  const [localError, setLocalError] = useState<string | null>(null)

  const setup = useMutation({
    mutationFn: () => api.setupAuth(setupCode, username, password),
    onSuccess: (status) => {
      queryClient.clear()
      queryClient.setQueryData(['auth', 'status'], status)
    },
  })

  function submit(event: FormEvent) {
    event.preventDefault()
    if (password !== confirm) {
      setLocalError('The passwords do not match.')
      return
    }
    setLocalError(null)
    setup.mutate()
  }

  const error = localError ?? (setup.isError ? setup.error.message : null)

  return (
    <main className="auth-screen">
      <section className="panel auth-panel" aria-labelledby="setup-heading">
        <p className="kicker">Welcome to Wanetra</p>
        <h1 id="setup-heading">Create your account</h1>
        <p className="muted auth-intro">
          Wanetra printed a setup code in its log when it started. Find it with <code>docker compose logs wanetra</code> and enter it below.
          You can turn authentication off later under Settings.
        </p>
        <form onSubmit={submit} className="auth-form" noValidate>
          <label>
            <span className="st-field-title">Setup code</span>
            <input className="st-input" value={setupCode} onChange={(e) => setSetupCode(e.target.value)}
              autoComplete="off" autoCapitalize="characters" spellCheck={false} placeholder="XXXX-XXXX-XXXX" required />
          </label>
          <label>
            <span className="st-field-title">Username</span>
            <input className="st-input" value={username} onChange={(e) => setUsername(e.target.value)}
              autoComplete="username" required />
          </label>
          <label>
            <span className="st-field-title">Password</span>
            <input className="st-input" type="password" value={password} onChange={(e) => setPassword(e.target.value)}
              autoComplete="new-password" minLength={8} required aria-describedby="setup-password-help" />
            <small id="setup-password-help" className="st-help">At least 8 characters.</small>
          </label>
          <label>
            <span className="st-field-title">Confirm password</span>
            <input className="st-input" type="password" value={confirm} onChange={(e) => setConfirm(e.target.value)}
              autoComplete="new-password" required />
          </label>
          {error && <p className="st-error-alert" role="alert">{error}</p>}
          <Button type="submit" disabled={setup.isPending || !setupCode || !username || !password}>
            {setup.isPending ? 'Creating…' : 'Create account'}
          </Button>
        </form>
      </section>
    </main>
  )
}
