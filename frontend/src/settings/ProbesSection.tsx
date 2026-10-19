import type { Setting } from '../lib/api'
import { FormFooter } from './FormFooter'
import { SettingMeta } from './SettingMeta'
import {
  FIELD_METAS,
  PROBES_ENABLED_KEY,
  PROBE_INTEGER_KEYS,
  settingToDisplay,
} from './types'
import { useSettingsForm } from './useSettingsForm'

const KEYS: string[] = [PROBES_ENABLED_KEY, ...PROBE_INTEGER_KEYS]

export function ProbesSection({ settings }: { settings: Setting[] }) {
  const { draft, settingsMap, errors, isDirty, statusMessage, serverError, isSaving, resettingKey, edit, submit, reset } =
    useSettingsForm({
      settings,
      activeKeys: () => KEYS,
      successMessage: 'Connection monitoring settings saved.',
    })

  const enabled = settingsMap.get(PROBES_ENABLED_KEY)
  if (!enabled) return null

  const enabledMeta = FIELD_METAS[PROBES_ENABLED_KEY]
  const enabledChecked = draft[PROBES_ENABLED_KEY] !== undefined ? Boolean(draft[PROBES_ENABLED_KEY]) : enabled.value === true
  const hasErrors = Object.keys(errors).length > 0

  return (
    <section className="panel settings-panel st-panel" aria-labelledby="probes-heading">
      <div className="panel-head">
        <div>
          <p className="kicker">Connection monitoring</p>
          <h2 id="probes-heading">Probes and outages</h2>
        </div>
        <span className="muted">Targets are managed on the Connectivity page</span>
      </div>

      <form onSubmit={submit} className="st-form" noValidate>
        <div>
          <label htmlFor="field-probes-enabled" className="st-checkbox-label">
            <input
              id="field-probes-enabled"
              type="checkbox"
              className="st-checkbox-input"
              checked={enabledChecked}
              disabled={enabled.locked}
              aria-describedby="help-probes-enabled"
              onChange={(e) => edit(PROBES_ENABLED_KEY, e.target.checked)}
            />
            <span>{enabledMeta.label}</span>
          </label>
          <p id="help-probes-enabled" className="st-help">{enabledMeta.help}</p>
          <SettingMeta setting={enabled} onReset={reset} isResetting={resettingKey === PROBES_ENABLED_KEY} fieldLabel={enabledMeta.label} />
        </div>

        <div className="st-fields-grid">
          {PROBE_INTEGER_KEYS.map((key) => {
            const setting = settingsMap.get(key)
            if (!setting) return null
            const meta = FIELD_METAS[key]
            const fieldId = `field-${key.replaceAll('.', '-')}`
            const error = errors[key]
            const value = draft[key] !== undefined ? String(draft[key]) : settingToDisplay(setting)
            return (
              <div key={key}>
                <label htmlFor={fieldId} className="st-field">
                  <span className="st-field-title">{meta.label}</span>
                  <input
                    id={fieldId}
                    type="number"
                    className="st-input"
                    value={value}
                    readOnly={setting.locked}
                    disabled={setting.locked}
                    min={setting.min ?? undefined}
                    max={setting.max ?? undefined}
                    step={1}
                    aria-invalid={Boolean(error)}
                    aria-describedby={`help-${fieldId}${error ? ` err-${fieldId}` : ''}${setting.locked ? ` lock-${key}` : ''}`}
                    onChange={(e) => { if (!setting.locked) edit(key, e.target.value) }}
                  />
                  <p id={`help-${fieldId}`} className="st-help">
                    {meta.help}
                    {setting.min !== null && setting.max !== null && <span> Between {setting.min} and {setting.max}.</span>}
                    {setting.defaultValue !== null && <span> Default: {String(setting.defaultValue)}.</span>}
                  </p>
                  {error && <p id={`err-${fieldId}`} className="st-error-text" role="status">{error}</p>}
                </label>
                <SettingMeta setting={setting} onReset={reset} isResetting={resettingKey === key} fieldLabel={meta.label} />
              </div>
            )
          })}
        </div>

        <FormFooter
          isSaving={isSaving}
          isDirty={isDirty}
          hasErrors={hasErrors}
          statusMessage={statusMessage}
          serverError={serverError}
          saveLabel="Save monitoring settings"
        />
      </form>
    </section>
  )
}
