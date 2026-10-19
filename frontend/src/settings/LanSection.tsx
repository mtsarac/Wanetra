import { Link } from 'react-router'
import type { Setting } from '../lib/api'
import { FormFooter } from './FormFooter'
import { BooleanSettingField, IntegerSettingField, TextSettingField } from './SettingFields'
import { LAN_ALERT_KEY, LAN_CIDR_KEY, LAN_ENABLED_KEY, LAN_INTEGER_KEYS } from './types'
import { useSettingsForm } from './useSettingsForm'

const KEYS: string[] = [LAN_ENABLED_KEY, ...LAN_INTEGER_KEYS, LAN_CIDR_KEY, LAN_ALERT_KEY]

export function LanSection({ settings }: { settings: Setting[] }) {
  const form = useSettingsForm({
    settings,
    activeKeys: () => KEYS,
    successMessage: 'Network scanning settings saved.',
  })

  const enabled = form.settingsMap.get(LAN_ENABLED_KEY)
  if (!enabled) return null

  const field = { draft: form.draft, errors: form.errors, edit: form.edit, reset: form.reset, resettingKey: form.resettingKey }
  const cidr = form.settingsMap.get(LAN_CIDR_KEY)
  const alert = form.settingsMap.get(LAN_ALERT_KEY)

  return (
    <section className="panel settings-panel st-panel" aria-labelledby="lan-heading">
      <div className="panel-head">
        <div>
          <p className="kicker">Network scanning</p>
          <h2 id="lan-heading">Devices on your network</h2>
        </div>
        <Link className="summary-link" to="/devices">Open devices</Link>
      </div>

      <form onSubmit={form.submit} className="st-form" noValidate>
        <BooleanSettingField setting={enabled} {...field} />

        <div className="st-fields-grid">
          {LAN_INTEGER_KEYS.map((key) => {
            const setting = form.settingsMap.get(key)
            return setting ? <IntegerSettingField key={key} setting={setting} {...field} /> : null
          })}
        </div>

        {cidr && <TextSettingField setting={cidr} {...field} />}
        {alert && <BooleanSettingField setting={alert} {...field} />}

        <FormFooter
          isSaving={form.isSaving}
          isDirty={form.isDirty}
          hasErrors={Object.keys(form.errors).length > 0}
          statusMessage={form.statusMessage}
          serverError={form.serverError}
          saveLabel="Save scanning settings"
        />
      </form>
    </section>
  )
}
