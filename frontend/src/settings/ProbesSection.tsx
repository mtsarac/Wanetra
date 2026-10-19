import type { Setting } from '../lib/api'
import { FormFooter } from './FormFooter'
import { BooleanSettingField, IntegerSettingField } from './SettingFields'
import { PROBES_ENABLED_KEY, PROBE_INTEGER_KEYS } from './types'
import { useSettingsForm } from './useSettingsForm'

const KEYS: string[] = [PROBES_ENABLED_KEY, ...PROBE_INTEGER_KEYS]

export function ProbesSection({ settings }: { settings: Setting[] }) {
  const form = useSettingsForm({
    settings,
    activeKeys: () => KEYS,
    successMessage: 'Connection monitoring settings saved.',
  })

  const enabled = form.settingsMap.get(PROBES_ENABLED_KEY)
  if (!enabled) return null

  const field = { draft: form.draft, errors: form.errors, edit: form.edit, reset: form.reset, resettingKey: form.resettingKey }

  return (
    <section className="panel settings-panel st-panel" aria-labelledby="probes-heading">
      <div className="panel-head">
        <div>
          <p className="kicker">Connection monitoring</p>
          <h2 id="probes-heading">Probes and outages</h2>
        </div>
        <span className="muted">Targets are managed on the Connectivity page</span>
      </div>

      <form onSubmit={form.submit} className="st-form" noValidate>
        <BooleanSettingField setting={enabled} {...field} />

        <div className="st-fields-grid">
          {PROBE_INTEGER_KEYS.map((key) => {
            const setting = form.settingsMap.get(key)
            return setting ? <IntegerSettingField key={key} setting={setting} {...field} /> : null
          })}
        </div>

        <FormFooter
          isSaving={form.isSaving}
          isDirty={form.isDirty}
          hasErrors={Object.keys(form.errors).length > 0}
          statusMessage={form.statusMessage}
          serverError={form.serverError}
          saveLabel="Save monitoring settings"
        />
      </form>
    </section>
  )
}
