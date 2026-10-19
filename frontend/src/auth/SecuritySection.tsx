import { useMutation, useQueryClient } from '@tanstack/react-query'
import { useState, type FormEvent } from 'react'
import { Button } from '../components/ui/button'
import { api } from '../lib/api'
import { AUTH_STATUS_KEY } from './AuthGate'
import { useAuth } from './AuthContext'

function ChangePasswordForm() {
  const [current, setCurrent] = useState('')
  const [next, setNext] = useState('')
  const [confirm, setConfirm] = useState('')
  const [localError, setLocalError] = useState<string | null>(null)
  const change = useMutation({
    mutationFn: () => api.changePassword(current, next),
    onSuccess: () => { setCurrent(''); setNext(''); setConfirm('') },
  })

  function submit(event: FormEvent) {
    event.preventDefault()
    if (next !== confirm) {
      setLocalError('The new passwords do not match.')
      return
    }
    setLocalError(null)
    change.mutate()
  }

  const error = localError ?? (change.isError ? change.error.message : null)

  return (
    <form onSubmit={submit} className="security-block" noValidate aria-labelledby="password-heading">
      <h3 id="password-heading">Change password</h3>
      <p className="muted st-help">Other signed-in browsers are signed out when you change it.</p>
      <div className="st-fields-grid">
        <label>
          <span className="st-field-title">Current password</span>
          <input className="st-input" type="password" value={current} autoComplete="current-password"
            onChange={(e) => setCurrent(e.target.value)} />
        </label>
        <label>
          <span className="st-field-title">New password</span>
          <input className="st-input" type="password" value={next} autoComplete="new-password" minLength={8}
            onChange={(e) => setNext(e.target.value)} />
        </label>
        <label>
          <span className="st-field-title">Confirm new password</span>
          <input className="st-input" type="password" value={confirm} autoComplete="new-password"
            onChange={(e) => setConfirm(e.target.value)} />
        </label>
      </div>
      <div className="security-row">
        <Button type="submit" disabled={change.isPending || !current || !next || !confirm}>
          {change.isPending ? 'Saving…' : 'Change password'}
        </Button>
        <p className="st-status-msg" role="status" aria-live="polite">{change.isSuccess ? 'Password changed.' : ''}</p>
      </div>
      {error && <p className="st-error-alert" role="alert">{error}</p>}
    </form>
  )
}

function MetricsTokenBlock({ configured }: { configured: boolean }) {
  const queryClient = useQueryClient()
  const [token, setToken] = useState<string | null>(null)
  const refresh = () => queryClient.invalidateQueries({ queryKey: AUTH_STATUS_KEY })

  const create = useMutation({
    mutationFn: () => api.createMetricsToken(),
    onSuccess: (data) => { setToken(data.token); void refresh() },
  })
  const revoke = useMutation({
    mutationFn: () => api.revokeMetricsToken(),
    onSuccess: () => { setToken(null); void refresh() },
  })

  const error = create.isError ? create.error.message : revoke.isError ? revoke.error.message : null

  return (
    <div className="security-block" role="group" aria-labelledby="metrics-token-heading">
      <h3 id="metrics-token-heading">Prometheus token</h3>
      <p className="muted st-help">
        <code>/metrics</code> needs a bearer token when authentication is on. Send it as
        {' '}<code>Authorization: Bearer &lt;token&gt;</code>. The token is shown once; generating a new one replaces the old one.
      </p>
      {token && (
        <div>
          <p className="st-help" role="status">Copy this token now. It cannot be shown again.</p>
          <code className="security-token">{token}</code>
        </div>
      )}
      <div className="security-row">
        <Button type="button" variant="outline" disabled={create.isPending} onClick={() => create.mutate()}>
          {configured ? 'Generate a new token' : 'Generate token'}
        </Button>
        {configured && (
          <Button type="button" variant="outline" disabled={revoke.isPending} onClick={() => revoke.mutate()}>
            Revoke token
          </Button>
        )}
        <span className="muted">{configured ? 'A token is active.' : 'No token yet. Prometheus is locked out.'}</span>
      </div>
      {error && <p className="st-error-alert" role="alert">{error}</p>}
    </div>
  )
}

function DisableAuthBlock() {
  const queryClient = useQueryClient()
  const [password, setPassword] = useState('')
  const disable = useMutation({
    mutationFn: () => api.disableAuth(password),
    onSuccess: () => { setPassword(''); void queryClient.invalidateQueries({ queryKey: AUTH_STATUS_KEY }) },
  })

  function submit(event: FormEvent) {
    event.preventDefault()
    disable.mutate()
  }

  return (
    <form onSubmit={submit} className="security-block" noValidate aria-labelledby="disable-auth-heading">
      <h3 id="disable-auth-heading">Turn off authentication</h3>
      <p className="muted st-help">
        Anyone who can reach this address will be able to change settings. Only do this on a network you trust.
      </p>
      <label>
        <span className="st-field-title">Confirm with your password</span>
        <input className="st-input" type="password" value={password} autoComplete="current-password"
          onChange={(e) => setPassword(e.target.value)} />
      </label>
      <div className="security-row">
        <Button type="submit" variant="outline" disabled={disable.isPending || !password}>
          {disable.isPending ? 'Turning off…' : 'Turn off authentication'}
        </Button>
      </div>
      {disable.isError && <p className="st-error-alert" role="alert">{disable.error.message}</p>}
    </form>
  )
}

function EnableAuthBlock() {
  const queryClient = useQueryClient()
  const enable = useMutation({
    mutationFn: () => api.enableAuth(),
    onSuccess: () => { void queryClient.invalidateQueries({ queryKey: AUTH_STATUS_KEY }) },
  })

  return (
    <div className="security-block" role="group" aria-labelledby="enable-auth-heading">
      <h3 id="enable-auth-heading">Authentication is off</h3>
      <p className="muted st-help">Turn it back on to require your account again. You will be asked to sign in.</p>
      <div className="security-row">
        <Button type="button" disabled={enable.isPending} onClick={() => enable.mutate()}>
          {enable.isPending ? 'Turning on…' : 'Turn on authentication'}
        </Button>
      </div>
      {enable.isError && <p className="st-error-alert" role="alert">{enable.error.message}</p>}
    </div>
  )
}

export function SecuritySection() {
  const auth = useAuth()
  if (!auth) return null
  const { status } = auth

  return (
    <section className="panel settings-panel st-panel" aria-labelledby="security-heading">
      <div className="panel-head">
        <div>
          <p className="kicker">Access</p>
          <h2 id="security-heading">Security</h2>
        </div>
        <span className="muted">{status.authEnabled ? 'Authentication on' : 'Authentication off'}</span>
      </div>

      {status.envLocked && (
        <p className="st-lock-note" role="note">
          Authentication is controlled by the <code>WANETRA_AUTH</code> environment variable. Remove it to manage authentication here.
        </p>
      )}

      {status.authEnabled && <ChangePasswordForm />}
      {status.authEnabled && <MetricsTokenBlock configured={status.metricsTokenConfigured} />}
      {status.authEnabled && !status.envLocked && <DisableAuthBlock />}
      {!status.authEnabled && status.accountExists && !status.envLocked && <EnableAuthBlock />}
    </section>
  )
}
