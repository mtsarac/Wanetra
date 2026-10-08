import { useState } from 'react'
import { applyTheme, readTheme, themes, type ThemeId } from '../lib/theme'

export function AppearanceSection() {
  const [selectedTheme, setSelectedTheme] = useState(readTheme)

  return (
    <section className="panel settings-panel appearance-panel" aria-labelledby="theme-heading">
      <div className="panel-head">
        <div>
          <p className="kicker">Appearance</p>
          <h2 id="theme-heading">Color theme</h2>
        </div>
        <span className="muted">Saved in this browser</span>
      </div>
      <label htmlFor="theme-select">
        Theme
        <span className="theme-select-wrap">
          <i
            style={{ backgroundColor: themes.find((t) => t.id === selectedTheme)?.swatch }}
            aria-hidden="true"
          />
          <select
            id="theme-select"
            value={selectedTheme}
            onChange={(e) => {
              const theme = e.target.value as ThemeId
              setSelectedTheme(theme)
              applyTheme(theme)
            }}
          >
            <optgroup label="Light themes">
              {themes
                .filter((t) => t.mode === 'light')
                .map((t) => (
                  <option key={t.id} value={t.id}>
                    {t.name}
                  </option>
                ))}
            </optgroup>
            <optgroup label="Dark themes">
              {themes
                .filter((t) => t.mode === 'dark')
                .map((t) => (
                  <option key={t.id} value={t.id}>
                    {t.name}
                  </option>
                ))}
            </optgroup>
          </select>
        </span>
      </label>
    </section>
  )
}
