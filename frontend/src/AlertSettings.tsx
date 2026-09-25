import { useState, type FormEvent } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { api, type AlertRule, type AlertRuleUpdate } from './lib/api'

const defaults: AlertRuleUpdate = {
  name: 'WAN health',
  enabled: true,
  minDownloadMbps: null,
  minUploadMbps: null,
  maxLatencyMs: null,
  maxJitterMs: null,
  maxPacketLossPercent: null,
  downloadBaselineDropPercent: 30,
  uploadBaselineDropPercent: null,
  consecutiveFailuresRequired: 3,
  consecutiveRecoveriesRequired: 2,
}

type NumericThreshold =
  | 'minDownloadMbps'
  | 'minUploadMbps'
  | 'maxLatencyMs'
  | 'maxJitterMs'
  | 'maxPacketLossPercent'
  | 'downloadBaselineDropPercent'
  | 'uploadBaselineDropPercent'

const thresholds: { key: NumericThreshold; label: string; unit: string; percent?: boolean; note?: string }[] = [
  { key: 'minDownloadMbps', label: 'Minimum download', unit: 'Mbps' },
  { key: 'minUploadMbps', label: 'Minimum upload', unit: 'Mbps' },
  { key: 'maxLatencyMs', label: 'Maximum latency', unit: 'ms' },
  { key: 'maxJitterMs', label: 'Maximum jitter', unit: 'ms' },
  { key: 'maxPacketLossPercent', label: 'Maximum packet loss', unit: '%', note: 'Unavailable with LibreSpeed; this threshold is not applied.' },
  { key: 'downloadBaselineDropPercent', label: 'Download drop from baseline', unit: '%', percent: true },
  { key: 'uploadBaselineDropPercent', label: 'Upload drop from baseline', unit: '%', percent: true },
]

function toDraft(rule: AlertRule): AlertRuleUpdate {
  return {
    name: rule.name,
    enabled: rule.enabled,
    minDownloadMbps: rule.minDownloadMbps,
    minUploadMbps: rule.minUploadMbps,
    maxLatencyMs: rule.maxLatencyMs,
    maxJitterMs: rule.maxJitterMs,
    maxPacketLossPercent: rule.maxPacketLossPercent,
    downloadBaselineDropPercent: rule.downloadBaselineDropPercent,
    uploadBaselineDropPercent: rule.uploadBaselineDropPercent,
    consecutiveFailuresRequired: rule.consecutiveFailuresRequired,
    consecutiveRecoveriesRequired: rule.consecutiveRecoveriesRequired,
  }
}

function formatTime(value: string | null) {
  return value ? new Date(value).toLocaleString([], { dateStyle: 'medium', timeStyle: 'short' }) : '—'
}

export default function AlertSettings() {
  const client = useQueryClient()
  const rule = useQuery({ queryKey: ['alert-rule'], queryFn: api.getAlertRule })
  const events = useQuery({ queryKey: ['degradation-events'], queryFn: () => api.getDegradationEvents() })
  const [draft, setDraft] = useState<AlertRuleUpdate | null>(null)
  const [message, setMessage] = useState<string | null>(null)
  const values = draft ?? (rule.data ? toDraft(rule.data) : defaults)


  const save = useMutation({
    mutationFn: api.saveAlertRule,
    onSuccess: async (saved) => {
      setDraft(toDraft(saved))
      setMessage('Alert rule saved.')
      await client.invalidateQueries({ queryKey: ['alert-rule'] })
    },
    onError: (error: Error) => setMessage(error.message),
  })

  function setThreshold(key: NumericThreshold, raw: string) {
    setDraft({ ...values, [key]: raw === '' ? null : Number(raw) })
  }

  function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    setMessage(null)
    save.mutate(values)
  }

  return (
    <main className="page-content">
      <header className="page-title"><div><p className="kicker">WANETRA / CONNECTION HEALTH</p><h1>Alerts</h1></div><span className="muted">Thresholds and incident history</span></header>
      <section className="panel settings-panel">
        <div className="panel-head"><div><p className="kicker">ALERTS / THRESHOLDS</p><h2>Degradation rule</h2></div><span className="muted">Supported conditions can trigger an incident</span></div>
        {rule.isLoading ? <div className="empty">Loading alert rule…</div> : (
          <form onSubmit={submit}>
            <label className="setting-check"><input type="checkbox" checked={values.enabled} onChange={(event) => setDraft({ ...values, enabled: event.target.checked })} /> Enable alert evaluation</label>
            <div className="settings-grid">
              <label><span>Rule name</span><input value={values.name} onChange={(event) => setDraft({ ...values, name: event.target.value })} required /></label>
              {thresholds.map(({ key, label, unit, percent, note }) => (
                <label key={key}>
                  <span>{label} <small>{unit}</small></span>
                  <input
                    type="number"
                    min="0"
                    max={percent ? 100 : undefined}
                    step="any"
                    value={values[key] ?? ''}
                    onChange={(event) => setThreshold(key, event.target.value)}
                    placeholder="Disabled"
                    disabled={note !== undefined}
                  />
                  {note && <small className="muted">{note}</small>}
                </label>
              ))}
              <label><span>Consecutive unhealthy tests</span><input type="number" min="1" step="1" value={values.consecutiveFailuresRequired} onChange={(event) => setDraft({ ...values, consecutiveFailuresRequired: Number(event.target.value) })} required /></label>
              <label><span>Consecutive recovery tests</span><input type="number" min="1" step="1" value={values.consecutiveRecoveriesRequired} onChange={(event) => setDraft({ ...values, consecutiveRecoveriesRequired: Number(event.target.value) })} required /></label>
            </div>
            <div className="form-actions"><button type="submit" disabled={save.isPending || rule.isLoading}>{save.isPending ? 'Saving…' : 'Save alert rule'}</button>{message && <span className={save.isError ? 'failure' : 'success'} role="status">{message}</span>}</div>
          </form>
        )}
        {rule.error && <div className="error" role="alert">{rule.error.message}</div>}
      </section>
      <section className="panel event-history">
        <div className="panel-head"><div><p className="kicker">INCIDENTS / LAST 20</p><h2>Degradation events</h2></div><span className="muted">Persisted event history</span></div>
        {events.error && <div className="error" role="alert">{events.error.message}</div>}
        {events.isLoading ? <div className="empty">Loading incidents…</div> : events.data?.length ? (
          <div className="event-list">
            {events.data.map((incident) => (
              <article className="event-card" key={incident.id}>
                <div className="event-heading"><div><strong>{incident.reason}</strong><span>Started {formatTime(incident.startedAt)}{incident.endedAt ? ` · ended ${formatTime(incident.endedAt)}` : ''}</span></div><span className={`event-status ${incident.status}`}>{incident.status}</span></div>
                <div className="event-metrics">
                  <span>Download {incident.worstDownloadMbps == null ? '—' : `${incident.worstDownloadMbps.toFixed(1)} Mbps`}</span>
                  <span>Upload {incident.worstUploadMbps == null ? '—' : `${incident.worstUploadMbps.toFixed(1)} Mbps`}</span>
                  <span>Latency {incident.maxLatencyMs == null ? '—' : `${incident.maxLatencyMs.toFixed(1)} ms`}</span>
                </div>
                <div className="event-notifications">Degradation notification: {incident.notificationSent ? 'sent' : 'not sent'} · Recovery notification: {incident.recoveryNotificationSent ? 'sent' : 'not sent'}</div>
              </article>
            ))}
          </div>
        ) : <div className="empty">No degradation events have been recorded.</div>}
      </section>
    </main>
  )
}
