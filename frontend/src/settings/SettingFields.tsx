import type { Setting } from '../lib/api'
import { SettingMeta } from './SettingMeta'
import { FIELD_METAS, settingToDisplay, type DraftValues } from './types'

export type FieldProps = {
  setting: Setting
  draft: DraftValues
  errors: Record<string, string>
  edit: (key: string, value: string | boolean) => void
  reset: (key: string) => void
  resettingKey: string | null
}

function ids(key: string) {
  const base = `field-${key.replaceAll('.', '-')}`
  return { field: base, help: `help-${base}`, error: `err-${base}`, lock: `lock-${key}` }
}

export function BooleanSettingField({ setting, draft, edit, reset, resettingKey }: FieldProps) {
  const meta = FIELD_METAS[setting.key]
  const id = ids(setting.key)
  const checked = draft[setting.key] !== undefined ? Boolean(draft[setting.key]) : setting.value === true

  return (
    <div>
      <label htmlFor={id.field} className="st-checkbox-label">
        <input
          id={id.field}
          type="checkbox"
          className="st-checkbox-input"
          checked={checked}
          disabled={setting.locked}
          aria-describedby={`${id.help}${setting.locked ? ` ${id.lock}` : ''}`}
          onChange={(e) => edit(setting.key, e.target.checked)}
        />
        <span>{meta.label}</span>
      </label>
      <p id={id.help} className="st-help">{meta.help}</p>
      <SettingMeta setting={setting} onReset={reset} isResetting={resettingKey === setting.key} fieldLabel={meta.label} />
    </div>
  )
}

export function IntegerSettingField({ setting, draft, errors, edit, reset, resettingKey }: FieldProps) {
  const meta = FIELD_METAS[setting.key]
  const id = ids(setting.key)
  const error = errors[setting.key]
  const value = draft[setting.key] !== undefined ? String(draft[setting.key]) : settingToDisplay(setting)

  return (
    <div>
      <label htmlFor={id.field} className="st-field">
        <span className="st-field-title">{meta.label}</span>
        <input
          id={id.field}
          type="number"
          className="st-input"
          value={value}
          readOnly={setting.locked}
          disabled={setting.locked}
          min={setting.min ?? undefined}
          max={setting.max ?? undefined}
          step={1}
          aria-invalid={Boolean(error)}
          aria-describedby={`${id.help}${error ? ` ${id.error}` : ''}${setting.locked ? ` ${id.lock}` : ''}`}
          onChange={(e) => { if (!setting.locked) edit(setting.key, e.target.value) }}
        />
        <p id={id.help} className="st-help">
          {meta.help}
          {setting.min !== null && setting.max !== null && <span> Between {setting.min} and {setting.max}.</span>}
          {setting.defaultValue !== null && <span> Default: {String(setting.defaultValue)}.</span>}
        </p>
        {error && <p id={id.error} className="st-error-text" role="status">{error}</p>}
      </label>
      <SettingMeta setting={setting} onReset={reset} isResetting={resettingKey === setting.key} fieldLabel={meta.label} />
    </div>
  )
}

export function TextSettingField({ setting, draft, edit, reset, resettingKey }: FieldProps) {
  const meta = FIELD_METAS[setting.key]
  const id = ids(setting.key)
  const value = draft[setting.key] !== undefined ? String(draft[setting.key]) : settingToDisplay(setting)

  return (
    <div className="st-field-wide">
      <label htmlFor={id.field} className="st-field">
        <span className="st-field-title">{meta.label}</span>
        <input
          id={id.field}
          type="text"
          className="st-input"
          value={value}
          readOnly={setting.locked}
          disabled={setting.locked}
          spellCheck={false}
          aria-describedby={`${id.help}${setting.locked ? ` ${id.lock}` : ''}`}
          onChange={(e) => { if (!setting.locked) edit(setting.key, e.target.value) }}
        />
        <p id={id.help} className="st-help">{meta.help}</p>
      </label>
      <SettingMeta setting={setting} onReset={reset} isResetting={resettingKey === setting.key} fieldLabel={meta.label} />
    </div>
  )
}
