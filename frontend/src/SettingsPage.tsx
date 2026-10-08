import { useQuery } from '@tanstack/react-query'
import { api } from './lib/api'
import ScheduleSettings from './ScheduleSettings'
import { AppearanceSection } from './settings/AppearanceSection'
import { RetentionSection } from './settings/RetentionSection'
import { SpeedTestSection } from './settings/SpeedTestSection'
import './settings/settings.css'

export default function SettingsPage() {
  const settingsQuery = useQuery({
    queryKey: ['settings'],
    queryFn: () => api.getSettings(),
  })

  return (
    <main id="main-content" className="page-content">
      <header className="page-title">
        <div>
          <p className="kicker">Configuration</p>
          <h1>Settings</h1>
        </div>
        <span className="muted">Application behavior</span>
      </header>

      {settingsQuery.isError && (
        <section className="panel settings-panel st-section-error" aria-label="Settings load error">
          <div className="error" role="alert">
            Failed to load settings: {settingsQuery.error.message}
          </div>
        </section>
      )}

      {settingsQuery.isLoading && (
        <section className="panel settings-panel" aria-label="Loading settings">
          <p className="muted">Loading settings…</p>
        </section>
      )}

      {settingsQuery.data?.settings && (
        <>
          <SpeedTestSection settings={settingsQuery.data.settings} />
          <ScheduleSettings />
          <RetentionSection settings={settingsQuery.data.settings} />
        </>
      )}

      {!settingsQuery.data?.settings && !settingsQuery.isLoading && (
        <ScheduleSettings />
      )}

      <AppearanceSection />
    </main>
  )
}
