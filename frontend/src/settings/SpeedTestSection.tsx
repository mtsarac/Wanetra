import type { Setting } from '../lib/api'
import { FormFooter } from './FormFooter'
import { OoklaLicenseBlock } from './OoklaLicenseBlock'
import { SettingMeta } from './SettingMeta'
import {
  ENGINE_FIELD_KEYS,
  ENGINE_OPTIONS,
  type EngineId,
  FIELD_METAS,
  lockReasonText,
  OOKLA_ACCEPT_LICENSE_KEY,
  settingToDisplay,
  sourceText,
  SPEEDTEST_ENGINE_KEY,
} from './types'
import { useSettingsForm } from './useSettingsForm'

export function SpeedTestSection({ settings }: { settings: Setting[] }) {
  const engineSetting = settings.find((s) => s.key === SPEEDTEST_ENGINE_KEY)
  const licenseSetting = settings.find((s) => s.key === OOKLA_ACCEPT_LICENSE_KEY)

  const activeKeysFn = (draft: Record<string, string | boolean>) => {
    const selected = (draft[SPEEDTEST_ENGINE_KEY] as EngineId | undefined)
      ?? (engineSetting?.value as EngineId | undefined)
      ?? 'librespeed'
    const perEngine = ENGINE_FIELD_KEYS[selected] ?? []
    return [SPEEDTEST_ENGINE_KEY, OOKLA_ACCEPT_LICENSE_KEY, ...perEngine]
  }

  const {
    draft,
    settingsMap,
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
    activeKeys: activeKeysFn,
    successMessage: 'Speed-test settings saved. Changes apply to the next test run.',
  })

  if (!engineSetting) return null

  const selectedEngine: EngineId = (draft[SPEEDTEST_ENGINE_KEY] as EngineId | undefined)
    ?? (engineSetting.value as EngineId | undefined)
    ?? 'librespeed'

  const licenseEffectiveAccepted = draft[OOKLA_ACCEPT_LICENSE_KEY] !== undefined
    ? Boolean(draft[OOKLA_ACCEPT_LICENSE_KEY])
    : Boolean(licenseSetting?.value)

  const licenseLocked = Boolean(licenseSetting?.locked)
  const engineLocked = engineSetting.locked
  const engineLockText = lockReasonText(engineSetting)

  const currentFieldKeys = ENGINE_FIELD_KEYS[selectedEngine] ?? []
  const hasErrors = Object.keys(errors).length > 0

  return (
    <section className="panel settings-panel st-panel" aria-labelledby="speedtest-engine-heading">
      <div className="panel-head">
        <div>
          <p className="kicker">Speed test</p>
          <h2 id="speedtest-engine-heading">Test engine and options</h2>
        </div>
        <span className="muted">Applies to the next run</span>
      </div>

      <form onSubmit={submit} className="st-form" noValidate>
        {/* Engine radio group */}
        <fieldset className="st-fieldset" aria-describedby={engineLockText ? `lock-${SPEEDTEST_ENGINE_KEY}` : undefined}>
          <legend className="st-legend">Engine</legend>
          <div className="st-radio-group" role="radiogroup" aria-label="Speed test engine">
            {ENGINE_OPTIONS.map((opt) => {
              const isSelected = selectedEngine === opt.id
              const isOoklaBlocked = opt.id === 'ookla' && !licenseEffectiveAccepted
              const isDisabled = engineLocked || isOoklaBlocked

              let warningText: string | null = null
              if (opt.id === 'ookla' && !licenseEffectiveAccepted) {
                warningText = licenseLocked
                  ? 'Acceptance is disabled by an environment variable; Ookla cannot be selected.'
                  : 'Requires accepting the Ookla EULA below before selection.'
              }

              return (
                <label
                  key={opt.id}
                  htmlFor={`engine-radio-${opt.id}`}
                  className={`st-radio-card ${isSelected ? 'st-selected' : ''} ${isDisabled ? 'st-disabled' : ''}`}
                  data-selected={isSelected}
                >
                  <input
                    id={`engine-radio-${opt.id}`}
                    type="radio"
                    name="speedtest-engine"
                    className="st-radio-input"
                    value={opt.id}
                    checked={isSelected}
                    disabled={isDisabled}
                    onChange={() => edit(SPEEDTEST_ENGINE_KEY, opt.id)}
                    aria-describedby={`engine-desc-${opt.id}${warningText ? ` engine-warn-${opt.id}` : ''}`}
                  />
                  <div className="st-radio-body">
                    <span className="st-radio-title">{opt.name}</span>
                    <p id={`engine-desc-${opt.id}`} className="st-radio-desc">
                      {opt.description}
                    </p>
                    {warningText && (
                      <p id={`engine-warn-${opt.id}`} className="st-radio-warning">
                        {warningText}
                      </p>
                    )}
                  </div>
                </label>
              )
            })}
          </div>

          <div className="st-meta-row">
            <div className="st-meta-info">
              <p className="st-source-line">
                Source: <span className="st-source-label">{sourceText(engineSetting)}</span>
              </p>
              {engineLockText && (
                <p id={`lock-${SPEEDTEST_ENGINE_KEY}`} className="st-lock-note">
                  {engineLockText}
                </p>
              )}
            </div>
            {engineSetting.source === 'stored' && !engineSetting.locked && (
              <button
                type="button"
                className="st-reset-button"
                onClick={() => reset(SPEEDTEST_ENGINE_KEY)}
                disabled={resettingKey === SPEEDTEST_ENGINE_KEY}
                aria-label="Reset Engine to default"
              >
                Reset to default<span className="st-sr-only"> for Engine</span>
              </button>
            )}
          </div>
        </fieldset>

        {/* Selected engine options */}
        <div className="st-fields-grid" aria-label={`${selectedEngine} configuration`}>
          {currentFieldKeys.map((key) => {
            const setting = settingsMap.get(key)
            if (!setting) return null

            const meta = FIELD_METAS[key] ?? { label: key, help: '' }
            const isRestricted = setting.lockReason === 'restricted'
            const isLocked = setting.locked
            const fieldError = errors[key]
            const fieldId = `field-${key.replace(/\./g, '-')}`
            const helpId = `help-${fieldId}`
            const errorId = `err-${fieldId}`
            const lockId = `lock-${setting.key}`

            const currentValue = draft[key] !== undefined
              ? String(draft[key])
              : settingToDisplay(setting)

            const describedBy = [
              meta.help ? helpId : null,
              fieldError ? errorId : null,
              setting.locked ? lockId : null,
            ].filter(Boolean).join(' ') || undefined

            const isWide = key.endsWith('executablePath')

            return (
              <div key={key} className={isWide ? 'st-field-wide' : undefined}>
                <label htmlFor={fieldId} className="st-field">
                  <span className="st-field-title">{meta.label}</span>
                  <input
                    id={fieldId}
                    type={setting.type === 'integer' ? 'number' : 'text'}
                    className="st-input"
                    value={currentValue}
                    placeholder={setting.value === null && isRestricted ? 'Not set (CLI managed by Wanetra)' : undefined}
                    readOnly={isRestricted || isLocked}
                    disabled={isLocked && !isRestricted}
                    min={setting.min ?? undefined}
                    max={setting.max ?? undefined}
                    step={setting.type === 'integer' ? 1 : undefined}
                    aria-invalid={Boolean(fieldError)}
                    aria-describedby={describedBy}
                    onChange={(e) => {
                      if (isLocked) return
                      edit(key, e.target.value)
                    }}
                  />
                  {meta.help && (
                    <p id={helpId} className="st-help">
                      {meta.help}
                      {setting.type === 'integer' && setting.min !== null && setting.max !== null && (
                        <span> Range: {setting.min}..{setting.max}.</span>
                      )}
                      {setting.defaultValue !== null && (
                        <span> Default: {String(setting.defaultValue)}.</span>
                      )}
                    </p>
                  )}
                  {fieldError && (
                    <p id={errorId} className="st-error-text" role="status">
                      {fieldError}
                    </p>
                  )}
                </label>
                <SettingMeta
                  setting={setting}
                  onReset={reset}
                  isResetting={resettingKey === key}
                  fieldLabel={meta.label}
                />
              </div>
            )
          })}
        </div>

        {/* Ookla license card */}
        <OoklaLicenseBlock
          setting={licenseSetting}
          draftAccepted={licenseEffectiveAccepted}
          isEngineOokla={selectedEngine === 'ookla'}
          isLicenseLocked={licenseLocked}
          onToggle={(checked) => edit(OOKLA_ACCEPT_LICENSE_KEY, checked)}
          onReset={reset}
          isResetting={resettingKey === OOKLA_ACCEPT_LICENSE_KEY}
        />

        <FormFooter
          isSaving={isSaving}
          isDirty={isDirty}
          hasErrors={hasErrors}
          statusMessage={statusMessage}
          serverError={serverError}
          saveLabel="Save speed-test settings"
        />
      </form>
    </section>
  )
}
