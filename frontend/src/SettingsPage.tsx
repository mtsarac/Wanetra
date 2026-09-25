import { useQuery } from '@tanstack/react-query'
import { api } from './lib/api'
import ScheduleSettings from './ScheduleSettings'

export default function SettingsPage() {
  const retention = useQuery({ queryKey: ['retention-settings'], queryFn: api.getRetentionSettings })

  return (
    <main className="page-content">
      <header className="page-title"><div><p className="kicker">WANETRA / CONFIGURATION</p><h1>Settings</h1></div><span className="muted">Application behavior</span></header>
      <ScheduleSettings />
      <section className="panel settings-panel retention-panel">
        <div className="panel-head"><div><p className="kicker">SETTINGS / DATA RETENTION</p><h2>Measurement history</h2></div><span className="muted">Cleanup runs daily</span></div>
        {retention.error ? <div className="error" role="alert">{retention.error.message}</div> : <p>Keep speed-test results for <strong>{retention.data?.days ?? '…'} days</strong>. Expired measurements are removed automatically; degradation events are preserved.</p>}
        <p className="muted retention-help">Set <code>DataRetention__Days</code> in the Compose environment and restart Wanetra to change this value.</p>
      </section>
    </main>
  )
}
