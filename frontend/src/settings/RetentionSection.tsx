import type { Setting } from '../lib/api'
import { FormFooter } from './FormFooter'
import { SettingMeta } from './SettingMeta'
import {
  FIELD_METAS,
  RETENTION_DAYS_KEY,
  settingToDisplay,
} from './types'
import { useSettingsForm } from './useSettingsForm'

export function RetentionSection({ settings }: { settings: Setting[] }) {
  const retentionSetting = settings.find((s) => s.key === RETENTION_DAYS_KEY)

  const {
    draft,
    errors,
    isDirty,
    statusMessage,
    serverError,
    isSaving,
    resettingKey,
    edit,
    submit,
    reset,
  } = useSettingsForm({
    settings,
    activeKeys: () => [RETENTION_DAYS_KEY],
    successMessage: 'Retention settings saved. Changes apply on the next cleanup pass.',
  })

  if (!retentionSetting) return null

  const meta = FIELD_METAS[RETENTION_DAYS_KEY] ?? { label: 'Keep results for (days)', help: '' }
  const isLocked = retentionSetting.locked
  const fieldError = errors[RETENTION_DAYS_KEY]
  const fieldId = 'field-retention-days'
  const helpId = 'help-retention-days'
  const errorId = 'err-retention-days'
  const lockId = `lock-${retentionSetting.key}`

  const currentValue = draft[RETENTION_DAYS_KEY] !== undefined
    ? String(draft[RETENTION_DAYS_KEY])
    : settingToDisplay(retentionSetting)

  const describedBy = [
    helpId,
    fieldError ? errorId : null,
    isLocked ? lockId : null,
  ].filter(Boolean).join(' ') || undefined

  return (
    <section className="panel settings-panel st-panel" aria-labelledby="retention-heading">
      <div className="panel-head">
        <div>
          <p className="kicker">Data retention</p>
          <h2 id="retention-heading">Measurement history</h2>
        </div>
        <span className="muted">Cleanup runs daily</span>
      </div>

      <form onSubmit={submit} className="st-form" noValidate>
        <div className="st-fields-grid">
          <div>
            <label htmlFor={fieldId} className="st-field">
              <span className="st-field-title">{meta.label}</span>
              <input
                id={fieldId}
                type="number"
                className="st-input"
                value={currentValue}
                readOnly={isLocked}
                disabled={isLocked}
                min={retentionSetting.min ?? undefined}
                max={retentionSetting.max ?? undefined}
                step={1}
                aria-invalid={Boolean(fieldError)}
                aria-describedby={describedBy}
                onChange={(e) => {
                  if (isLocked) return
                  edit(RETENTION_DAYS_KEY, e.target.value)
                }}
              />
              <p id={helpId} className="st-help">
                {meta.help}
                {retentionSetting.min !== null && (
                  <span> Minimum: {retentionSetting.min} days.</span>
                )}
                {retentionSetting.defaultValue !== null && (
                  <span> Default: {String(retentionSetting.defaultValue)} days.</span>
                )}
              </p>
              {fieldError && (
                <p id={errorId} className="st-error-text" role="status">
                  {fieldError}
                </p>
              )}
            </label>
            <SettingMeta
              setting={retentionSetting}
              onReset={reset}
              isResetting={resettingKey === RETENTION_DAYS_KEY}
              fieldLabel={meta.label}
            />
          </div>
        </div>

        <FormFooter
          isSaving={isSaving}
          isDirty={isDirty}
          hasErrors={Boolean(fieldError)}
          statusMessage={statusMessage}
          serverError={serverError}
          saveLabel="Save retention settings"
        />
      </form>
    </section>
  )
}
