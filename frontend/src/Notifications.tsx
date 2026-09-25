import { useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { api } from './lib/api'

type NtfyDraft = {
  serverUrl: string
  topic: string
  username: string
  password: string
  token: string
  priority: string
  tags: string
}

type WebhookDraft = {
  url: string
  method: string
  headersJson: string
}

const emptyNtfy: NtfyDraft = {
  serverUrl: '', topic: '', username: '', password: '', token: '', priority: '', tags: '',
}

const emptyWebhook: WebhookDraft = { url: '', method: '', headersJson: '' }

function ntfyJson(draft: NtfyDraft): string {
  const trimmedTags = draft.tags.trim() === ''
    ? undefined
    : draft.tags.split(',').map((tag) => tag.trim()).filter(Boolean)
  return JSON.stringify({
    serverUrl: draft.serverUrl.trim(),
    topic: draft.topic.trim(),
    username: draft.username.trim() === '' ? undefined : draft.username.trim(),
    password: draft.password.trim() === '' ? undefined : draft.password.trim(),
    token: draft.token.trim() === '' ? undefined : draft.token.trim(),
    priority: draft.priority.trim() === '' ? undefined : draft.priority.trim(),
    tags: trimmedTags,
  })
}

function webhookJson(draft: WebhookDraft): string {
  const trimmed = draft.headersJson.trim()
  return JSON.stringify({
    url: draft.url.trim(),
    method: draft.method,
    headers: trimmed === '' ? undefined : JSON.parse(trimmed) as Record<string, string>,
  })
}

function draftHasContent(json: string, required: string[]): boolean {
  try {
    const parsed = JSON.parse(json) as Record<string, unknown>
    return required.every((key) => typeof parsed[key] === 'string' && (parsed[key] as string).trim() !== '')
  } catch {
    return false
  }
}

export default function NotificationsPanel() {
  const client = useQueryClient()
  const stored = useQuery({ queryKey: ['notifications'], queryFn: api.getNotifications })
  const [ntfy, setNtfy] = useState<NtfyDraft>(emptyNtfy)
  const [webhook, setWebhook] = useState<WebhookDraft>(emptyWebhook)
  const [ntfyOverride, setNtfyOverride] = useState<boolean | null>(null)
  const [webhookOverride, setWebhookOverride] = useState<boolean | null>(null)
  const [message, setMessage] = useState<string | null>(null)

  const ntfyStored = stored.data?.configurations.find((item) => item.provider === 'ntfy')
  const webhookStored = stored.data?.configurations.find((item) => item.provider === 'webhook')
  const ntfyEnabled = ntfyOverride ?? ntfyStored?.enabled ?? false
  const webhookEnabled = webhookOverride ?? webhookStored?.enabled ?? false

  const save = useMutation({
    mutationFn: () => api.saveNotifications([
      { provider: 'ntfy', enabled: ntfyEnabled, configurationJson: ntfyJson(ntfy) },
      { provider: 'webhook', enabled: webhookEnabled, configurationJson: webhookJson(webhook) },
    ]),
    onSuccess: (data) => {
      client.setQueryData(['notifications'], data)
      setNtfy(emptyNtfy)
      setWebhook(emptyWebhook)
      setNtfyOverride(null)
      setWebhookOverride(null)
      setMessage('Saved. Secrets stay stored; blank fields kept existing values.')
    },
    onError: (error) => setMessage(error instanceof Error ? error.message : 'Save failed.'),
  })

  const test = useMutation({
    mutationFn: (target: { provider: string; draft: string; required: string[] }) => {
      if (draftHasContent(target.draft, target.required)) {
        return api.testNotification({ provider: target.provider, configurationJson: target.draft })
      }
      const id = stored.data?.configurations.find((item) => item.provider === target.provider)?.id
      if (id == null) throw new Error(`No saved ${target.provider} configuration to test. Fill the form first.`)
      return api.testNotification({ id })
    },
    onSuccess: () => setMessage('Test notification sent.'),
    onError: (error) => setMessage(error instanceof Error ? error.message : 'Test failed.'),
  })

  return (
    <main id="main-content" className="page-content">
      <header className="page-title"><div><p className="kicker">Delivery channels</p><h1>Notifications</h1></div><span className="muted">Choose where incident updates go</span></header>
      <section className="panel notifications">
      <div className="panel-head">
        <div><p className="kicker">Incident delivery</p><h2>Degradation and recovery alerts</h2></div>
        <span className="muted">Blank fields keep stored secrets</span>
      </div>
      {message && <div className="notice" role="status">{message}</div>}
      <div className="notify-grid">
        <div className="notify-card">
          <label className="check"><input type="checkbox" checked={ntfyEnabled} onChange={(e) => setNtfyOverride(e.target.checked)} /> ntfy {ntfyStored?.hasConfiguration && <small>· configured</small>}</label>
          <label>Server URL<input value={ntfy.serverUrl} onChange={(e) => setNtfy((prev) => ({ ...prev, serverUrl: e.target.value }))} placeholder={ntfyStored?.serverUrl ?? 'https://ntfy.sh'} inputMode="url" /></label>
          <label>Topic<input value={ntfy.topic} onChange={(e) => setNtfy((prev) => ({ ...prev, topic: e.target.value }))} placeholder={ntfyStored?.topic ?? 'wanetra'} /></label>
          <label>Token (optional)<input type="password" value={ntfy.token} onChange={(e) => setNtfy((prev) => ({ ...prev, token: e.target.value }))} placeholder={ntfyStored?.hasCredentials ? '(stored)' : 'Bearer token'} autoComplete="off" /></label>
          <div className="row">
            <label>Username<input value={ntfy.username} onChange={(e) => setNtfy((prev) => ({ ...prev, username: e.target.value }))} placeholder={ntfyStored?.hasCredentials ? '(stored)' : ''} autoComplete="off" /></label>
            <label>Password<input type="password" value={ntfy.password} onChange={(e) => setNtfy((prev) => ({ ...prev, password: e.target.value }))} placeholder={ntfyStored?.hasCredentials ? '(stored)' : 'Basic auth'} autoComplete="off" /></label>
          </div>
          <div className="row">
            <label>Priority<input value={ntfy.priority} onChange={(e) => setNtfy((prev) => ({ ...prev, priority: e.target.value }))} placeholder={ntfyStored?.priority ?? 'default'} /></label>
            <label>Tags (comma)<input value={ntfy.tags} onChange={(e) => setNtfy((prev) => ({ ...prev, tags: e.target.value }))} placeholder={ntfyStored?.tags ?? 'warning'} /></label>
          </div>
          <button disabled={test.isPending} onClick={() => test.mutate({ provider: 'ntfy', draft: ntfyJson(ntfy), required: ['serverUrl', 'topic'] })}>Send test</button>
        </div>
        <div className="notify-card">
          <label className="check"><input type="checkbox" checked={webhookEnabled} onChange={(e) => setWebhookOverride(e.target.checked)} /> Webhook {webhookStored?.hasConfiguration && <small>· configured</small>}</label>
          <label>URL<input value={webhook.url} onChange={(e) => setWebhook((prev) => ({ ...prev, url: e.target.value }))} placeholder={webhookStored?.hasUrl ? '(stored)' : 'https://example.com/hook'} inputMode="url" /></label>
          <label>Method
            <select value={webhook.method || webhookStored?.method || 'POST'} onChange={(e) => setWebhook((prev) => ({ ...prev, method: e.target.value }))}>
              <option value="">(stored/default)</option><option>GET</option><option>POST</option><option>PUT</option>
            </select>
          </label>
          <label>Headers JSON (optional)<textarea value={webhook.headersJson} onChange={(e) => setWebhook((prev) => ({ ...prev, headersJson: e.target.value }))} placeholder={webhookStored?.hasHeaders ? '(stored; blank keeps existing headers)' : '{"X-Token": "..."}'} rows={3} spellCheck={false} /></label>
          <button disabled={test.isPending} onClick={() => test.mutate({ provider: 'webhook', draft: webhookJson(webhook), required: ['url'] })}>Send test</button>
        </div>
      </div>
      <button className="save" disabled={save.isPending} onClick={() => { save.mutate() }}>{save.isPending ? 'Saving…' : 'Save notifications'}</button>
      </section>
    </main>
  )
}
