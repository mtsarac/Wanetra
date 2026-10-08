import { useQuery } from '@tanstack/react-query'
import { useState } from 'react'
import { api } from './lib/api'
import { applyTheme, readTheme, themes, type ThemeId } from './lib/theme'
import ScheduleSettings from './ScheduleSettings'

export default function SettingsPage() {
  const retention = useQuery({ queryKey: ['retention-settings'], queryFn: api.getRetentionSettings })
  const [selectedTheme, setSelectedTheme] = useState(readTheme)

  return (
    <main id="main-content" className="page-content">
      <header className="page-title"><div><p className="kicker">Configuration</p><h1>Settings</h1></div><span className="muted">Application behavior</span></header>
      <section className="panel settings-panel appearance-panel">
        <div className="panel-head"><div><p className="kicker">Appearance</p><h2>Color theme</h2></div><span className="muted">Saved in this browser</span></div>
        <label htmlFor="theme-select">Theme
          <span className="theme-select-wrap"><i style={{ backgroundColor: themes.find((theme) => theme.id === selectedTheme)?.swatch }} aria-hidden="true" /><select id="theme-select" value={selectedTheme} onChange={(event) => { const theme = event.target.value as ThemeId; setSelectedTheme(theme); applyTheme(theme) }}>
            <optgroup label="Light themes">{themes.filter((theme) => theme.mode === 'light').map((theme) => <option key={theme.id} value={theme.id}>{theme.name}</option>)}</optgroup>
            <optgroup label="Dark themes">{themes.filter((theme) => theme.mode === 'dark').map((theme) => <option key={theme.id} value={theme.id}>{theme.name}</option>)}</optgroup>
          </select>
        </span>
        </label>
      </section>
      <ScheduleSettings />
      <section className="panel settings-panel retention-panel">
        <div className="panel-head"><div><p className="kicker">Data retention</p><h2>Measurement history</h2></div><span className="muted">Cleanup runs daily</span></div>
        {retention.error ? <div className="error" role="alert">{retention.error.message}</div> : <p>Keep speed-test results for <strong>{retention.data?.days ?? '…'} days</strong>. Expired measurements are removed automatically; degradation events are preserved.</p>}
        <p className="muted retention-help">Set <code>DataRetention__Days</code> in the Compose environment and restart Wanetra to change this value.</p>
      </section>
    </main>
  )
}
